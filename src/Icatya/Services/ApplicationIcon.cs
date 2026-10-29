using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Icatya.Services;

internal static class ApplicationIcon
{
    public static ImageSource? Load(int processId)
    {
        try {
            using var process = Process.GetProcessById(processId);
            var path = process.MainModule?.FileName;
            if (path == null) return null;
            using var icon = Icon.ExtractAssociatedIcon(path);
            if (icon == null) return null;
            var image = Imaging.CreateBitmapSourceFromHIcon(icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(24,24));
            image.Freeze(); return image;
        } catch (Exception) { return null; }
    }
}
