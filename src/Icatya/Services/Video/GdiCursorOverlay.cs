using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace Icatya.Services.Video;

// Used only by monitor capture. WGC already captures window cursors natively.
// Caches shape metadata; never owns or destroys the system's shared HCURSOR.
internal sealed class GdiCursorOverlay
{
    private IntPtr _shape;
    private Size _size;
    private Point _hotspot;
    internal int ShapeReads { get; private set; }

    public bool DrawCurrent(IntPtr target, Rectangle source, Rectangle destination)
    {
        var cursor = new CursorInfo { Size = Marshal.SizeOf<CursorInfo>() };
        if (!GetCursorInfo(ref cursor) || (cursor.Flags & 3) != 1) return false;
        return Draw(target, cursor.Handle, cursor.Position, source, destination);
    }
    internal bool Draw(IntPtr target, IntPtr cursor, Point position, Rectangle source, Rectangle destination)
    {
        if (target == IntPtr.Zero || cursor == IntPtr.Zero || !source.Contains(position)) return false;
        if (cursor != _shape)
        {
            if (!GetIconInfo(cursor, out var info)) return false;
            try
            {
                IntPtr bitmap = info.Color != IntPtr.Zero ? info.Color : info.Mask;
                if (bitmap == IntPtr.Zero || GetObject(bitmap, Marshal.SizeOf<NativeBitmap>(), out var shape) == 0) return false;
                int height = info.Color != IntPtr.Zero ? shape.Height : shape.Height / 2;
                if (shape.Width <= 0 || height <= 0) return false;
                _size = new(shape.Width, height); _hotspot = new((int)info.XHotspot, (int)info.YHotspot);
                _shape = cursor; ShapeReads++;
            }
            finally
            {
                if (info.Mask != IntPtr.Zero) DeleteObject(info.Mask);
                if (info.Color != IntPtr.Zero) DeleteObject(info.Color);
            }
        }
        var area = Project(position, _hotspot, _size, source, destination);
        if (area == null) return false;
        int saved = SaveDC(target);
        if (saved == 0) return false;
        try
        {
            // Keep partial cursors inside the monitor image, including letterboxed captures.
            if (IntersectClipRect(target, destination.Left, destination.Top, destination.Right, destination.Bottom) <= 0) return false;
            return DrawIconEx(target, area.Value.X, area.Value.Y, cursor, area.Value.Width, area.Value.Height, 0, IntPtr.Zero, 3);
        }
        finally { RestoreDC(target, saved); }
    }
    internal static Rectangle? Project(Point position, Point hotspot, Size size, Rectangle source, Rectangle destination)
    {
        if (source.Width <= 0 || source.Height <= 0 || destination.Width <= 0 || destination.Height <= 0 || !source.Contains(position)) return null;
        double sx = (double)destination.Width / source.Width, sy = (double)destination.Height / source.Height;
        int x = (int)Math.Round(destination.X + ((double)position.X - source.X - hotspot.X) * sx);
        int y = (int)Math.Round(destination.Y + ((double)position.Y - source.Y - hotspot.Y) * sy);
        return new(x, y, Math.Max(1, (int)Math.Round(size.Width * sx)), Math.Max(1, (int)Math.Round(size.Height * sy)));
    }
    [StructLayout(LayoutKind.Sequential)] private struct CursorInfo
    { public int Size; public uint Flags; public IntPtr Handle; public Point Position; }
    [StructLayout(LayoutKind.Sequential)] private struct IconInfo
    { public int IsIcon; public uint XHotspot, YHotspot; public IntPtr Mask, Color; }
    [StructLayout(LayoutKind.Sequential)] private struct NativeBitmap
    { public int Type, Width, Height, WidthBytes; public ushort Planes, BitsPixel; public IntPtr Bits; }
    [DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CursorInfo cursor);
    [DllImport("user32.dll")] private static extern bool GetIconInfo(IntPtr cursor, out IconInfo icon);
    [DllImport("user32.dll")] private static extern bool DrawIconEx(IntPtr dc, int x, int y, IntPtr icon, int width, int height, uint step, IntPtr brush, uint flags);
    [DllImport("gdi32.dll", EntryPoint = "GetObjectW")] private static extern int GetObject(IntPtr bitmap, int size, out NativeBitmap result);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern int SaveDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool RestoreDC(IntPtr dc, int saved);
    [DllImport("gdi32.dll")] private static extern int IntersectClipRect(IntPtr dc, int left, int top, int right, int bottom);
}
