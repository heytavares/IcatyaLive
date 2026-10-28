using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using MoabLive.Models;

namespace MoabLive.Services.Video;

// One reusable top-down DIB. Created, used and released on the capture thread.
internal sealed class GdiScreenCapture : IDisposable
{
    private IntPtr _screen, _memory, _bitmap, _previous, _pixels;
    private readonly Rectangle _bounds;
    private readonly Rectangle _destination;
    private readonly int _width, _height;
    public GdiScreenCapture(Rectangle bounds, CaptureProfile profile)
    {
        if (profile.Width % 4 != 0) throw new ArgumentException("A largura precisa ser múltipla de quatro.");
        _bounds = bounds; _width = profile.Width; _height = profile.Height;
        var scale = Math.Min((double)_width / bounds.Width, (double)_height / bounds.Height);
        int width = (int)(bounds.Width * scale), height = (int)(bounds.Height * scale);
        _destination = new((_width - width) / 2, (_height - height) / 2, width, height);
        try
        {
            _screen = GetDC(IntPtr.Zero);
            if (_screen == IntPtr.Zero) throw new Win32Exception();
            _memory = CreateCompatibleDC(_screen);
            if (_memory == IntPtr.Zero) throw new Win32Exception();
            var info = new BitmapInfo { Header = new BitmapHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapHeader>(), Width = _width, Height = -_height,
                Planes = 1, Bits = 24, ImageSize = (uint)(_width * _height * 3)
            } };
            _bitmap = CreateDIBSection(_screen, ref info, 0, out _pixels, IntPtr.Zero, 0);
            if (_bitmap == IntPtr.Zero || _pixels == IntPtr.Zero) throw new Win32Exception();
            _previous = SelectObject(_memory, _bitmap);
            if (_previous == IntPtr.Zero || _previous == new IntPtr(-1)) throw new Win32Exception();
            SetStretchBltMode(_memory, 3);
            if (!PatBlt(_memory, 0, 0, _width, _height, 0x00000042)) throw new Win32Exception();
        }
        catch { Dispose(); throw; }
    }
    public void CopyTo(VideoFrame frame)
    {
        if (_memory == IntPtr.Zero) throw new ObjectDisposedException(nameof(GdiScreenCapture));
        bool copied;
        if (_bounds.Size == new Size(_width, _height))
            copied = BitBlt(_memory, 0, 0, _width, _height, _screen, _bounds.X, _bounds.Y, 0x00CC0020);
        else
            copied = StretchBlt(_memory, _destination.X, _destination.Y, _destination.Width, _destination.Height,
                _screen, _bounds.X, _bounds.Y, _bounds.Width, _bounds.Height, 0x00CC0020);
        if (!copied) throw new Win32Exception(Marshal.GetLastWin32Error(), "Não foi possível capturar este monitor.");
        GdiFlush();
        Marshal.Copy(_pixels, frame.Pixels, 0, _width * _height * 3);
    }
    public void Dispose()
    {
        if (_memory != IntPtr.Zero && _previous != IntPtr.Zero && _previous != new IntPtr(-1)) SelectObject(_memory, _previous);
        if (_bitmap != IntPtr.Zero) { DeleteObject(_bitmap); _bitmap = IntPtr.Zero; }
        if (_memory != IntPtr.Zero) { DeleteDC(_memory); _memory = IntPtr.Zero; }
        if (_screen != IntPtr.Zero) { ReleaseDC(IntPtr.Zero, _screen); _screen = IntPtr.Zero; }
        _previous = _pixels = IntPtr.Zero;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapHeader
    {
        public uint Size; public int Width, Height; public ushort Planes, Bits;
        public uint Compression, ImageSize; public int XResolution, YResolution; public uint ColorsUsed, ColorsImportant;
    }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo { public BitmapHeader Header; public uint Colors; }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr pixels, IntPtr section, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool GdiFlush();
    [DllImport("gdi32.dll")] private static extern int SetStretchBltMode(IntPtr dc, int mode);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool PatBlt(IntPtr dc, int x, int y, int width, int height, uint operation);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, uint operation);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool StretchBlt(IntPtr target, int x, int y, int width, int height, IntPtr source, int sourceX, int sourceY, int sourceWidth, int sourceHeight, uint operation);
}

