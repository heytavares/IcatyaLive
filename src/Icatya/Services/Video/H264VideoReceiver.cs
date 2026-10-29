using System;
using System.Diagnostics;
using System.IO;
using Icatya.Models;

namespace Icatya.Services.Video;

// Serialized by PeerSession's receive lock, independent for every incoming live.
internal sealed class H264VideoReceiver
{
    private readonly H264VideoCodec _codec;
    private readonly H264ReceiveBuffer _buffer = new();
    private readonly Action<VideoFrame> _present;
    private readonly Action _requestKeyFrame;
    private byte[]? _sps, _pps;
    private bool _waiting = true;
    private long _lastRequest;
    public long RejectedFrames => _buffer.DroppedFrames;
    public long RecoveryRequests { get; private set; }
    public long DecodeErrors { get; private set; }
    public DurationMetric DecodeTime { get; } = new();
    public H264VideoReceiver(H264VideoCodec codec, Action<VideoFrame> present, Action requestKeyFrame)
    {
        _codec = codec; _present = present; _requestKeyFrame = requestKeyFrame;
        _buffer.FrameReady += Receive;
        _buffer.Discontinuity += Recover;
    }
    public void Push(uint ssrc, ushort sequence, uint timestamp, bool marker, ReadOnlySpan<byte> payload) => _buffer.Push(ssrc, sequence, timestamp, marker, payload);
    public void Expire() { _buffer.Expire(); if (_waiting) Request(); }
    public void Reset()
    {
        _buffer.Reset(); _sps = _pps = null; _waiting = true; _lastRequest = 0; _codec.ResetDecoder();
    }
    private void Recover() { _waiting = true; _codec.ResetDecoder(); Request(); }
    private void Request()
    {
        long now = Stopwatch.GetTimestamp();
        if (_lastRequest != 0 && Stopwatch.GetElapsedTime(_lastRequest, now).TotalMilliseconds < 500) return;
        _lastRequest = now; RecoveryRequests++; _requestKeyFrame();
    }
    private void Receive(H264AccessUnit unit)
    {
        if (unit.Sps != null) _sps = unit.Sps;
        if (unit.Pps != null) _pps = unit.Pps;
        if (_waiting && (!unit.IsIdr || _sps == null || _pps == null)) { Request(); return; }
        byte[] data = unit.Data;
        if (_waiting)
        {
            using var complete = new MemoryStream();
            if (unit.Sps == null) { complete.Write(new byte[] { 0, 0, 0, 1 }); complete.Write(_sps!); }
            if (unit.Pps == null) { complete.Write(new byte[] { 0, 0, 0, 1 }); complete.Write(_pps!); }
            complete.Write(data); data = complete.ToArray();
        }
        try
        {
            bool produced = false;
            long started = Stopwatch.GetTimestamp();
            _codec.Decode(data, frame => { produced = true; _present(frame); });
            DecodeTime.Record(started);
            if (produced) _waiting = false;
        }
        catch (InvalidOperationException) { DecodeErrors++; Recover(); }
    }
}
