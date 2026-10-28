using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Icatya.Models;
using Icatya.Services;

internal static class CaptureStabilityChecks
{
    public static async Task<int> RunAsync()
    {
        try
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen ?? throw new Exception("Sem monitor.");
            // Exercise a consumer slower than capture: bounded memory, dropped raw frames,
            // timely cancellation, and cleanup even when the consumer throws.
            var capture = new ScreenCaptureService();
            using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            {
                try { await capture.RunAsync(screen.Bounds, CaptureProfile.Available[1], _ => Thread.Sleep(70), cancel.Token); }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
                if (capture.DroppedFrames == 0) throw new Exception("Não descartou quadros antigos com consumidor lento.");
                Console.WriteLine($"PASS consumidor lento sem fila acumulada: {capture.DroppedFrames} quadros substituídos");
            }
            using (var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(3)))
            {
                try
                {
                    await capture.RunAsync(screen.Bounds, CaptureProfile.Available[0], _ => throw new InvalidOperationException("consumer-test"), cancel.Token);
                    throw new Exception("Perdeu a falha do consumidor.");
                }
                catch (InvalidOperationException ex) when (ex.Message == "consumer-test") { Console.WriteLine("PASS falha do consumidor encerra a captura"); }
            }

            using var sender = new PeerSession(true, false);
            using var receiver = new PeerSession(false, false);
            int received = 0;
            receiver.FrameReceived += _ => Interlocked.Increment(ref received);
            string? error = null;
            sender.Error += message => error = message;
            sender.ApplyAnswer(await receiver.AcceptOfferAsync(await sender.CreateOfferAsync()));
            try { await Task.WhenAll(sender.Ready, receiver.Ready).WaitAsync(TimeSpan.FromSeconds(20)); }
            catch { Console.WriteLine(sender.DiagnosticSummary() + receiver.DiagnosticSummary()); throw; }
            foreach (var profile in new[] { CaptureProfile.Available[0], CaptureProfile.Available[2], CaptureProfile.Available[0] })
            {
                sender.ConfigureVideo(profile);
                sender.SetSending(true);
                int before = Volatile.Read(ref received);
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(20));
                var watch = Stopwatch.StartNew();
                try { await capture.RunAsync(screen.Bounds, profile, sender.SendFrame, cancel.Token); }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { }
                if (watch.Elapsed.TotalSeconds > 23) throw new Exception("Captura demorou a parar.");
                sender.SetSending(false);
                if (error != null) throw new Exception(error);
                int delta = Volatile.Read(ref received) - before;
                if (delta < 100) throw new Exception("Vídeo deixou de chegar durante a transmissão.");
                Console.WriteLine($"STABILITY {profile.Width}x{profile.Height}: {delta / watch.Elapsed.TotalSeconds:F1} fps recebidos em {watch.Elapsed.TotalSeconds:F1}s; frames substituídos {capture.DroppedFrames}");
            }
            if (!receiver.DiagnosticSummary().Contains("Erros de decodificação: 0")) throw new Exception(receiver.DiagnosticSummary());
            Console.WriteLine("CAPTURE STABILITY PASSED: transmissão prolongada, mudança de resolução e reinício");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
