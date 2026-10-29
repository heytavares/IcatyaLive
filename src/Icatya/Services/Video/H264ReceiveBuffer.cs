using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Icatya.Services.Video;

internal sealed record H264AccessUnit(byte[] Data, bool IsIdr, byte[]? Sps, byte[]? Pps);

// RFC 6184 mode 1. Only whole access units enter the predictive decoder.
// A short bounded reorder window also covers a marker arriving ahead of its fragments.
internal sealed class H264ReceiveBuffer
{
    private sealed class Frame(long timestamp, long created)
    {
        public long Timestamp = timestamp, Created = created;
        public long? End;
        public int Bytes;
        public readonly SortedDictionary<long, byte[]> Packets = new();
    }
    private readonly SortedDictionary<long, Frame> _frames = new();
    private long _highestSequence, _highestTimestamp, _lastReleased = long.MinValue;
    private long? _expected;
    private bool _initialized;
    private uint _ssrc;
    internal const int MaxFrameBytes = 4 * 1024 * 1024;
    public event Action<H264AccessUnit>? FrameReady;
    public event Action? Discontinuity;
    public long DroppedFrames { get; private set; }
    public int BufferedFrames => _frames.Count;
    public void Reset()
    {
        _frames.Clear(); _initialized = false; _expected = null; _lastReleased = long.MinValue;
    }
    public void Push(uint ssrc, ushort sequence, uint timestamp, bool marker, ReadOnlySpan<byte> payload)
    {
        long now = Stopwatch.GetTimestamp();
        if (_initialized && ssrc != _ssrc) { Reset(); Discontinuity?.Invoke(); }
        long seq = _initialized ? _highestSequence + unchecked((short)(sequence - (ushort)_highestSequence)) : sequence;
        long time = _initialized ? _highestTimestamp + unchecked((int)(timestamp - (uint)_highestTimestamp)) : timestamp;
        if (!_initialized) { _ssrc = ssrc; _highestSequence = seq; _highestTimestamp = time; _initialized = true; }
        _highestSequence = Math.Max(_highestSequence, seq); _highestTimestamp = Math.Max(_highestTimestamp, time);
        if (time <= _lastReleased) return;
        if (!_frames.TryGetValue(time, out var frame)) _frames.Add(time, frame = new(time, now));
        if (payload.Length == 0 || payload.Length > 65535 || frame.Packets.Count >= 4096 || frame.Bytes + payload.Length > MaxFrameBytes)
        {
            Drop(frame); Drain(now); return;
        }
        if (frame.Packets.TryAdd(seq, payload.ToArray())) frame.Bytes += payload.Length;
        if (marker) frame.End = seq;
        Drain(now);
    }
    public void Expire() => Drain(Stopwatch.GetTimestamp());
    private void Drain(long now)
    {
        while (_frames.Count > 0)
        {
            var frame = _frames.First().Value;
            long start = frame.Packets.Count == 0 ? 0 : frame.Packets.First().Key;
            bool contiguous = frame.End.HasValue && frame.Packets.Count > 0 && frame.Packets.Last().Key == frame.End &&
                frame.End.Value - start + 1 == frame.Packets.Count;
            bool follows = !_expected.HasValue || start == _expected;
            if (contiguous && follows)
            {
                _frames.Remove(frame.Timestamp); _lastReleased = frame.Timestamp; _expected = frame.End + 1;
                try { FrameReady?.Invoke(Assemble(frame.Packets.Values)); }
                catch (InvalidDataException) { DroppedFrames++; _expected = null; Discontinuity?.Invoke(); }
                continue;
            }
            if (_frames.Count <= 3 && Stopwatch.GetElapsedTime(frame.Created, now).TotalMilliseconds < 60) return;
            Drop(frame);
        }
    }
    private void Drop(Frame frame)
    {
        foreach (var key in _frames.Keys.Where(key => key <= frame.Timestamp).ToArray()) { _frames.Remove(key); DroppedFrames++; }
        _lastReleased = Math.Max(_lastReleased, frame.Timestamp);
        _expected = null; Discontinuity?.Invoke();
    }
    private static H264AccessUnit Assemble(IEnumerable<byte[]> packets)
    {
        var nals = new List<byte[]>();
        using var fragment = new MemoryStream();
        int fragmentHeader = -1;
        foreach (var payload in packets)
        {
            int type = payload[0] & 31;
            if ((payload[0] & 128) != 0) throw new InvalidDataException();
            if (type is >= 1 and <= 23)
            {
                if (fragmentHeader >= 0) throw new InvalidDataException();
                nals.Add(payload);
            }
            else if (type == 24)
            {
                if (fragmentHeader >= 0) throw new InvalidDataException();
                int offset = 1;
                while (offset + 2 <= payload.Length)
                {
                    int length = payload[offset] * 256 + payload[offset + 1]; offset += 2;
                    if (length == 0 || length > payload.Length - offset) throw new InvalidDataException();
                    nals.Add(payload.AsSpan(offset, length).ToArray()); offset += length;
                }
                if (offset != payload.Length) throw new InvalidDataException();
            }
            else if (type == 28)
            {
                if (payload.Length < 3 || (payload[1] & 32) != 0) throw new InvalidDataException();
                bool begin = (payload[1] & 128) != 0, end = (payload[1] & 64) != 0;
                int header = (payload[0] & 224) | (payload[1] & 31);
                if ((header & 31) is < 1 or > 23 || begin && end) throw new InvalidDataException();
                if (begin)
                {
                    if (fragmentHeader >= 0) throw new InvalidDataException();
                    fragmentHeader = header; fragment.SetLength(0); fragment.WriteByte((byte)header);
                }
                else if (fragmentHeader != header) throw new InvalidDataException();
                fragment.Write(payload, 2, payload.Length - 2);
                if (end) { nals.Add(fragment.ToArray()); fragmentHeader = -1; fragment.SetLength(0); }
            }
            else throw new InvalidDataException();
        }
        if (fragmentHeader >= 0 || nals.Count == 0 || nals.Count > 1024) throw new InvalidDataException();
        foreach (var nal in nals)
            if (nal.Length == 0 || (nal[0] & 128) != 0 || (nal[0] & 31) is < 1 or > 23) throw new InvalidDataException();
        var slices = nals.Where(n => (n[0] & 31) is 1 or 5).ToArray();
        // first_mb_in_slice == 0 is the one-bit unsigned Exp-Golomb code '1'.
        if (slices.Length == 0 || slices[0].Length < 2 || (slices[0][1] & 128) == 0) throw new InvalidDataException();
        bool idr = slices.All(n => (n[0] & 31) == 5);
        byte[]? sps = nals.LastOrDefault(n => (n[0] & 31) == 7), pps = nals.LastOrDefault(n => (n[0] & 31) == 8);
        if (sps?.Length > 65536 || pps?.Length > 65536) throw new InvalidDataException();
        using var output = new MemoryStream(nals.Sum(n => n.Length + 4));
        foreach (var nal in nals) { output.Write(new byte[] { 0, 0, 0, 1 }); output.Write(nal); }
        return new(output.ToArray(), idr, sps, pps);
    }
}
