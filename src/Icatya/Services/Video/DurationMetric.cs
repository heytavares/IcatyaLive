using System.Diagnostics;
using System.Threading;

namespace Icatya.Services.Video;

public readonly record struct DurationSnapshot(long Count, double AverageMs, double MaximumMs);

// Fixed-size counters: profiling does not retain frames or allocate per sample.
public sealed class DurationMetric
{
    private long _count, _ticks, _maximum;
    public void Record(long started)
    {
        long elapsed = Stopwatch.GetTimestamp() - started;
        Interlocked.Add(ref _ticks, elapsed);
        long previous = Interlocked.Read(ref _maximum);
        while (elapsed > previous)
        {
            long actual = Interlocked.CompareExchange(ref _maximum, elapsed, previous);
            if (actual == previous) break;
            previous = actual;
        }
        Interlocked.Increment(ref _count);
    }
    public DurationSnapshot Snapshot()
    {
        long count = Interlocked.Read(ref _count);
        return new(count, count == 0 ? 0 : Interlocked.Read(ref _ticks) * 1000.0 / Stopwatch.Frequency / count,
            Interlocked.Read(ref _maximum) * 1000.0 / Stopwatch.Frequency);
    }
}
