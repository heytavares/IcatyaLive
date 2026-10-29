using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Icatya.Models;

namespace Icatya.Services.Video;

// UI-thread only. Two lazy surfaces bound memory and avoid waiting for the compositor's current surface.
internal sealed class BitmapPresenter
{
    private readonly WriteableBitmap?[] _surfaces = new WriteableBitmap?[2];
    private int _current, _width, _height, _sourceWidth;
    private int[]? _columns;
    public WriteableBitmap? Bitmap { get; private set; }
    public DurationMetric UploadTime { get; } = new();
    public long BusyLocks { get; private set; }
    public long AlternativeSurfaceFrames { get; private set; }
    public bool TryPresent(VideoFrame frame, bool thumbnail = false)
    {
        int width = thumbnail ? Math.Min(frame.Width, 320) : frame.Width;
        int height = thumbnail ? Math.Max(1, (int)((long)frame.Height * width / frame.Width)) : frame.Height;
        if (width != _width || height != _height)
        {
            Clear(); _width = width; _height = height;
        }
        if (thumbnail && (_columns == null || _sourceWidth != frame.Width))
        {
            _columns = new int[width]; _sourceWidth = frame.Width;
            for (int x = 0; x < width; x++) _columns[x] = (int)((long)x * frame.Width / width) * 3;
        }
        for (int attempt = 0; attempt < _surfaces.Length; attempt++)
        {
            int index = (_current + attempt) % _surfaces.Length;
            var surface = _surfaces[index] ??= new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr32, null);
            if (!surface.TryLock(new Duration(TimeSpan.Zero))) { BusyLocks++; continue; }
            long started = Stopwatch.GetTimestamp();
            try
            {
                PresentationPixels.Copy(frame, surface.BackBuffer, surface.BackBufferStride, width, height, _columns);
                surface.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally { surface.Unlock(); UploadTime.Record(started); }
            if (attempt > 0) AlternativeSurfaceFrames++;
            Bitmap = surface; _current = index;
            return true;
        }
        return false;
    }
    public void Clear()
    {
        Array.Clear(_surfaces); Bitmap = null; _columns = null;
        _width = _height = _sourceWidth = _current = 0;
    }
}
