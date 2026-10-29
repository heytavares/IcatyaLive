using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace Icatya.Services.Audio;

// One output device and one independent jitter/decoder state per remote transmission.
public sealed class AudioPlayback : ISampleProvider, IDisposable
{
    private sealed class Stream : IDisposable
    {
        private readonly object _gate = new();
        private readonly SortedDictionary<long, byte[]> _packets = new();
        private readonly PcmBuffer _pcm = new(9600);
        private readonly OpusAudioDecoder _decoder = new();
        private long _highest, _last = long.MinValue;
        private bool _received, _started, _disposed;
        private DateTime _oldest;
        public void Add(ushort sequence, byte[] payload)
        {
            lock (_gate)
            {
                if (_disposed || payload.Length is <= 0 or > 1275) return;
                long extended = _received ? _highest + unchecked((short)(sequence - unchecked((ushort)_highest))) : sequence;
                if (extended <= _last || _packets.ContainsKey(extended)) return;
                if (!_received || extended > _highest) _highest = extended;
                _received = true;
                if (_packets.Count == 0) _oldest = DateTime.UtcNow;
                _packets.Add(extended, payload.ToArray());
                while (_packets.Count > 10) _packets.Remove(_packets.First().Key);
            }
        }
        public void Read(Span<float> output)
        {
            lock (_gate)
            {
                if (_disposed) { output.Clear(); return; }
                while (_pcm.Count < output.Length && _packets.Count > 0)
                {
                    if (!_started && _packets.Count < 3 && DateTime.UtcNow - _oldest < TimeSpan.FromMilliseconds(60)) break;
                    _started = true;
                    var packet = _packets.First(); _packets.Remove(packet.Key);
                    if (packet.Key <= _last) continue;
                    _last = packet.Key;
                    try { _pcm.Write(_decoder.Decode(packet.Value)); }
                    catch (Exception ex) when (ex is not OutOfMemoryException) { /* malformed/late packet cannot stop the other streams */ }
                }
                if (_pcm.Read(output) == 0 && _packets.Count == 0) _started = false;
            }
        }
        public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _packets.Clear(); _pcm.Clear(); _decoder.Dispose(); } }
    }
    private readonly object _gate = new();
    private readonly Dictionary<string, Stream> _streams = new();
    private readonly HashSet<string> _allowed = new();
    private WasapiOut? _output;
    private bool _opening, _disposed;
    private DateTime _retryAt;
    private float _volume = 0.65f;
    private int _epoch;
    public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(48000, 2);
    public float Volume { get => System.Threading.Volatile.Read(ref _volume); set => System.Threading.Volatile.Write(ref _volume, float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0); }
    public event Action<string>? Warning;
    public void Allow(string sender) { lock (_gate) if (!_disposed) _allowed.Add(sender); }
    public void AddPacket(string sender, ushort sequence, byte[] payload, bool openDevice = true)
    {
        lock (_gate)
        {
            if (_disposed || !_allowed.Contains(sender)) return;
            if (!_streams.TryGetValue(sender, out var stream)) _streams.Add(sender, stream = new());
            stream.Add(sequence, payload);
            if (openDevice && !_opening && _output == null && DateTime.UtcNow >= _retryAt)
            {
                _opening = true;
                int epoch = _epoch;
                _ = Task.Run(() => OpenOutput(epoch));
            }
        }
    }
    private void OpenOutput(int epoch)
    {
        WasapiOut? output = null;
        try
        {
            output = new WasapiOut(AudioClientShareMode.Shared, true, 60);
            output.Init(this);
            output.PlaybackStopped += (_, args) => { if (args.Exception != null) OutputFailed(output, args.Exception); };
            lock (_gate)
            {
                if (_disposed || epoch != _epoch || _streams.Count == 0) { output.Dispose(); return; }
                _output = output;
                output.Play();
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            output?.Dispose();
            lock (_gate) { _output = null; _retryAt = DateTime.UtcNow.AddSeconds(5); }
            Warning?.Invoke("Não foi possível reproduzir o som. Confira o dispositivo de saída do Windows. " + ex.Message);
        }
        finally { lock (_gate) _opening = false; }
    }
    private void OutputFailed(WasapiOut output, Exception error)
    {
        lock (_gate) { if (_output != output) return; _output = null; _retryAt = DateTime.UtcNow.AddSeconds(5); }
        _ = Task.Run(output.Dispose);
        Warning?.Invoke("Saída de áudio interrompida. Tentando usar novamente o dispositivo padrão do Windows. " + error.Message);
    }
    public int Read(float[] buffer, int offset, int count)
    {
        var destination = buffer.AsSpan(offset, count);
        destination.Clear();
        Stream[] streams;
        lock (_gate) streams = _streams.Values.ToArray();
        var scratch = ArrayPool<float>.Shared.Rent(1920);
        try {
            foreach (var stream in streams)
                for (int start = 0; start < count; start += 1920) {
                    int length = Math.Min(1920, count - start);
                    stream.Read(scratch.AsSpan(0, length));
                    AudioCaptureMixer.Mix(scratch.AsSpan(0, length), destination.Slice(start, length), 1);
                }
        } finally { ArrayPool<float>.Shared.Return(scratch); }
        float volume = Volume;
        for (int i = 0; i < count; i++) destination[i] = Math.Clamp(destination[i] * volume, -1, 1);
        return count;
    }
    public void Remove(string sender)
    {
        Stream? stream;
        WasapiOut? output = null;
        lock (_gate)
        {
            _allowed.Remove(sender);
            _streams.Remove(sender, out stream);
            if (_streams.Count == 0) { output = _output; _output = null; _epoch++; }
        }
        stream?.Dispose();
        if (output != null) _ = Task.Run(() => { output.Stop(); output.Dispose(); });
    }
    public void Clear()
    {
        string[] ids;
        lock (_gate) { _allowed.Clear(); ids = _streams.Keys.ToArray(); }
        foreach (string id in ids) Remove(id);
    }
    public void Dispose() { lock (_gate) _disposed = true; Clear(); }
}
