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
    private volatile bool _disposed;
    private readonly H264VideoReceiver _videoReceiver;
    private readonly Timer? _recoveryTimer;
    private long _lastKeyFrameRequest;
    private bool _canSend;
    private volatile bool _remoteSending;
    private volatile bool _audioNegotiated;
    private volatile bool _watching = true, _viewerWatching = true;
    private bool _remoteApplied;
    private long _networkFrames, _decodedFrames, _stunMessages, _sentFrames;
    public DurationMetric EncodeTime { get; } = new();
    public DurationMetric SendTime { get; } = new();
    public DurationMetric DecodeTime => _videoReceiver.DecodeTime;
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
        return $"iCatya 1.0.0\nPapel: {(_isSender ? "transmite" : "assiste")}\nSessão: {_session.ToString("N")[..8]}\nDados do outro lado aplicados: {_remoteApplied}\nConexão: {_peer.connectionState}\nVerificação da rede: {_peer.iceConnectionState}\nColeta de endereços: {_peer.iceGatheringState}\nEndereços locais disponíveis: {candidates.Count}\nRespostas STUN/ICE recebidas: {Interlocked.Read(ref _stunMessages)}\nQuadros encaminhados para envio: {Interlocked.Read(ref _sentFrames)}\nQuadros recebidos da rede: {Interlocked.Read(ref _networkFrames)}\nQuadros decodificados: {Interlocked.Read(ref _decodedFrames)}\nErros de decodificação: {_videoReceiver.DecodeErrors}\nQuadros incompletos descartados: {_videoReceiver.RejectedFrames}\nPedidos de recuperação: {_videoReceiver.RecoveryRequests}\nCompartilhamento ativo neste PC: {_canSend}\n";
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
    internal bool ConnectionFailed => _disposed || _peer.connectionState is RTCPeerConnectionState.failed or RTCPeerConnectionState.disconnected or RTCPeerConnectionState.closed;
    internal bool ControlConnected => !_disposed && _control?.readyState == RTCDataChannelState.open;
    internal Guid SessionId => _session;
    internal bool ViewerWantsMedia => _viewerWatching;
    public bool IsSender => _isSender;
    public event Action<string>? StatusChanged;
    public event Action<VideoFrame>? FrameReceived;
    public event Action<ushort, byte[]>? AudioReceived;
    public event Action? KeyFrameRequested;
    internal event Action? ReconnectRequested;
    public event Action? RemotePaused;
    public event Action? RemoteResumed;
    public event Action? ConnectionLost;
    public event Action<string>? Error;
    public Task Ready => _ready.Task;

    public PeerSession(bool sender, bool useStun = true)
    {
        _isSender = sender;
        _videoReceiver = new H264VideoReceiver(_codec,
            frame => { Interlocked.Increment(ref _decodedFrames); FrameReceived?.Invoke(frame); },
            () => { ThreadPool.QueueUserWorkItem(_ => RequestRemoteKeyFrame()); });
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
        _peer.addTrack(new MediaStreamTrack(new List<AudioFormat> {
            new(111, "opus", 48000, 2, "minptime=10;useinbandfec=1;stereo=1;sprop-stereo=1")
        }, sender ? MediaStreamStatusEnum.SendOnly : MediaStreamStatusEnum.RecvOnly));
        _peer.OnAudioFormatsNegotiated += negotiated => _audioNegotiated = negotiated.Any(format => format.FormatName.Equals("opus", StringComparison.OrdinalIgnoreCase));
        _peer.OnRtpPacketReceived += (_, media, packet) =>
        {
            if (!_disposed && !_isSender && _watching && _remoteSending && media == SDPMediaTypesEnum.audio && packet.Header.PayloadType == 111)
                AudioReceived?.Invoke(packet.Header.SequenceNumber, packet.Payload);
            if (!_disposed && !_isSender && _watching && _remoteSending && media == SDPMediaTypesEnum.video && packet.Header.PayloadType == 96)
                ReceiveVideo(packet);
        };
        _peer.GetRtpChannel().OnStunMessageReceived += (_, _, _) => Interlocked.Increment(ref _stunMessages);
        if (!sender) _recoveryTimer = new Timer(_ => {
            lock (_codecGate) if (!_disposed && _watching && _remoteSending) _videoReceiver.Expire();
        }, null, 100, 100);
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
                    _remoteSending = false;
                    StatusChanged?.Invoke("Não foi possível conectar. Confira a troca de convite/resposta e copie o diagnóstico para investigar a rede.");
                    ConnectionLost?.Invoke(); break;
                case RTCPeerConnectionState.disconnected:
                case RTCPeerConnectionState.closed:
                    _remoteSending = false;
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
        void AnnounceState()
        {
            lock (_sendGate)
                if (!_disposed) channel.send(_isSender ? _canSend ? "started" : "paused" : _watching ? "watch:on" : "watch:off");
        }
        channel.onopen += AnnounceState;
        channel.onmessage += (_, protocol, bytes) =>
        {
            if (_disposed || bytes.Length > 256) return;
            var message = Encoding.UTF8.GetString(bytes);
            if (message == "paused" && !_isSender) { _remoteSending = false; lock (_codecGate) if (!_disposed) _videoReceiver.Reset(); RemotePaused?.Invoke(); }
            if (message == "started" && !_isSender) {
                bool wasSending = _remoteSending;
                _remoteSending = true;
                if (!wasSending) lock (_codecGate) if (!_disposed) { _videoReceiver.Reset(); _videoReceiver.Expire(); }
                if (!wasSending) RemoteResumed?.Invoke();
            }
            if (message == "keyframe" && _isSender) {
                bool accepted = false;
                lock (_sendGate) {
                    long now = Stopwatch.GetTimestamp();
                    if (_canSend && (_lastKeyFrameRequest == 0 || Stopwatch.GetElapsedTime(_lastKeyFrameRequest, now).TotalMilliseconds >= 500)) {
                        _lastKeyFrameRequest = now;
                        lock (_codecGate) _codec.ForceKeyFrame();
                        accepted = true;
                    }
                }
                if (accepted) KeyFrameRequested?.Invoke();
            }
            if (message == "reconnect" && _isSender) ThreadPool.QueueUserWorkItem(_ => { if (!_disposed) ReconnectRequested?.Invoke(); });
            if (_isSender && message is "watch:on" or "watch:off")
            {
                bool resumed;
                lock (_sendGate)
                {
                    bool enabled = message == "watch:on";
                    resumed = enabled && !_viewerWatching;
                    _viewerWatching = enabled;
                    if (resumed) { _lastPacketAt = 0; lock (_codecGate) _codec.ForceKeyFrame(); }
                }
                if (resumed) KeyFrameRequested?.Invoke();
            }
        };
        // SIPSorcery can deliver ondatachannel after the channel is already open.
        // Subscribe first, then also announce immediately so saved "closed" state is not lost on reconnect.
        if (channel.readyState == RTCDataChannelState.open) AnnounceState();
    }

    internal bool RequestReconnect()
    {
        if (_disposed || _isSender || _control?.readyState != RTCDataChannelState.open) return false;
        try { _control.send("reconnect"); return true; }
        catch (Exception ex) when (ex is not OutOfMemoryException) { return false; }
    }

    internal void SetWatching(bool watching)
    {
        if (_isSender) return;
        lock (_codecGate)
        {
            if (_disposed) return;
            bool changed = _watching != watching;
            _watching = watching;
            if (changed) _videoReceiver.Reset();
        }
        if (_control?.readyState == RTCDataChannelState.open)
            try { _control.send(watching ? "watch:on" : "watch:off"); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { /* State is repeated on the next connection. */ }
        if (watching) RequestRemoteKeyFrame();
    }

    private void ReceiveVideo(RTPPacket packet)
    {
        if (packet.Header.MarkerBit != 0) Interlocked.Increment(ref _networkFrames);
        lock (_codecGate) {
            if (_disposed) return;
            _videoReceiver.Push(packet.Header.SyncSource, packet.Header.SequenceNumber, packet.Header.Timestamp, packet.Header.MarkerBit != 0, packet.Payload);
        }
    }
    private void RequestRemoteKeyFrame()
    {
        if (_disposed || _isSender || !_remoteSending || _control?.readyState != RTCDataChannelState.open) return;
        try { _control.send("keyframe"); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { /* next retry or periodic IDR recovers */ }
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
            if (!_canSend || !_viewerWatching || !IsConnected) return;
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

    // RoomSession encodes once and fans the same H.264 packet out to every viewer.
    public void SendEncodedFrame(byte[] encoded, CaptureProfile profile)
    {
        lock (_sendGate)
        {
            if (!_canSend || !_viewerWatching || !IsConnected) return;
            long now = Stopwatch.GetTimestamp();
            // A room uses this fanout path, rather than SendFrame. Reaffirm the state here too.
            if (_control?.readyState == RTCDataChannelState.open &&
                (_lastSharingStateAt == 0 || Stopwatch.GetElapsedTime(_lastSharingStateAt, now).TotalSeconds >= 1))
            {
                _control.send("started");
                _lastSharingStateAt = now;
            }
            uint duration = _lastPacketAt == 0 ? (uint)(90000 / profile.FramesPerSecond) :
                (uint)Math.Clamp(Stopwatch.GetElapsedTime(_lastPacketAt, now).TotalSeconds * 90000, 1, 450000);
            _lastPacketAt = now;
            _peer.SendVideo(duration, encoded);
            Interlocked.Increment(ref _sentFrames);
        }
    }

    public void SendAudioEncoded(byte[] packet)
    {
        lock (_sendGate)
            if (_canSend && _viewerWatching && IsConnected && _audioNegotiated) _peer.SendAudio(960, packet);
    }

    public void Dispose()
    {
        lock (_sendGate)
        {
            if (_disposed) return;
            _canSend = false;
            _disposed = true;
            _recoveryTimer?.Dispose();
            _peer.Close("Icatya encerrado");
            _peer.Dispose();
            lock (_codecGate) { _videoReceiver.Reset(); _codec.Dispose(); }
        }
    }
}
