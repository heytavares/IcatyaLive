using System;
using System.Diagnostics;
using System.Windows.Media;

namespace Icatya.Services.Video;

// Render at WPF's composition boundary instead of two independent 16 ms timers.
internal sealed class PresentationClock
{
    private bool _running;
    private TimeSpan _last = TimeSpan.MinValue;
    public long TickCount { get; private set; }
    public DurationMetric CallbackTime { get; } = new();
    public event EventHandler? Tick;
    public void Start()
    {
        if (_running) return;
        _running = true; _last = TimeSpan.MinValue;
        CompositionTarget.Rendering += Render;
    }
    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= Render;
    }
    private void Render(object? sender, EventArgs args)
    {
        if (args is RenderingEventArgs rendering) {
            if (rendering.RenderingTime == _last) return;
            _last = rendering.RenderingTime;
        }
        long started = Stopwatch.GetTimestamp();
        TickCount++;
        try { Tick?.Invoke(this, args); }
        finally { CallbackTime.Record(started); }
    }
}
