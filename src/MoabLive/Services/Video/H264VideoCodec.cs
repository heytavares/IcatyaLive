using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using MoabLive.Models;

namespace MoabLive.Services.Video;

// A codec instance is used by one sender OR one receiver, under PeerSession's codec lock.
public sealed unsafe class H264VideoCodec : IDisposable
{
    private AVCodecContext* _encoder;
    private AVCodecContext* _decoder;
    private AVFrame* _encodeFrame;
    private AVFrame* _decodeFrame;
    private AVPacket* _encodePacket;
    private AVPacket* _decodePacket;
    private SwsContext* _toEncoder;
    private SwsContext* _toBgr;
    private CaptureProfile? _profile;
    private long _pts;
    private bool _forceKeyFrame = true;
    private bool _disposed;
    public string EncoderName { get; private set; } = "Aguardando configuração";
    public bool HardwareAccelerated { get; private set; }
    public string FallbackReason { get; private set; } = "";

    public H264VideoCodec() => NativeVideoRuntime.EnsureLoaded();

    public void Configure(CaptureProfile profile, bool softwareOnly = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!softwareOnly && _profile == profile && _encoder != null) { ForceKeyFrame(); return; }
        ResetEncoder();
        var failures = new List<string>();
        var candidates = softwareOnly ? new[] { "libopenh264" } : new[] { "h264_amf", "h264_nvenc", "h264_qsv", "libopenh264" };
        foreach (var name in candidates)
        {
            if (ffmpeg.avcodec_find_encoder_by_name(name) == null) continue;
            try
            {
                OpenEncoder(name, profile);
                using var probe = VideoFrame.Rent(profile.Width, profile.Height);
                Array.Clear(probe.Pixels, 0, probe.Stride * probe.Height);
                bool produced = false;
                for (int attempt = 0; attempt < 20 && !produced; attempt++)
                {
                    produced = Encode(probe) != null;
                    if (!produced) System.Threading.Thread.Sleep(2);
                }
                if (!produced) throw new InvalidOperationException("O codificador não produziu quadros durante a verificação.");
                ResetEncoder();
                OpenEncoder(name, profile);
                EncoderName = name;
                HardwareAccelerated = name != "libopenh264";
                FallbackReason = HardwareAccelerated ? "" : string.Join("; ", failures);
                ForceKeyFrame();
                return;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ApplicationException)
            {
                failures.Add(name + ": " + ex.Message);
                ResetEncoder();
            }
        }
        throw new InvalidOperationException("Não foi possível iniciar o vídeo H.264. " + string.Join("; ", failures));
    }

    private void OpenEncoder(string name, CaptureProfile profile)
    {
        var codec = ffmpeg.avcodec_find_encoder_by_name(name);
        _encoder = ffmpeg.avcodec_alloc_context3(codec);
        if (_encoder == null) throw new OutOfMemoryException();
        _encoder->width = profile.Width;
        _encoder->height = profile.Height;
        _encoder->pix_fmt = name == "libopenh264" ? AVPixelFormat.AV_PIX_FMT_YUV420P : AVPixelFormat.AV_PIX_FMT_NV12;
        _encoder->time_base = new AVRational { num = 1, den = profile.FramesPerSecond };
        _encoder->framerate = new AVRational { num = profile.FramesPerSecond, den = 1 };
        _encoder->bit_rate = profile.Bitrate;
        _encoder->gop_size = profile.FramesPerSecond;
        _encoder->max_b_frames = 0;
        _encoder->thread_count = 2;
        if (name == "h264_amf")
        {
            SetOption("usage", "lowlatency");
            SetOption("quality", "speed");
            SetOption("profile", "constrained_baseline");
            SetOption("level", "4.2");
            SetOption("forced_idr", "1");
            SetOption("header_spacing", profile.FramesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SetOption("async_depth", "1");
        }
        if (name == "h264_nvenc") { SetOption("preset", "p3"); SetOption("tune", "ull"); }
        Check(ffmpeg.avcodec_open2(_encoder, codec, null));
        _encodeFrame = ffmpeg.av_frame_alloc();
        _encodePacket = ffmpeg.av_packet_alloc();
        if (_encodeFrame == null || _encodePacket == null) throw new OutOfMemoryException();
        _encodeFrame->format = (int)_encoder->pix_fmt;
        _encodeFrame->width = profile.Width;
        _encodeFrame->height = profile.Height;
        Check(ffmpeg.av_frame_get_buffer(_encodeFrame, 32));
        _toEncoder = ffmpeg.sws_getContext(profile.Width, profile.Height, AVPixelFormat.AV_PIX_FMT_BGR24,
            profile.Width, profile.Height, _encoder->pix_fmt, (int)SwsFlags.SWS_FAST_BILINEAR, null, null, null);
        if (_toEncoder == null) throw new InvalidOperationException("Falha ao converter o formato de imagem.");
        _profile = profile;
        _pts = 0;
    }

    private void SetOption(string key, string value) => Check(ffmpeg.av_opt_set(_encoder->priv_data, key, value, 0));
    public void ForceKeyFrame() => _forceKeyFrame = true;

    public byte[]? Encode(VideoFrame input)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_encoder == null || _profile == null) throw new InvalidOperationException("Configure o codificador antes de transmitir.");
        if (input.Width != _profile.Width || input.Height != _profile.Height) throw new InvalidOperationException("A resolução do quadro difere do perfil configurado.");
        Check(ffmpeg.av_frame_make_writable(_encodeFrame));
        fixed (byte* pixels = input.Pixels)
        {
            byte*[] source = [pixels, null, null, null];
            int[] strides = [input.Stride, 0, 0, 0];
            ffmpeg.sws_scale(_toEncoder, source, strides, 0, input.Height, _encodeFrame->data, _encodeFrame->linesize);
        }
        _encodeFrame->pts = _pts++;
        _encodeFrame->pict_type = _forceKeyFrame ? AVPictureType.AV_PICTURE_TYPE_I : AVPictureType.AV_PICTURE_TYPE_NONE;
        _forceKeyFrame = false;
        Check(ffmpeg.avcodec_send_frame(_encoder, _encodeFrame));
        byte[]? result = null;
        int status;
        while ((status = ffmpeg.avcodec_receive_packet(_encoder, _encodePacket)) >= 0)
        {
            try
            {
                int previous = result?.Length ?? 0;
                var joined = new byte[previous + _encodePacket->size];
                if (result != null) Buffer.BlockCopy(result, 0, joined, 0, previous);
                Marshal.Copy((IntPtr)_encodePacket->data, joined, previous, _encodePacket->size);
                result = joined;
            }
            finally { ffmpeg.av_packet_unref(_encodePacket); }
        }
        if (status != ffmpeg.AVERROR(ffmpeg.EAGAIN) && status != ffmpeg.AVERROR_EOF) Check(status);
        return result;
    }

    // The frame belongs to this method; subscribers retain it if needed after their callback returns.
    public void Decode(byte[] encoded, Action<VideoFrame> consume)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_decoder == null)
        {
            var codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
            _decoder = ffmpeg.avcodec_alloc_context3(codec);
            if (_decoder == null) throw new OutOfMemoryException();
            _decoder->thread_count = 2;
            _decoder->thread_type = ffmpeg.FF_THREAD_SLICE;
            Check(ffmpeg.avcodec_open2(_decoder, codec, null));
            _decodeFrame = ffmpeg.av_frame_alloc();
            _decodePacket = ffmpeg.av_packet_alloc();
            if (_decodeFrame == null || _decodePacket == null) throw new OutOfMemoryException();
        }
        Check(ffmpeg.av_new_packet(_decodePacket, encoded.Length));
        try
        {
            Marshal.Copy(encoded, 0, (IntPtr)_decodePacket->data, encoded.Length);
            Check(ffmpeg.avcodec_send_packet(_decoder, _decodePacket));
        }
        finally { ffmpeg.av_packet_unref(_decodePacket); }
        int status;
        while ((status = ffmpeg.avcodec_receive_frame(_decoder, _decodeFrame)) >= 0)
        {
            try
            {
                int width = _decodeFrame->width, height = _decodeFrame->height;
                if (width is <= 0 or > 1920 || height is <= 0 or > 1080) throw new InvalidOperationException("Resolução recebida fora dos limites.");
                _toBgr = ffmpeg.sws_getCachedContext(_toBgr, width, height, (AVPixelFormat)_decodeFrame->format,
                    width, height, AVPixelFormat.AV_PIX_FMT_BGR24, (int)SwsFlags.SWS_FAST_BILINEAR, null, null, null);
                if (_toBgr == null) throw new InvalidOperationException("Falha ao converter a imagem recebida.");
                using var output = VideoFrame.Rent(width, height);
                fixed (byte* pixels = output.Pixels)
                {
                    byte*[] destination = [pixels, null, null, null];
                    int[] strides = [output.Stride, 0, 0, 0];
                    ffmpeg.sws_scale(_toBgr, _decodeFrame->data, _decodeFrame->linesize, 0, height, destination, strides);
                }
                consume(output);
            }
            finally { ffmpeg.av_frame_unref(_decodeFrame); }
        }
        if (status != ffmpeg.AVERROR(ffmpeg.EAGAIN) && status != ffmpeg.AVERROR_EOF) Check(status);
    }

    private static void Check(int result)
    {
        if (result >= 0) return;
        byte* message = stackalloc byte[1024];
        ffmpeg.av_strerror(result, message, 1024);
        throw new InvalidOperationException(Marshal.PtrToStringUTF8((IntPtr)message) ?? $"FFmpeg: {result}");
    }
    private void ResetEncoder()
    {
        if (_toEncoder != null) { ffmpeg.sws_freeContext(_toEncoder); _toEncoder = null; }
        fixed (AVCodecContext** context = &_encoder) ffmpeg.avcodec_free_context(context);
        fixed (AVFrame** frame = &_encodeFrame) ffmpeg.av_frame_free(frame);
        fixed (AVPacket** packet = &_encodePacket) ffmpeg.av_packet_free(packet);
        _profile = null;
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ResetEncoder();
        if (_toBgr != null) { ffmpeg.sws_freeContext(_toBgr); _toBgr = null; }
        fixed (AVCodecContext** context = &_decoder) ffmpeg.avcodec_free_context(context);
        fixed (AVFrame** frame = &_decodeFrame) ffmpeg.av_frame_free(frame);
        fixed (AVPacket** packet = &_decodePacket) ffmpeg.av_packet_free(packet);
    }
}


