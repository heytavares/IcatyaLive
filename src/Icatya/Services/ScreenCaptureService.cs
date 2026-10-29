using System;
using System.Diagnostics;
using System.Linq;
using System.Drawing;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Icatya.Models;
using Icatya.Services.Video;

namespace Icatya.Services;

public sealed class ScreenCaptureService
{
    public DurationMetric CaptureTime { get; private set; } = new();
    public DurationMetric ConsumeTime { get; private set; } = new();
    private long _droppedFrames;
    public long DroppedFrames => Interlocked.Read(ref _droppedFrames);
    public async Task RunAsync(Rectangle bounds, CaptureProfile profile, Action<VideoFrame> consume,
        CancellationToken cancellation)
        => await RunCoreAsync(bounds, null, profile, consume, cancellation);
    public async Task RunAsync(ShareSource source, CaptureProfile profile, Action<VideoFrame> consume, CancellationToken cancellation)
    {
        source.Validate();
        var bounds = source.IsWindow ? Rectangle.Empty :
            System.Windows.Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == source.DisplayName)?.Bounds
                ?? throw new InvalidOperationException("O monitor escolhido não está disponível.");
        await RunCoreAsync(bounds, source.IsWindow ? source : null, profile, consume, cancellation);
    }
    private async Task RunCoreAsync(Rectangle bounds, ShareSource? window, CaptureProfile profile, Action<VideoFrame> consume,
        CancellationToken cancellation)
    {
        CaptureTime = new(); ConsumeTime = new();
        Interlocked.Exchange(ref _droppedFrames, 0);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var frames = Channel.CreateBounded<VideoFrame>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleWriter = true, SingleReader = true
        }, dropped => { Interlocked.Increment(ref _droppedFrames); dropped.Dispose(); });
        // Capture and encode overlap. Only the latest unprocessed frame is retained.
        var capture = Task.Factory.StartNew(() =>
        {
            try
            {
                using var source = window == null ? new GdiScreenCapture(bounds, profile) : null;
                using var app = window != null ? new WindowCapture(window) : null;
                using var pacer = new FramePacer(profile.FramesPerSecond, stop.Token);
                while (!stop.IsCancellationRequested)
                {
                    long started = Stopwatch.GetTimestamp();
                    using var frame = VideoFrame.Rent(profile.Width, profile.Height);
                    if (app != null && !app.TryCopyTo(frame)) { if (!pacer.WaitNext()) break; continue; }
                    source?.CopyTo(frame);
                    CaptureTime.Record(started);
                    stop.Token.ThrowIfCancellationRequested();
                    var queued = frame.Retain();
                    if (!frames.Writer.TryWrite(queued)) queued.Dispose();
                    if (!pacer.WaitNext()) break;
                }
            }
            catch { stop.Cancel(); throw; }
            finally { frames.Writer.TryComplete(); }
        }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        var encode = Task.Run(async () =>
        {
            try
            {
                await foreach (var frame in frames.Reader.ReadAllAsync(stop.Token))
                    using (frame)
                    {
                        stop.Token.ThrowIfCancellationRequested();
                        long started = Stopwatch.GetTimestamp();
                        consume(frame);
                        ConsumeTime.Record(started);
                    }
            }
            catch { stop.Cancel(); throw; }
        });
        try { await Task.WhenAll(capture, encode); }
        finally
        {
            stop.Cancel();
            while (frames.Reader.TryRead(out var remaining)) remaining.Dispose();
        }
    }
}
