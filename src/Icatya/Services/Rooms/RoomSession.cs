using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Icatya.Models;
using Icatya.Services.Video;

namespace Icatya.Services.Rooms;

public sealed record RoomMember(string Id, string Nick, bool Sharing, string Avatar = AvatarCatalog.Default);

public sealed partial class RoomSession : IAsyncDisposable
{
    private sealed class Link(PeerSession peer, string id)
    {
        public PeerSession Peer { get; } = peer;
        public string Id { get; } = id;
        public long CreatedAt { get; } = Stopwatch.GetTimestamp();
        public long ConnectedAt, FailedAt, WaitingSince, LastFrameAt, LastRecoveryAt;
        public int Attempt;
        public bool Replacing, ReportedFailure;
    }
    private readonly object _gate = new();
    private readonly object _encoding = new();
    private readonly RoomConnection _connection = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly Dictionary<string, RoomMember> _members = new();
    private readonly Dictionary<string, Link> _outgoing = new(), _incoming = new();
    private readonly HashSet<string> _unwatched = new();
    private readonly bool _useStun;
    private readonly string _clientKey;
    private volatile string _avatar;
    private H264VideoCodec? _encoder;
    private CaptureProfile _profile = CaptureProfile.Available[0];
    private volatile bool _sharing, _disposed;
    private int _generation;
    private long _lastRecoveryKeyFrame;
    public DurationMetric EncodeTime { get; } = new();
    public DurationMetric SendTime { get; } = new();
    public string PerformanceSummary()
    {
        var encode=EncodeTime.Snapshot(); var send=SendTime.Snapshot();
        return $"Sala: {Members.Length} participantes; {ConnectedViewers} destinos conectados\nCompressão compartilhada média/máxima: {encode.AverageMs:F2}/{encode.MaximumMs:F2} ms\nEnvio aos espectadores média/máxima: {send.AverageMs:F2}/{send.MaximumMs:F2} ms\n";
    }
    public string SelfId { get; private set; } = "";
    public RoomIdentity Identity { get; }
    public string Name { get; private set; }
    public bool CanRename { get; private set; }
    public event Action? NameChanged;
    public bool Connected { get; private set; }
    public int ConnectedViewers { get { lock (_gate) return _outgoing.Values.Count(link => link.Peer.IsConnected); } }
    public string EncodingDescription { get { lock (_encoding) return _encoder == null ? "" : _encoder.EncoderName + (_encoder.HardwareAccelerated ? " · GPU" : " · CPU"); } }
    public RoomMember[] Members { get { lock (_gate) return _members.Values.ToArray(); } }
    public event Action? MembersChanged;
    public event Action<string>? StatusChanged;
    // Frames are borrowed for the callback; retain only if queued for presentation.
    public event Action<string, VideoFrame>? FrameReceived;
    public event Action<string, ushort, byte[]>? AudioReceived;
    public event Action<string>? StreamStopped;

    public RoomSession(RoomIdentity identity, bool useStun = true, string? clientKey = null, string avatar = AvatarCatalog.Default)
    {
        Identity = identity;
        Name = identity.Name;
        _useStun = useStun;
        _clientKey = clientKey ?? RoomClientIdentity.Create();
        _avatar = AvatarCatalog.Normalize(avatar);
        _connection.Message += Receive;
        _connection.Status += message => StatusChanged?.Invoke(message);
        _connection.Disconnected += ClearPeers;
        _ = MonitorLinksAsync();
    }
    public void Start(Uri service) => _connection.Start(service, Identity, _clientKey, () => _avatar);
    private static RoomMember ReadMember(JsonElement m) => new(m.GetProperty("id").GetString()!, m.GetProperty("nick").GetString()!, m.GetProperty("sharing").GetBoolean(),
        AvatarCatalog.Normalize(m.TryGetProperty("avatar", out var avatar) && avatar.ValueKind == JsonValueKind.String ? avatar.GetString() : null));
    private void Receive(JsonElement message)
    {
        if (_disposed) return;
        string? type = message.GetProperty("type").GetString();
        switch (type)
        {
            case "welcome":
                SelfId = message.GetProperty("self").GetString()!;
                Connected = true;
                CanRename = false;
                if (message.TryGetProperty("room", out var room))
                {
                    Name = RoomIdentity.NormalizeName(room.GetProperty("name").GetString());
                    CanRename = room.TryGetProperty("canRename", out var rename) && rename.GetBoolean();
                    NameChanged?.Invoke();
                }
                foreach (var m in message.GetProperty("members").EnumerateArray()) AddMember(ReadMember(m));
                _ = SendSafeAsync(new { type = "sharing", active = _sharing });
                _ = SendSafeAsync(new { type = "profile", avatar = _avatar });
                StatusChanged?.Invoke("Na sala · compartilhe o convite com seus amigos.");
                MembersChanged?.Invoke();
                break;
            case "joined": AddMember(ReadMember(message.GetProperty("member"))); break;
            case "room":
                Name = RoomIdentity.NormalizeName(message.GetProperty("name").GetString());
                NameChanged?.Invoke();
                break;
            case "error": StatusChanged?.Invoke(message.GetProperty("message").GetString() ?? "Não foi possível concluir a ação na sala."); break;
            case "left": RemoveMember(message.GetProperty("id").GetString()!); break;
            case "profile":
                var updated = ReadMember(message.GetProperty("member"));
                lock (_gate) if (_members.ContainsKey(updated.Id)) _members[updated.Id] = updated;
                MembersChanged?.Invoke();
                break;
            case "sharing":
                string id = message.GetProperty("from").GetString()!;
                bool active = message.GetProperty("active").GetBoolean();
                lock (_gate) if (_members.TryGetValue(id, out var member)) _members[id] = member with { Sharing = active };
                if (!active) StreamStopped?.Invoke(id);
                MembersChanged?.Invoke();
                break;
            case "signal": _ = HandleSignalAsync(message); break;
        }
    }
    private void AddMember(RoomMember member)
    {
        lock (_gate)
        {
            if (_disposed || !_members.TryAdd(member.Id, member)) return;
        }
        MembersChanged?.Invoke();
        if (member.Id != SelfId) _ = OfferAsync(member.Id, _generation);
    }
    private bool Current(string id, int generation) { lock (_gate) return !_disposed && generation == _generation && _members.ContainsKey(id); }
    private bool CurrentLink(Link link, int generation, bool outgoing)
    {
        lock (_gate) return Current(link.Id, generation) && (outgoing ? _outgoing : _incoming).TryGetValue(link.Id, out var current) && ReferenceEquals(current, link);
    }
    private async Task OfferAsync(string id, int generation, int attempt = 0)
    {
        Link? link = null;
        try
        {
            link = new(new PeerSession(true, _useStun), id);
            link.Attempt = attempt;
            lock (_gate)
            {
                if (!Current(id, generation) || _outgoing.ContainsKey(id)) { link.Peer.Dispose(); return; }
                _outgoing.Add(id, link);
            }
            var peer = link.Peer;
            peer.ReconnectRequested += () => RetryOutgoing(link, generation);
            peer.KeyFrameRequested += () =>
            {
                if (!CurrentLink(link, generation, true) || !_sharing) return;
                lock (_encoding)
                {
                    long now = Stopwatch.GetTimestamp();
                    if (_lastRecoveryKeyFrame != 0 && Stopwatch.GetElapsedTime(_lastRecoveryKeyFrame, now).TotalMilliseconds < 500) return;
                    _lastRecoveryKeyFrame = now;
                    _encoder?.ForceKeyFrame();
                }
            };
            peer.StatusChanged += _ =>
            {
                if (!CurrentLink(link, generation, true) || !peer.IsConnected) return;
                peer.SetSending(_sharing);
                lock (_encoding) _encoder?.ForceKeyFrame();
            };
            var offer = await peer.CreateOfferAsync(_stop.Token);
            if (CurrentLink(link, generation, true)) await _connection.SendAsync(new { type = "signal", to = id, kind = "offer", code = offer }, _stop.Token);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (link != null && CurrentLink(link, generation, true)) RetryOutgoing(link, generation); }
    }
    private async Task HandleSignalAsync(JsonElement message)
    {
        string id = message.GetProperty("from").GetString()!;
        int generation = _generation;
        if (!Current(id, generation)) return;
        Link? receiving = null;
        try
        {
            string code = message.GetProperty("code").GetString()!;
            var invite = InviteCodec.Decode(code);
            if (invite.Kind != message.GetProperty("kind").GetString()) return;
            if (message.GetProperty("kind").GetString() == "answer")
            {
                Link? sender;
                lock (_gate) _outgoing.TryGetValue(id, out sender);
                // A late answer from a replaced connection must not be applied to its successor.
                if (sender != null && sender.Peer.SessionId == invite.Session && CurrentLink(sender, generation, true)) sender.Peer.ApplyAnswer(code);
                return;
            }
            if (message.GetProperty("kind").GetString() != "offer" || invite.Kind != "offer") return;
            var receiver = new PeerSession(false, _useStun);
            receiving = new Link(receiver, id);
            Link? previous;
            lock (_gate)
            {
                if (!Current(id, generation)) { receiver.Dispose(); return; }
                if (_incoming.TryGetValue(id, out var existing) && existing.Peer.SessionId == invite.Session) { receiver.Dispose(); return; }
                _incoming.Remove(id, out previous);
                _incoming[id] = receiving;
                receiver.SetWatching(!_unwatched.Contains(id));
            }
            previous?.Peer.Dispose();
            SetStreamStatus(id, "Conectando à tela…");
            receiver.AudioReceived += (sequence, packet) =>
            {
                bool valid;
                lock (_gate) valid = !_disposed && _incoming.TryGetValue(id, out var link) && link.Peer == receiver &&
                    _members.TryGetValue(id, out var member) && member.Sharing;
                if (valid && IsWatching(id)) AudioReceived?.Invoke(id, sequence, packet);
            };
            receiver.FrameReceived += frame =>
            {
                bool valid;
                lock (_gate) valid = !_disposed && _incoming.TryGetValue(id, out var link) && link.Peer == receiver;
                if (valid && IsWatching(id)) {
                    Interlocked.Exchange(ref receiving.LastFrameAt, Stopwatch.GetTimestamp());
                    FrameReceived?.Invoke(id, frame);
                }
            };
            receiver.RemotePaused += () =>
            {
                if (!CurrentLink(receiving, generation, false)) return;
                bool sharing;
                lock (_gate) sharing = _members.TryGetValue(id, out var member) && member.Sharing;
                if (sharing) SetStreamStatus(id, "Aguardando o transmissor enviar a imagem…");
                else StreamStopped?.Invoke(id);
            };
            receiver.RemoteResumed += () => { if (CurrentLink(receiving, generation, false)) MembersChanged?.Invoke(); };
            receiver.ConnectionLost += () =>
            {
                if (!CurrentLink(receiving, generation, false)) return;
                SetStreamStatus(id, "Conexão interrompida · tentando recuperar…");
            };
            var answer = await receiver.AcceptOfferAsync(code, _stop.Token);
            if (CurrentLink(receiving, generation, false)) await _connection.SendAsync(new { type = "signal", to = id, kind = "answer", code = answer }, _stop.Token);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { if (receiving != null && CurrentLink(receiving, generation, false)) SetStreamStatus(id, "A conexão desta tela falhou · aguardando nova tentativa…"); }
    }
    private async Task SendSafeAsync(object value)
    {
        try { await _connection.SendAsync(value, _stop.Token); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { /* connection loop owns retry */ }
    }
    public bool IsWatching(string id) { lock (_gate) return !_unwatched.Contains(id); }
    public async Task RenameAsync(string name)
    {
        name = RoomIdentity.NormalizeName(name);
        if (!Connected || !CanRename) throw new InvalidOperationException("Aguarde a conexão com o serviço atualizado para renomear a sala.");
        await _connection.SendAsync(new { type = "rename", name }, _stop.Token);
    }
    public void SetWatching(string id, bool watching)
    {
        PeerSession? receiver;
        lock (_gate)
        {
            if (_disposed || !_members.ContainsKey(id)) return;
            if (watching) _unwatched.Remove(id); else _unwatched.Add(id);
            receiver = _incoming.GetValueOrDefault(id)?.Peer;
        }
        receiver?.SetWatching(watching);
    }
    public void SetAvatar(string avatar)
    {
        if (!AvatarCatalog.IsValid(avatar)) throw new ArgumentException("Avatar inválido.", nameof(avatar));
        lock (_gate)
        {
            if (_disposed) return;
            _avatar = avatar;
            if (_members.TryGetValue(SelfId, out var self)) _members[SelfId] = self with { Avatar = avatar };
        }
        if (Connected) _ = SendSafeAsync(new { type = "profile", avatar });
        MembersChanged?.Invoke();
    }
    public void ConfigureVideo(CaptureProfile profile, bool softwareOnly = false)
    {
        lock (_encoding)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _encoder ??= new();
            _encoder.Configure(profile, softwareOnly);
            _profile = profile;
        }
    }
    public void SetSharing(bool active)
    {
        Link[] peers;
        lock (_gate)
        {
            if (_disposed) return;
            _sharing = active;
            if (_members.TryGetValue(SelfId, out var self)) _members[SelfId] = self with { Sharing = active };
            peers = _outgoing.Values.ToArray();
        }
        foreach (var link in peers) link.Peer.SetSending(active);
        lock (_encoding) _encoder?.ForceKeyFrame();
        if (Connected) _ = SendSafeAsync(new { type = "sharing", active });
        MembersChanged?.Invoke();
    }
    public void SendFrame(VideoFrame frame)
    {
        Link[] peers;
        lock (_gate) { if (!_sharing || _disposed) return; peers = _outgoing.Values.Where(p => p.Peer.IsConnected && p.Peer.ViewerWantsMedia).ToArray(); }
        if (peers.Length == 0) return;
        byte[]? encoded;
        CaptureProfile profile;
        lock (_encoding) {
            long started=Stopwatch.GetTimestamp();
            encoded = _encoder?.Encode(frame); profile = _profile;
            EncodeTime.Record(started);
        }
        if (encoded == null) return;
        long sendStarted=Stopwatch.GetTimestamp();
        foreach (var link in peers)
            try { link.Peer.SendEncodedFrame(encoded, profile); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { /* one peer cannot stop the other viewers */ }
        SendTime.Record(sendStarted);
    }
    public void SendAudioEncoded(byte[] packet)
    {
        Link[] peers;
        lock (_gate) { if (!_sharing || _disposed) return; peers = _outgoing.Values.ToArray(); }
        foreach (var link in peers)
            try { link.Peer.SendAudioEncoded(packet); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { /* Other viewers continue. */ }
    }
    private void RemoveMember(string id)
    {
        Link? outgoing, incoming;
        lock (_gate) { _members.Remove(id); _unwatched.Remove(id); _streamStatuses.Remove(id); _outgoing.Remove(id, out outgoing); _incoming.Remove(id, out incoming); }
        outgoing?.Peer.Dispose(); incoming?.Peer.Dispose();
        StreamStopped?.Invoke(id);
        MembersChanged?.Invoke();
    }
    private void ClearPeers()
    {
        Link[] peers;
        string[] ids;
        lock (_gate)
        {
            _generation++;
            Connected = false;
            CanRename = false;
            ids = _members.Keys.ToArray();
            peers = _outgoing.Values.Concat(_incoming.Values).ToArray();
            _outgoing.Clear(); _incoming.Clear(); _members.Clear(); _streamStatuses.Clear(); _unwatched.Clear();
        }
        foreach (var link in peers) link.Peer.Dispose();
        foreach (string id in ids) StreamStopped?.Invoke(id);
        MembersChanged?.Invoke();
    }
    public async ValueTask DisposeAsync()
    {
        lock (_gate) { if (_disposed) return; _disposed = true; }
        _stop.Cancel();
        await _connection.DisposeAsync();
        ClearPeers();
        lock (_encoding) { _encoder?.Dispose(); _encoder = null; }
        // Pending ICE gathering observes cancellation before releasing its peer.
    }
}
