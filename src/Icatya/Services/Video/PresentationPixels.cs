using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Icatya.Models;

namespace Icatya.Services.Video;

internal static class PresentationPixels
{
    private static readonly Vector128<byte> Expand = Vector128.Create((byte)0, 1, 2, 128, 3, 4, 5, 128, 6, 7, 8, 128, 9, 10, 11, 128);
    private static readonly Vector128<byte> Opaque = Vector128.Create(0xff000000u).AsByte();

    // Expand BGR24 into WPF's native 32-bit surface. SIMD tails never read beyond a source row.
    internal static unsafe void Copy(VideoFrame frame, IntPtr destination, int stride, int width, int height, int[]? columns = null, bool vectorize = true)
    {
        fixed (byte* source = frame.Pixels)
        {
            for (int y = 0; y < height; y++)
            {
                byte* input = source + (height == frame.Height ? y : (int)((long)y * frame.Height / height)) * frame.Stride;
                byte* output = (byte*)destination + y * stride;
                int x = 0;
                if (width == frame.Width && height == frame.Height)
                {
                    if (vectorize && Ssse3.IsSupported)
                        for (; x + 5 < width; x += 4)
                            Sse2.Store(output + x * 4, Sse2.Or(Ssse3.Shuffle(Sse2.LoadVector128(input + x * 3), Expand), Opaque));
                    for (; x < width; x++)
                        ((uint*)output)[x] = 0xff000000u | input[x * 3] | (uint)input[x * 3 + 1] << 8 | (uint)input[x * 3 + 2] << 16;
                }
                else
                {
                    for (; x < width; x++)
                    {
                        int at = columns![x];
                        ((uint*)output)[x] = 0xff000000u | input[at] | (uint)input[at + 1] << 8 | (uint)input[at + 2] << 16;
                    }
                }
            }
        }
    }
}
