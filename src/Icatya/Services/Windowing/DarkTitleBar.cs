using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Icatya.Services.Windowing;

// Keep the native caption buttons, resize behavior and system menu.
internal static class DarkTitleBar
{
    private const int UseImmersiveDarkMode = 20;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    public static void Attach(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        Apply(handle);
        var source = HwndSource.FromHwnd(handle);
        source?.AddHook((IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled) =>
        {
            // Reapply after Windows theme/composition changes, without modifying system settings.
            if (message is 0x031A or 0x031E) Apply(hwnd);
            return IntPtr.Zero;
        });
    }

    private static void Apply(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
        int enabled = 1;
        if (DwmSetWindowAttribute(handle, UseImmersiveDarkMode, ref enabled, sizeof(int)) < 0)
            DwmSetWindowAttribute(handle, 19, ref enabled, sizeof(int));
        // COLORREF is 0x00BBGGRR. Unsupported attributes are ignored by older Windows builds.
        int background = 0x00141212; // #121214
        int foreground = 0x00F1EFEF; // #EFEFF1
        int border = 0x00383333;     // #333338
        DwmSetWindowAttribute(handle, CaptionColor, ref background, sizeof(int));
        DwmSetWindowAttribute(handle, TextColor, ref foreground, sizeof(int));
        DwmSetWindowAttribute(handle, BorderColor, ref border, sizeof(int));
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}


