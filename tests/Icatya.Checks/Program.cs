using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Icatya.Models;
using Icatya.Services;

if (args.Contains("--manual-signaling")) return await ManualSignalingChecks.RunAsync();
if (args.Contains("--codec-reconfigure")) return CodecReconfigurationChecks.Run();
if (args.Contains("--capture-benchmark")) return await CaptureBenchmarks.RunAsync();
if (args.Contains("--capture-stability")) return await CaptureStabilityChecks.RunAsync();

static void Assert(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Reject(Action action, string label)
{
    try { action(); }
    catch (InvalidOperationException) { Console.WriteLine("PASS " + label); return; }
    throw new Exception("Não rejeitou: " + label);
}

try
{
    PresentationChecks.Run();
    var rented = VideoFrame.Rent(1280, 720);
    var retained = rented.Retain();
    rented.Dispose();
    Assert(retained.Pixels.Length >= 1280 * 720 * 3, "Quadro compartilhado liberado cedo demais");
    retained.Dispose();
    try { retained.Retain(); throw new Exception("Quadro já liberado foi reutilizado"); }
    catch (ObjectDisposedException) { Console.WriteLine("PASS propriedade compartilhada e liberação dos buffers"); }
    Reject(() => InviteCodec.Decode("texto qualquer"), "convite inválido");
    Reject(() => InviteCodec.Decode("MOAB1." + new string('A', 25000)), "limite de tamanho");
    Reject(() => InviteCodec.Decode(InviteCodec.Encode(new(1, "offer", Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1), "v=0\r\n"))), "convite expirado");
    for (int cycle = 1; cycle <= 4; cycle++)
    {
        using var sender = new PeerSession(true, false);
        using var receiver = new PeerSession(false, false);
        sender.StatusChanged += s => Console.WriteLine("sender: " + s);
        receiver.StatusChanged += s => Console.WriteLine("receiver: " + s);
        sender.Error += e => throw new Exception(e);
        var received = new TaskCompletionSource<VideoFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int count = 0;
        receiver.FrameReceived += frame =>
        {
            Interlocked.Increment(ref count);
            if (!received.Task.IsCompleted)
            {
                frame.Retain();
                if (!received.TrySetResult(frame)) frame.Dispose();
            }
        };
        receiver.RemotePaused += () => paused.TrySetResult();
        var offer = await sender.CreateOfferAsync();
        Assert(InviteCodec.Decode(offer).Kind == "offer", "Oferta inválida");
        var answer = await receiver.AcceptOfferAsync(offer);
        var wrong = InviteCodec.Decode(answer) with { Session = Guid.NewGuid() };
        Reject(() => sender.ApplyAnswer(InviteCodec.Encode(wrong)), "resposta de outra sessão");
        sender.ApplyAnswer(answer);
        await Task.WhenAll(sender.Ready, receiver.Ready).WaitAsync(TimeSpan.FromSeconds(20));
        Console.WriteLine($"PASS conexão ICE/DTLS local, ciclo {cycle}; convite {offer.Length} caracteres");
        var profile = cycle == 1 ? CaptureProfile.Available[0] : cycle == 2 ? CaptureProfile.Available[1] : cycle == 3 ? CaptureProfile.Available[3] : CaptureProfile.Available[0];
        sender.ConfigureVideo(profile, softwareOnly: cycle == 4);
        Console.WriteLine("ENCODER " + sender.EncodingDescription);
        int width = profile.Width;
        int height = profile.Height;
        var pixels = new byte[width * height * 3];
        for (int i = 0; i < pixels.Length; i += 3) { pixels[i] = 40; pixels[i + 1] = 120; pixels[i + 2] = 210; }
        using var sample = new VideoFrame(pixels, width, height, width * 3);
        // Explicitly verify no media is sent before the user starts sharing.
        sender.SendFrame(sample);
        await Task.Delay(200);
        Assert(count == 0, "Enviou vídeo sem iniciar");
        sender.SetSending(true);
        for (int n = 0; n < 8; n++) { sender.SendFrame(sample); await Task.Delay(120); }
        using var frame = await received.Task.WaitAsync(TimeSpan.FromSeconds(15));
        Assert(frame.Width == width && frame.Height == height, "Resolução recebida incorreta");
        int offset = frame.Stride * (height / 2) + (width / 2) * 3;
        Assert(Math.Abs(frame.Pixels[offset] - 40) < 35 && Math.Abs(frame.Pixels[offset + 1] - 120) < 35 && Math.Abs(frame.Pixels[offset + 2] - 210) < 35,
            "Pixels recebidos não correspondem ao vídeo enviado");
        Assert(count >= 3, "Recebeu poucos quadros");
        Console.WriteLine($"PASS vídeo H.264 codificado, transmitido e decodificado: {count} quadros");
        sender.SetSending(false);
        await paused.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(300);
        int before = count;
        sender.SendFrame(sample);
        await Task.Delay(300);
        Assert(count == before, "Enviou vídeo após parar");
        Console.WriteLine("PASS pausa remota e bloqueio de envio após parar");
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.RemoteResumed += () => resumed.TrySetResult();
        sender.SetSending(true);
        await resumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        for (int n = 0; n < 4; n++) { sender.SendFrame(sample); await Task.Delay(120); }
        Assert(count > before, "Não retomou o envio");
        Console.WriteLine("PASS retomada após pausa");
        sender.Dispose(); sender.Dispose();
        Assert(!sender.IsConnected, "Dispose manteve conexão");
        Console.WriteLine("PASS encerramento e descarte repetido");
    }
    using (var software = new Icatya.Services.Video.H264VideoCodec())
    {
        software.Configure(CaptureProfile.Available[0], softwareOnly: true);
        Assert(!software.HardwareAccelerated && software.EncoderName == "libopenh264", "Fallback CPU incorreto");
        Console.WriteLine("PASS caminho alternativo H.264 por CPU");
    }
    using (var discovery = new PeerSession(true, true))
    {
        var internetOffer = InviteCodec.Decode(await discovery.CreateOfferAsync());
        Console.WriteLine(internetOffer.Sdp.Contains("typ srflx") ? "PASS descoberta STUN pública" : "NOTE STUN sem endereço externo neste ambiente; só conexão local validada");
    }
    Console.WriteLine("ALL CHECKS PASSED");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }









