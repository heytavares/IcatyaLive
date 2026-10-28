using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MoabLive.Models;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using Vpx.Net;

namespace MoabLive.Services;

// Owns one peer and its codec. Encoding/decoding never runs on the WPF dispatcher.
public sealed class PeerSession : IDisposable
{
    private readonly RTCPeerConnection _peer;
    private readonly Vp8NetVideoEncoderEndPoint _codec = new();
    private readonly object _codecGate = new();
    private readonly object _sendGate = new();
    private RTCDataChannel? _control;
    private bool _disposed;
    private bool _canSend;
    private readonly bool _isSender;
    private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Guid _session;
    private int _encodedFrames;
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
        _peer.addTrack(new MediaStreamTrack(_codec.GetVideoSourceFormats(),
            sender ? MediaStreamStatusEnum.SendOnly : MediaStreamStatusEnum.RecvOnly));
        _peer.OnVideoFormatsNegotiated += formats =>
        {
            lock (_codecGate)
            {
                if (_disposed) return;
                _codec.SetVideoSourceFormat(formats.First());
                _codec.SetVideoSinkFormat(formats.First());
            }
        };
        _codec.OnVideoSourceEncodedSample += (duration, sample) =>
        {
            if (IsConnected) _peer.SendVideo(duration, sample);
        };
        _peer.OnVideoFrameReceived += (endpoint, timestamp, encoded, format) =>
        {
            if (_disposed || _isSender) return;
            try
            {
                lock (_codecGate)
                    if (!_disposed) _codec.GotVideoFrame(endpoint, timestamp, encoded, format);
            }
            catch (Exception) { StatusChanged?.Invoke("Quadro perdido. Aguardando a próxima imagem completa…"); }
        };
        _codec.OnVideoSinkDecodedSample += (pixels, width, height, stride, format) =>
        {
            if (_disposed || width is 0 or > 1920 || height is 0 or > 1080) return;
            FrameReceived?.Invoke(new VideoFrame(pixels, (int)width, (int)height, stride));
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
                    StatusChanged?.Invoke("A conexão direta falhou. A rede pode exigir um retransmissor.");
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
        if (!_isSender || invite.Kind != "answer" || invite.Session != _session)
            throw new InvalidOperationException("Esta resposta não pertence ao convite aberto nesta janela.");
        ApplyRemote(invite);
    }

    private void ApplyRemote(SessionInvite invite)
    {
        var result = _peer.setRemoteDescription(new RTCSessionDescriptionInit
        { type = invite.Kind == "offer" ? RTCSdpType.offer : RTCSdpType.answer, sdp = invite.Sdp });
        if (result != SetDescriptionResultEnum.OK)
            throw new InvalidOperationException("Não foi possível aceitar os dados da conexão: " + result);
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
            if (_control?.readyState == RTCDataChannelState.open) _control.send(_canSend ? "started" : "paused");
            if (_canSend) lock (_codecGate) _codec.ForceKeyFrame();
        }
    }

    public void SendFrame(VideoFrame frame, uint durationMilliseconds)
    {
        lock (_sendGate)
        {
            if (!_canSend || !IsConnected) return;
            try
            {
                lock (_codecGate)
                {
                    if ((_encodedFrames++ % 10) == 0) _codec.ForceKeyFrame();
                    _codec.ExternalVideoSourceRawSample(durationMilliseconds, frame.Width, frame.Height,
                        frame.Pixels, VideoPixelFormatsEnum.Bgr);
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
            _peer.Close("MoabLive encerrado");
            _peer.Dispose();
            lock (_codecGate) _codec.Dispose();
        }
    }
}


