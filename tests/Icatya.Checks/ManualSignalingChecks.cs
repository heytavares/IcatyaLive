using System;
using System.Threading.Tasks;
using Icatya.Services;
using Icatya.Models;

internal static class ManualSignalingChecks
{
    public static async Task<int> RunAsync()
    {
        try
        {
            using var sender = new PeerSession(true, false);
            using var receiver = new PeerSession(false, false);
            sender.StatusChanged += value => Console.WriteLine("sender: " + value);
            receiver.StatusChanged += value => Console.WriteLine("receiver: " + value);
            var received = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            receiver.FrameReceived += frame => received.TrySetResult(frame.Width);
            int connectionLosses = 0;
            receiver.ConnectionLost += () => System.Threading.Interlocked.Increment(ref connectionLosses);
            string offer = await sender.CreateOfferAsync();
            string answer = await receiver.AcceptOfferAsync(offer);
            Console.WriteLine("Resposta gerada. Simulando 25 segundos para copiar pelo chat.");
            await Task.Delay(TimeSpan.FromSeconds(25));
            Console.WriteLine("ANTES DA RESPOSTA\n" + sender.DiagnosticSummary() + receiver.DiagnosticSummary());
            if (connectionLosses != 0) throw new Exception("Houve falha prematura enquanto a resposta era copiada.");
            sender.ApplyAnswer(answer);
            try { await Task.WhenAll(sender.Ready, receiver.Ready).WaitAsync(TimeSpan.FromSeconds(12)); }
            catch { Console.WriteLine("APÓS TENTATIVA\n" + sender.DiagnosticSummary() + receiver.DiagnosticSummary()); throw; }
            sender.ConfigureVideo(CaptureProfile.Available[0]);
            using var image = VideoFrame.Rent(1280, 720);
            Array.Fill(image.Pixels, (byte)90, 0, image.Stride * image.Height);
            sender.SetSending(true);
            for (int i = 0; i < 10; i++) { sender.SendFrame(image); await Task.Delay(40); }
            if (await received.Task.WaitAsync(TimeSpan.FromSeconds(5)) != 1280) throw new Exception("Vídeo recebido incorreto.");
            sender.SetSending(false);
            Console.WriteLine("PASS conexão e vídeo recebido após 25 segundos de troca manual, sem falha prematura.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}



