using System;
using System.Buffers;
using System.Threading;

namespace MoabLive.Models;

// Frames have shared ownership: capture/decoder and the single-slot UI mailbox release separately.
public sealed class VideoFrame : IDisposable
{
    private readonly byte[] _pixels;
    private readonly bool _pooled;
    private int _references = 1;
    public byte[] Pixels => Volatile.Read(ref _references) > 0 ? _pixels : throw new ObjectDisposedException(nameof(VideoFrame));
    public int Width { get; }
    public int Height { get; }
    public int Stride { get; }
    public VideoFrame(byte[] pixels, int width, int height, int stride) : this(pixels, width, height, stride, false) { }
    private VideoFrame(byte[] pixels, int width, int height, int stride, bool pooled)
    {
        if (width <= 0 || height <= 0 || stride < width * 3 || pixels.Length < checked(stride * height))
            throw new ArgumentException("Buffer de vídeo inválido.");
        _pixels = pixels; Width = width; Height = height; Stride = stride; _pooled = pooled;
    }
    public static VideoFrame Rent(int width, int height)
    {
        if (width is <= 0 or > 1920 || height is <= 0 or > 1080) throw new ArgumentOutOfRangeException(nameof(width), "Resolução fora dos limites.");
        return new(ArrayPool<byte>.Shared.Rent(checked(width * height * 3)), width, height, width * 3, true);
    }
    public VideoFrame Retain()
    {
        int count;
        do
        {
            count = Volatile.Read(ref _references);
            if (count <= 0) throw new ObjectDisposedException(nameof(VideoFrame));
        } while (Interlocked.CompareExchange(ref _references, count + 1, count) != count);
        return this;
    }
    public void Dispose()
    {
        int count = Interlocked.Decrement(ref _references);
        if (count == 0 && _pooled) ArrayPool<byte>.Shared.Return(_pixels);
        if (count < 0) throw new InvalidOperationException("O quadro foi liberado mais de uma vez.");
    }
}

public sealed record CaptureProfile(string Name, int Width, int Height, int FramesPerSecond, int Bitrate = 4_000_000)
{
    public override string ToString() => Name;
    public static readonly CaptureProfile[] Available =
    [
        new("720p · 30 fps · 4 Mbps", 1280, 720, 30, 4_000_000),
        new("720p · meta 60 fps · 6 Mbps", 1280, 720, 60, 6_000_000),
        new("1080p · 30 fps · 7 Mbps", 1920, 1080, 30, 7_000_000),
        new("1080p · meta 60 fps · 10 Mbps", 1920, 1080, 60, 10_000_000)
    ];
}


