using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Icatya.Models;
using Icatya.Services.Rooms;

// Synthetic peers for inspecting the real WPF multi-stream layout without capturing the desktop.
internal static class RoomPreview
{
    public static async Task<int> RunAsync()
    {
        string code = File.ReadAllText("work/room-preview-code.txt").Trim();
        await using var b = new RoomSession(new(code, "Beto · teste"), false);
        await using var c = new RoomSession(new(code, "Caio · teste"), false);
        b.Start(new("http://127.0.0.1:8787/")); c.Start(new("http://127.0.0.1:8787/"));
        b.ConfigureVideo(CaptureProfile.Available[0], true);
        c.ConfigureVideo(CaptureProfile.Available[0], true);
        b.SetSharing(true); c.SetSharing(true);
        using var blue = VideoFrame.Rent(1280, 720);
        using var green = VideoFrame.Rent(1280, 720);
        for (int i = 0; i < blue.Stride * blue.Height; i += 3)
        {
            blue.Pixels[i] = 130; blue.Pixels[i + 1] = 65; blue.Pixels[i + 2] = 40;
            green.Pixels[i] = 65; green.Pixels[i + 1] = 110; green.Pixels[i + 2] = 40;
        }
        Console.WriteLine("Synthetic peers ready for UI inspection; no desktop capture.");
        var until = DateTime.UtcNow.AddMinutes(4);
        while (DateTime.UtcNow < until) { b.SendFrame(blue); c.SendFrame(green); await Task.Delay(100); }
        return 0;
    }
}
