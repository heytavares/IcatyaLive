using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Icatya.Controls;

// DWM supplies a live thumbnail without capturing or encoding every selectable window.
public sealed class WindowThumbnail : FrameworkElement
{
    private readonly IntPtr _source;
    private IntPtr _thumbnail, _destination;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    public WindowThumbnail(IntPtr source)
    {
        _source = source;
        Loaded += (_, _) => {
            _destination = new WindowInteropHelper(Window.GetWindow(this)).Handle;
            if (DwmRegisterThumbnail(_destination, _source, out _thumbnail) == 0) _timer.Start();
        };
        Unloaded += (_, _) => { _timer.Stop(); if (_thumbnail != IntPtr.Zero) DwmUnregisterThumbnail(_thumbnail); _thumbnail = IntPtr.Zero; };
        _timer.Tick += (_, _) => Update();
    }
    private void Update()
    {
        if (_thumbnail == IntPtr.Zero || !IsVisible || ActualWidth < 2 || ActualHeight < 2) return;
        var origin = new NativePoint(); ClientToScreen(_destination, ref origin);
        var top = PointToScreen(new Point()); var bottom = PointToScreen(new Point(ActualWidth, ActualHeight));
        bool visible = true;
        DependencyObject? parent = this;
        while ((parent = VisualTreeHelper.GetParent(parent)) != null) {
            if (parent is System.Windows.Controls.ScrollContentPresenter viewport) {
                var a = viewport.PointToScreen(new Point()); var b = viewport.PointToScreen(new Point(viewport.ActualWidth, viewport.ActualHeight));
                visible &= top.Y >= a.Y && bottom.Y <= b.Y && top.X >= a.X && bottom.X <= b.X;
            }
        }
        var properties = new Properties { Flags = 1 | 4 | 8 | 16, Opacity = 255, Visible = visible, ClientOnly = false,
            Destination = new RectNative { Left = (int)top.X-origin.X, Top = (int)top.Y-origin.Y, Right = (int)bottom.X-origin.X, Bottom = (int)bottom.Y-origin.Y } };
        if (DwmQueryThumbnailSourceSize(_thumbnail, out var size) == 0 && size.X > 0 && size.Y > 0) {
            double ratio = Math.Min((bottom.X-top.X)/size.X, (bottom.Y-top.Y)/size.Y);
            int width = (int)(size.X*ratio), height = (int)(size.Y*ratio);
            int x = properties.Destination.Left + ((int)(bottom.X-top.X)-width)/2;
            int y = properties.Destination.Top + ((int)(bottom.Y-top.Y)-height)/2;
            properties.Destination = new RectNative { Left=x,Top=y,Right=x+width,Bottom=y+height };
        }
        DwmUpdateThumbnailProperties(_thumbnail, ref properties);
    }
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct Properties {
        public uint Flags; public RectNative Destination, Source; public byte Opacity;
        [MarshalAs(UnmanagedType.Bool)] public bool Visible;
        [MarshalAs(UnmanagedType.Bool)] public bool ClientOnly;
    }
    [DllImport("dwmapi.dll")] private static extern int DwmRegisterThumbnail(IntPtr destination, IntPtr source, out IntPtr thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUnregisterThumbnail(IntPtr thumbnail);
    [DllImport("dwmapi.dll")] private static extern int DwmUpdateThumbnailProperties(IntPtr thumbnail, ref Properties properties);
    [DllImport("dwmapi.dll")] private static extern int DwmQueryThumbnailSourceSize(IntPtr thumbnail, out NativePoint size);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);
}
