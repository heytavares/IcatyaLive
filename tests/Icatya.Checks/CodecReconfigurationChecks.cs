using System;
using Icatya.Models;
using Icatya.Services.Video;

internal static class CodecReconfigurationChecks
{
    public static int Run()
    {
        try
        {
            using var encoder = new H264VideoCodec();
            using var decoder = new H264VideoCodec();
            foreach (var profile in new[] { CaptureProfile.Available[0], CaptureProfile.Available[3], CaptureProfile.Available[1] })
            {
                encoder.Configure(profile);
                using var input = VideoFrame.Rent(profile.Width, profile.Height);
                Array.Fill(input.Pixels, (byte)110, 0, input.Stride * input.Height);
                int decoded = 0;
                for (int frame = 0; frame < 8; frame++)
                {
                    var packet = encoder.Encode(input);
                    if (packet == null) continue;
                    decoder.Decode(packet, image =>
                    {
                        if (image.Width != profile.Width || image.Height != profile.Height) throw new Exception("Resolução antiga após reconfiguração");
                        if (Math.Abs(image.Pixels[image.Stride * (image.Height / 2)] - 110) > 25) throw new Exception("Pixels incorretos após reconfiguração");
                        decoded++;
                    });
                }
                if (decoded < 3) throw new Exception("Quadros insuficientes após mudança de perfil");
                Console.WriteLine($"PASS reconfiguração {profile.Width}x{profile.Height}@{profile.FramesPerSecond}: {decoded} quadros, {encoder.EncoderName}");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

