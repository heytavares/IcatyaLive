namespace MoabLive.Models;

public sealed record VideoFrame(byte[] Pixels, int Width, int Height, int Stride);
public sealed record CaptureProfile(string Name, int Width, int Height, int FramesPerSecond)
{
    public override string ToString() => Name;
    public static readonly CaptureProfile[] Available =
    [
        new("Leve · 640 × 352 · até 10 fps", 640, 352, 10),
        new("Detalhe · 1280 × 720 · até 10 fps", 1280, 720, 10)
    ];
}

