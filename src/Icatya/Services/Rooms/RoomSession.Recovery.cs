using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Icatya.Services.Rooms;

public sealed partial class RoomSession
{
    private const int MaximumRetries = 3;
    private readonly Dictionary<string, string> _streamStatuses = new();
    public event Action<string, string>? StreamStatusChanged;

    public string StreamStatus(string id)
    {
        lock (_gate) return _streamStatuses.GetValueOrDefault(id, "Conectando à tela…");
    }
    private void SetStreamStatus(string id, string status)
    {
        lock (_gate)
        {
            if (_disposed || !_members.ContainsKey(id) || _streamStatuses.GetValueOrDefault(id) == status) return;
            _streamStatuses[id] = status;
        }
        StreamStatusChanged?.Invoke(id, status);
    }
    private async Task MonitorLinksAsync()
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(_stop.Token)) CheckLinkHealth(Stopwatch.GetTimestamp());
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
    }
    // The sender owns retries for its direction. The reverse live and other friends keep their peers.
    internal void CheckLinkHealth(long now)
    {
        Link[] outgoing, incoming;
        int generation;
        lock (_gate)
        {
            if (_disposed || !Connected) return;
            generation = _generation;
            outgoing = _outgoing.Values.ToArray(); incoming = _incoming.Values.ToArray();
        }
        foreach (var link in outgoing)
        {
            bool retry;
            lock (_gate)
            {
                if (!CurrentLink(link, generation, true) || link.Replacing) continue;
                if (link.Peer.IsConnected)
                {
                    link.FailedAt = 0;
                    if (link.ConnectedAt == 0) link.ConnectedAt = now;
                    if (link.Peer.ControlConnected && Stopwatch.GetElapsedTime(link.ConnectedAt, now).TotalSeconds >= 60)
                    { link.Attempt = 0; link.ReportedFailure = false; }
                    retry = !link.Peer.ControlConnected && Stopwatch.GetElapsedTime(link.ConnectedAt, now).TotalSeconds >= 20;
                }
                else
                {
                    link.ConnectedAt = 0;
                    if (link.Peer.ConnectionFailed && link.FailedAt == 0) link.FailedAt = now;
                    retry = NeedsReconnect(Stopwatch.GetElapsedTime(link.CreatedAt, now),
                        link.FailedAt == 0 ? null : Stopwatch.GetElapsedTime(link.FailedAt, now));
                }
            }
            if (retry) RetryOutgoing(link, generation);
        }
        foreach (var link in incoming)
        {
            bool stalled;
            lock (_gate)
            {
                if (!CurrentLink(link, generation, false)) continue;
                if (!_members.TryGetValue(link.Id, out var member) || !member.Sharing || _unwatched.Contains(link.Id))
                { link.WaitingSince = 0; continue; }
                if (link.WaitingSince == 0) link.WaitingSince = now;
                long lastFrame = Math.Max(link.WaitingSince, Interlocked.Read(ref link.LastFrameAt));
                stalled = Stopwatch.GetElapsedTime(lastFrame, now).TotalSeconds >= 20 &&
                    (link.LastRecoveryAt == 0 || Stopwatch.GetElapsedTime(link.LastRecoveryAt, now).TotalSeconds >= 20);
                if (stalled) link.LastRecoveryAt = now;
            }
            if (!stalled) continue;
            bool requested = link.Peer.RequestReconnect();
            SetStreamStatus(link.Id, requested ? "Sem imagem · tentando recuperar esta live…" :
                Stopwatch.GetElapsedTime(link.CreatedAt, now).TotalSeconds >= 45 ?
                "Não foi possível conectar esta live. Copie o diagnóstico nas configurações." :
                "Esta live não conectou · verificando a conexão direta…");
        }
    }
    internal static bool NeedsReconnect(TimeSpan connectingFor, TimeSpan? failedFor) =>
        connectingFor >= TimeSpan.FromSeconds(45) || failedFor >= TimeSpan.FromSeconds(3);

    private void RetryOutgoing(Link link, int generation)
    {
        bool exhausted;
        lock (_gate)
        {
            if (!CurrentLink(link, generation, true) || link.Replacing || link.ReportedFailure) return;
            exhausted = link.Attempt >= MaximumRetries;
            if (exhausted) link.ReportedFailure = true;
            else link.Replacing = true;
        }
        if (exhausted)
        {
            StatusChanged?.Invoke("Uma conexão de vídeo falhou após 3 novas tentativas. A sala continua ativa; copie o diagnóstico nas configurações para verificar a rede.");
            return;
        }
        StatusChanged?.Invoke($"Recuperando uma conexão de vídeo · tentativa {link.Attempt + 1}/{MaximumRetries}…");
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, link.Attempt)), _stop.Token);
                lock (_gate)
                {
                    if (!CurrentLink(link, generation, true)) return;
                    _outgoing.Remove(link.Id);
                }
                link.Peer.Dispose();
                await OfferAsync(link.Id, generation, link.Attempt + 1);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        });
    }

    public string DiagnosticSummary()
    {
        (string Direction, Link Link)[] links;
        lock (_gate) links = _outgoing.Values.Select(link => ("Envio", link))
            .Concat(_incoming.Values.Select(link => ("Recepção", link))).ToArray();
        var text = new StringBuilder($"Sala conectada: {Connected}; conexões de vídeo: {links.Length}\n");
        foreach (var (direction, link) in links)
        {
            text.AppendLine($"{direction} · participante {link.Id[..Math.Min(8, link.Id.Length)]} · tentativa {link.Attempt + 1}");
            try { text.AppendLine(link.Peer.DiagnosticSummary()); }
            catch (Exception ex) when (ex is ObjectDisposedException or InvalidOperationException) { text.AppendLine("Conexão substituída durante a coleta."); }
        }
        return text.ToString();
    }
}
