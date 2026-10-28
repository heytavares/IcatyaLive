using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MoabLive.Models;

namespace MoabLive.Services;

public sealed class ScreenCaptureService
{
    // A sequential loop: one capture and one encode at a time, never a backlog of frames.
    public Task RunAsync(Rectangle bounds, CaptureProfile profile, Action<VideoFrame> consume,
        CancellationToken cancellation) => Task.Run(async () =>
    {
        using var desktop = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
        using var scaled = new Bitmap(profile.Width, profile.Height, PixelFormat.Format24bppRgb);
        using var capture = Graphics.FromImage(desktop);
        using var resize = Graphics.FromImage(scaled);
        resize.InterpolationMode = InterpolationMode.Bilinear;
        var factor = Math.Min((double)profile.Width / bounds.Width, (double)profile.Height / bounds.Height);
        var width = (int)(bounds.Width * factor);
        var height = (int)(bounds.Height * factor);
        var destination = new Rectangle((profile.Width - width) / 2, (profile.Height - height) / 2, width, height);
        var period = TimeSpan.FromSeconds(1.0 / profile.FramesPerSecond);
        while (!cancellation.IsCancellationRequested)
        {
            var watch = Stopwatch.StartNew();
            capture.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            resize.Clear(Color.Black);
            resize.DrawImage(desktop, destination);
            var bits = scaled.LockBits(new Rectangle(0, 0, scaled.Width, scaled.Height),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            VideoFrame frame;
            try
            {
                var stride = scaled.Width * 3;
                var pixels = new byte[stride * scaled.Height];
                for (int row = 0; row < scaled.Height; row++)
                    Marshal.Copy(bits.Scan0 + row * bits.Stride, pixels, row * stride, stride);
                frame = new(pixels, scaled.Width, scaled.Height, stride);
            }
            finally { scaled.UnlockBits(bits); }
            cancellation.ThrowIfCancellationRequested();
            consume(frame);
            var remaining = period - watch.Elapsed;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellation);
        }
    }, cancellation);
}
