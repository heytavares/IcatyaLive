using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Icatya.Services.Video;

internal static class CursorChecks
{
    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    public static int Run()
    {
        try
        {
            var source = new Rectangle(-1920, 0, 1920, 1080);
            var destination = new Rectangle(0, 0, 1280, 720);
            var projected = GdiCursorOverlay.Project(new Point(-960, 540), new Point(12, 12), new Size(48, 48), source, destination);
            Require(projected == new Rectangle(632, 352, 32, 32), "Negative monitor coordinates, hotspot or scaling are wrong");
            Require(GdiCursorOverlay.Project(new Point(1, 540), Point.Empty, new Size(32, 32), source, destination) == null,
                "Cursor from another monitor leaked into the capture");
            var boxed = GdiCursorOverlay.Project(new Point(512, 384), new Point(8, 8), new Size(32, 32),
                new Rectangle(0, 0, 1024, 768), new Rectangle(160, 0, 960, 720));
            Require(boxed == new Rectangle(632, 352, 30, 30), "Letterbox cursor position is wrong");
            Console.WriteLine("PASS cursor projection: negative coordinates, hotspot, 1080p/720p, letterbox and other-monitor exclusion");

            using var image = new Bitmap(192, 160, PixelFormat.Format32bppArgb);
            using var graphics = Graphics.FromImage(image);
            graphics.Clear(Color.Magenta);
            IntPtr arrow = LoadCursor(IntPtr.Zero, new IntPtr(32512));
            IntPtr beam = LoadCursor(IntPtr.Zero, new IntPtr(32513));
            Require(arrow != IntPtr.Zero && beam != IntPtr.Zero, "Standard system cursors unavailable");
            var overlay = new GdiCursorOverlay();
            var capture = new Rectangle(0, 0, 128, 128);
            var content = new Rectangle(40, 16, 96, 96);
            IntPtr dc = graphics.GetHdc();
            try
            {
                Require(overlay.Draw(dc, arrow, new Point(0, 0), capture, content), "Native cursor not drawn");
                int reads = overlay.ShapeReads;
                for (int i = 0; i < 200; i++) Require(overlay.Draw(dc, arrow, new Point(0, 0), capture, content), "Cached cursor failed");
                Require(overlay.ShapeReads == reads, "Allocates cursor bitmaps every frame");
                uint before = GetGuiResources(GetCurrentProcess(), 0);
                for (int i = 0; i < 500; i++) overlay.Draw(dc, i % 2 == 0 ? arrow : beam, new Point(0, 0), capture, content);
                uint after = GetGuiResources(GetCurrentProcess(), 0);
                Require(after <= before + 4, $"Cursor shape changes leaked GDI resources: {before} -> {after}");
                Require(!overlay.Draw(dc, arrow, new Point(200, 0), capture, content), "Draws a cursor outside the selected monitor");
            }
            finally { graphics.ReleaseHdc(dc); }
            int changed = 0;
            for (int y = 0; y < image.Height; y++) for (int x = 0; x < image.Width; x++)
            {
                bool different = image.GetPixel(x, y).ToArgb() != Color.Magenta.ToArgb();
                if (different) { changed++; Require(content.Contains(x, y), "Cursor overwrote letterbox pixels"); }
            }
            Require(changed > 3, "Cursor native draw produced no visible pixels");
            Console.WriteLine("PASS real system cursor rendered to an offscreen bitmap, clipped correctly, shape metadata cached and GDI objects released");
            Console.WriteLine("No desktop capture, cursor movement, system setting change or network transmission.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);
    [DllImport("user32.dll")] private static extern uint GetGuiResources(IntPtr process, uint flags);
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
}
