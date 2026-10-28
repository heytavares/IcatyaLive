using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MoabLive.Models;
using MoabLive.Services.Video;

namespace MoabLive.Services;

public sealed class ScreenCaptureService
{
    public async Task RunAsync(Rectangle bounds, CaptureProfile profile, Action<VideoFrame> consume,
        CancellationToken cancellation)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var frames = Channel.CreateBounded<VideoFrame>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest, SingleWriter = true, SingleReader = true
        }, dropped => dropped.Dispose());
        // Capture and encode overlap. Only the latest unprocessed frame is retained.
        var capture = Task.Factory.StartNew(() =>
        {
            try
            {
                using var source = new GdiScreenCapture(bounds, profile);
                var period = TimeSpan.FromSeconds(1.0 / profile.FramesPerSecond);
                while (!stop.IsCancellationRequested)
                {
                    long started = Stopwatch.GetTimestamp();
                    using var frame = VideoFrame.Rent(profile.Width, profile.Height);
                    source.CopyTo(frame);
                    stop.Token.ThrowIfCancellationRequested();
                    var queued = frame.Retain();
                    if (!frames.Writer.TryWrite(queued)) queued.Dispose();
                    var remaining = period - Stopwatch.GetElapsedTime(started);
                    if (remaining > TimeSpan.Zero && stop.Token.WaitHandle.WaitOne(remaining)) break;
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
                    using (frame) { stop.Token.ThrowIfCancellationRequested(); consume(frame); }
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
