using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Icatya.Models;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Icatya.Services.Video;
using System.Diagnostics;

namespace Icatya.Services;

// Owns one peer and its codec. Encoding/decoding never runs on the WPF dispatcher.
public sealed class PeerSession : IDisposable
{
    public const int ManualSignalingTimeoutSeconds = 120;
    static PeerSession()
    {
        // This library exposes a process-wide timeout. Allow time for manual chat exchange.
        RtpIceChannel.FAILED_TIMEOUT_PERIOD = ManualSignalingTimeoutSeconds;
    }
    private readonly RTCPeerConnection _peer;
    private readonly H264VideoCodec _codec = new();
    private readonly object _codecGate = new();
    private readonly object _sendGate = new();
    private RTCDataChannel? _control;
    private bool _disposed;
    private bool _canSend;
    private bool _remoteApplied;
    private long _networkFrames, _decodedFrames, _decodeErrors, _stunMessages, _sentFrames;
    public DurationMetric EncodeTime { get; } = new();
    public DurationMetric SendTime { get; } = new();
    public DurationMetric DecodeTime { get; } = new();
    public string PerformanceSummary()
    {
        var encode = EncodeTime.Snapshot();
        var send = SendTime.Snapshot();
        var decode = DecodeTime.Snapshot();
        return $"Compressão média/máxima: {encode.AverageMs:F2}/{encode.MaximumMs:F2} ms\nEnvio médio/máximo: {send.AverageMs:F2}/{send.MaximumMs:F2} ms\nDecodificação média/máxima: {decode.AverageMs:F2}/{decode.MaximumMs:F2} ms\n";
    }
    public string DiagnosticSummary()
    {
        var candidates = _peer.GetRtpChannel().Candidates;
        return $"Icatya 0.5.2\nPapel: {(_isSender ? "transmite" : "assiste")}\nSessão: {_session.ToString("N")[..8]}\nDados do outro lado aplicados: {_remoteApplied}\nConexão: {_peer.connectionState}\nVerificação da rede: {_peer.iceConnectionState}\nColeta de endereços: {_peer.iceGatheringState}\nEndereços locais disponíveis: {candidates.Count}\nRespostas STUN/ICE recebidas: {Interlocked.Read(ref _stunMessages)}\nQuadros encaminhados para envio: {Interlocked.Read(ref _sentFrames)}\nQuadros recebidos da rede: {Interlocked.Read(ref _networkFrames)}\nQuadros decodificados: {Interlocked.Read(ref _decodedFrames)}\nErros de decodificação: {Interlocked.Read(ref _decodeErrors)}\nCompartilhamento ativo neste PC: {_canSend}\n";
    }
    private readonly bool _isSender;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Guid _session;
    private CaptureProfile _profile = CaptureProfile.Available[0];
    private long _lastPacketAt;
    private long _lastSharingStateAt;
    public string EncodingDescription => _codec.HardwareAccelerated ? _codec.EncoderName + " · GPU" : _codec.EncoderName + " · CPU";
    public string FallbackReason => _codec.FallbackReason;
    public void ConfigureVideo(CaptureProfile profile, bool softwareOnly = false)
    {
        lock (_sendGate) lock (_codecGate)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(PeerSession));
            _codec.Configure(profile, softwareOnly);
            _profile = profile;
        }
    }
    public bool IsConnected => !_disposed && _peer.connectionState == RTCPeerConnectionState.connected;
    public bool IsSender => _isSender;
    public event Action<string>? StatusChanged;
    public event Action<VideoFrame>? FrameReceived;
    public event Action? RemotePaused;
    public event Action? RemoteResumed;
    public event Action? ConnectionLost;
    public event Action<string>? Error;
    public Task Ready => _ready.Task;

    public PeerSession(bool sender, bool useStun = true)
    {
        _isSender = sender;
        _peer = new RTCPeerConnection(new RTCConfiguration
        {
            iceServers = useStun ? new List<RTCIceServer> { new() { urls = "stun:stun.cloudflare.com:3478" } } : new()
        });
        var formats = new List<VideoFormat>
        {
            new(VideoCodecsEnum.H264, 96, 90000, "packetization-mode=1;profile-level-id=42e02a;level-asymmetry-allowed=1")
        };
        _peer.addTrack(new MediaStreamTrack(formats,
            sender ? MediaStreamStatusEnum.SendOnly : MediaStreamStatusEnum.RecvOnly));
        _peer.GetRtpChannel().OnStunMessageReceived += (_, _, _) => Interlocked.Increment(ref _stunMessages);
        _peer.OnVideoFrameReceived += (endpoint, timestamp, encoded, format) =>
        {
            if (_disposed || _isSender) return;
            Interlocked.Increment(ref _networkFrames);
            try
            {
                long started = Stopwatch.GetTimestamp();
                lock (_codecGate)
                    if (!_disposed) _codec.Decode(encoded, frame =>
                    {
                        Interlocked.Increment(ref _decodedFrames);
                        FrameReceived?.Invoke(frame);
                    });
                DecodeTime.Record(started);
            }
            catch (Exception) { Interlocked.Increment(ref _decodeErrors); StatusChanged?.Invoke("Recebendo dados, mas aguardando uma imagem de vídeo completa…"); }
        };
        _peer.onconnectionstatechange += state =>
        {
            if (_disposed) return;
            switch (state)
            {
                case RTCPeerConnectionState.connected:
                    StatusChanged?.Invoke("Conexão direta estabelecida · vídeo protegido por WebRTC");
                    _ready.TrySetResult(); break;
                case RTCPeerConnectionState.connecting:
                    StatusChanged?.Invoke("Tentando conectar diretamente ao outro PC…"); break;
                case RTCPeerConnectionState.failed:
                    StatusChanged?.Invoke("Não foi possível conectar. Confira a troca de convite/resposta e copie o diagnóstico para investigar a rede.");
                    ConnectionLost?.Invoke(); break;
                case RTCPeerConnectionState.disconnected:
                case RTCPeerConnectionState.closed:
                    StatusChanged?.Invoke("Conexão interrompida. Crie outro convite para tentar novamente.");
                    ConnectionLost?.Invoke(); break;
            }
        };
        _peer.ondatachannel += AttachControl;
    }

    public async Task<string> CreateOfferAsync(CancellationToken cancellation = default)
    {
        if (!_isSender) throw new InvalidOperationException("Somente quem transmite cria o convite.");
        _session = Guid.NewGuid();
        AttachControl(await _peer.createDataChannel("moablive-control-v1"));
        await GatherAsync(cancellation);
        var offer = _peer.createOffer();
        await _peer.setLocalDescription(offer);
        return InviteCodec.Encode(new(1, "offer", _session, DateTimeOffset.UtcNow.AddMinutes(20), offer.sdp));
    }

    public async Task<string> AcceptOfferAsync(string code, CancellationToken cancellation = default)
    {
        var invite = InviteCodec.Decode(code);
        if (invite.Kind != "offer" || _isSender)
            throw new InvalidOperationException("Para assistir, cole o convite de quem está transmitindo.");
        _session = invite.Session;
        ApplyRemote(invite);
        await GatherAsync(cancellation);
        var answer = _peer.createAnswer();
        await _peer.setLocalDescription(answer);
        return InviteCodec.Encode(new(1, "answer", _session, invite.Expires, answer.sdp));
    }

    public void ApplyAnswer(string code)
    {
        var invite = InviteCodec.Decode(code);
        if (invite.Kind != "answer") throw new InvalidOperationException("Este texto é um convite. Neste PC, cole a resposta gerada pelo computador que vai assistir.");
        if (!_isSender || invite.Session != _session)
            throw new InvalidOperationException("Esta resposta não pertence ao convite aberto nesta janela.");
        ApplyRemote(invite);
    }

    private void ApplyRemote(SessionInvite invite)
    {
        var result = _peer.setRemoteDescription(new RTCSessionDescriptionInit
        { type = invite.Kind == "offer" ? RTCSdpType.offer : RTCSdpType.answer, sdp = invite.Sdp });
        if (result != SetDescriptionResultEnum.OK)
            throw new InvalidOperationException("Não foi possível aceitar os dados da conexão: " + result);
        _remoteApplied = true;
    }

    private async Task GatherAsync(CancellationToken cancellation)
    {
        var deadline = DateTime.UtcNow.AddSeconds(12);
        while (_peer.iceGatheringState != RTCIceGatheringState.complete && DateTime.UtcNow < deadline)
        {
            cancellation.ThrowIfCancellationRequested();
            await Task.Delay(100, cancellation);
        }
        cancellation.ThrowIfCancellationRequested();
    }

    private void AttachControl(RTCDataChannel channel)
    {
        if (channel.label != "moablive-control-v1") { channel.close(); return; }
        _control = channel;
        channel.onopen += () =>
        {
            lock (_sendGate)
                if (!_disposed && _isSender) channel.send(_canSend ? "started" : "paused");
        };
        channel.onmessage += (_, protocol, bytes) =>
        {
            if (_disposed || bytes.Length > 256) return;
            var message = Encoding.UTF8.GetString(bytes);
            if (message == "paused") RemotePaused?.Invoke();
            if (message == "started") RemoteResumed?.Invoke();
        };
    }

    public void SetSending(bool enabled)
    {
        lock (_sendGate)
        {
            _canSend = enabled && _isSender && !_disposed;
            if (_control?.readyState == RTCDataChannelState.open)
            {
                _control.send(_canSend ? "started" : "paused");
                _lastSharingStateAt = Stopwatch.GetTimestamp();
            }
            if (_canSend) { _lastPacketAt = 0; lock (_codecGate) _codec.ForceKeyFrame(); }
        }
    }

    public void SendFrame(VideoFrame frame)
    {
        lock (_sendGate)
        {
            if (!_canSend || !IsConnected) return;
            if (_control?.readyState == RTCDataChannelState.open &&
                (_lastSharingStateAt == 0 || Stopwatch.GetElapsedTime(_lastSharingStateAt).TotalSeconds >= 1))
            {
                _control.send("started");
                _lastSharingStateAt = Stopwatch.GetTimestamp();
            }
            try
            {
                lock (_codecGate)
                {
                    long encodeStarted = Stopwatch.GetTimestamp();
                    var encoded = _codec.Encode(frame);
                    EncodeTime.Record(encodeStarted);
                    if (encoded != null)
                    {
                        long now = Stopwatch.GetTimestamp();
                        uint duration = _lastPacketAt == 0 ? (uint)(90000 / _profile.FramesPerSecond) :
                            (uint)Math.Clamp(Stopwatch.GetElapsedTime(_lastPacketAt, now).TotalSeconds * 90000, 1, 450000);
                        _lastPacketAt = now;
                        long sendStarted = Stopwatch.GetTimestamp();
                        _peer.SendVideo(duration, encoded);
                        SendTime.Record(sendStarted);
                        Interlocked.Increment(ref _sentFrames);
                    }
                }
            }
            catch (Exception ex) { Error?.Invoke("Não foi possível codificar o vídeo: " + ex.Message); }
        }
    }

    public void Dispose()
    {
        lock (_sendGate)
        {
            if (_disposed) return;
            _canSend = false;
            _disposed = true;
            _peer.Close("Icatya encerrado");
            _peer.Dispose();
            lock (_codecGate) _codec.Dispose();
        }
    }
}










