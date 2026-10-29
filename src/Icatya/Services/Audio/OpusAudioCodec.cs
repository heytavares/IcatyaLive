using System;
using Concentus;
using Concentus.Enums;

namespace Icatya.Services.Audio;

public sealed class OpusAudioEncoder : IDisposable
{
    public const int Rate = 48000, Channels = 2, FrameSamples = 960, InterleavedSamples = 1920;
    private readonly IOpusEncoder _encoder = OpusCodecFactory.CreateEncoder(Rate, Channels, OpusApplication.OPUS_APPLICATION_AUDIO);
    private readonly byte[] _encoded = new byte[1275];
    public OpusAudioEncoder() { _encoder.Bitrate = 96000; _encoder.Complexity = 5; _encoder.UseInbandFEC = true; _encoder.PacketLossPercent = 5; }
    public byte[] Encode(ReadOnlySpan<float> pcm)
    {
        if (pcm.Length != InterleavedSamples) throw new ArgumentException("O áudio precisa de 20 ms estéreo.");
        int count = _encoder.Encode(pcm, FrameSamples, _encoded, _encoded.Length);
        return _encoded.AsSpan(0, count).ToArray();
    }
    public void Dispose() => _encoder.Dispose();
}

public sealed class OpusAudioDecoder : IDisposable
{
    private readonly IOpusDecoder _decoder = OpusCodecFactory.CreateDecoder(48000, 2);
    private readonly float[] _decoded = new float[5760 * 2];
    public float[] Decode(ReadOnlySpan<byte> packet)
    {
        if (packet.Length is <= 0 or > 1275) return Array.Empty<float>();
        int frames = _decoder.Decode(packet, _decoded, 5760, false);
        return _decoded.AsSpan(0, frames * 2).ToArray();
    }
    public void Dispose() => _decoder.Dispose();
}
