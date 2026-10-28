using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Icatya.Models;

namespace Icatya.Services.Video;

// UI-thread only. Do not stall controls when WPF's render thread owns the bitmap.
internal sealed class BitmapPresenter
{
    public WriteableBitmap? Bitmap { get; private set; }
    public bool TryPresent(VideoFrame frame)
    {
        if (Bitmap == null || Bitmap.PixelWidth != frame.Width || Bitmap.PixelHeight != frame.Height)
            Bitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr24, null);
        if (!Bitmap.TryLock(new Duration(TimeSpan.Zero))) return false;
        try
        {
            int rowBytes = checked(frame.Width * 3);
            if (frame.Stride == Bitmap.BackBufferStride)
                Marshal.Copy(frame.Pixels, 0, Bitmap.BackBuffer, checked(frame.Stride * frame.Height));
            else
                for (int row = 0; row < frame.Height; row++)
                    Marshal.Copy(frame.Pixels, row * frame.Stride, Bitmap.BackBuffer + row * Bitmap.BackBufferStride, rowBytes);
            Bitmap.AddDirtyRect(new Int32Rect(0, 0, frame.Width, frame.Height));
            return true;
        }
        finally { Bitmap.Unlock(); }
    }
    public void Clear() => Bitmap = null;
}
