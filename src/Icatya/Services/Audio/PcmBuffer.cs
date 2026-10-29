using System;

namespace Icatya.Services.Audio;

// Bounded stereo PCM queue. Overflow discards old audio instead of adding delay.
public sealed class PcmBuffer
{
    private readonly object _gate = new();
    private readonly float[] _samples;
    private int _head, _count;
    public PcmBuffer(int capacity = 19200)
    {
        if (capacity < 2 || capacity % 2 != 0) throw new ArgumentOutOfRangeException(nameof(capacity));
        _samples = new float[capacity];
    }
    public int Count { get { lock (_gate) return _count; } }
    public void Write(ReadOnlySpan<float> samples)
    {
        lock (_gate)
            foreach (float sample in samples)
            {
                if (_count == _samples.Length) { _head = (_head + 1) % _samples.Length; _count--; }
                _samples[(_head + _count++) % _samples.Length] = float.IsFinite(sample) ? sample : 0;
            }
    }
    public int Read(Span<float> destination)
    {
        lock (_gate)
        {
            int length = Math.Min(destination.Length, _count);
            for (int i = 0; i < length; i++) { destination[i] = _samples[_head]; _head = (_head + 1) % _samples.Length; }
            _count -= length;
            destination[length..].Clear();
            return length;
        }
    }
    public void Clear() { lock (_gate) { _head = _count = 0; } }
}

