using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MoabLive.Models;
using MoabLive.Services;

internal static class CaptureBenchmarks
{
    public static async Task<int> RunAsync()
    {
        try
        {
            var screen = System.Windows.Forms.Screen.PrimaryScreen ?? throw new InvalidOperationException("Monitor não encontrado.");
            foreach (var profile in new[] { CaptureProfile.Available[0], CaptureProfile.Available[1], CaptureProfile.Available[3] })
            {
                using var sender = new PeerSession(true, false);
                using var receiver = new PeerSession(false, false);
                int captured = 0, received = 0;
                bool texture = false;
                receiver.FrameReceived += _ => Interlocked.Increment(ref received);
                string? error = null;
                sender.Error += message => error = message;
                var offer = await sender.CreateOfferAsync();
                sender.ApplyAnswer(await receiver.AcceptOfferAsync(offer));
                await Task.WhenAll(sender.Ready, receiver.Ready).WaitAsync(TimeSpan.FromSeconds(20));
                sender.ConfigureVideo(profile);
                sender.SetSending(true);
                using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var process = Process.GetCurrentProcess();
                var cpuBefore = process.TotalProcessorTime;
                long allocationBefore = GC.GetTotalAllocatedBytes(true);
                var watch = Stopwatch.StartNew();
                try
                {
                    await new ScreenCaptureService().RunAsync(screen.Bounds, profile, frame =>
                    {
                        if (captured == 0)
                        {
                            byte first = frame.Pixels[0];
                            for (int i = 0; i < frame.Stride * frame.Height; i += 1024)
                                if (Math.Abs(frame.Pixels[i] - first) > 20) { texture = true; break; }
                        }
                        captured++;
                        sender.SendFrame(frame);
                    }, cancel.Token);
                }
                catch (OperationCanceledException) { }
                watch.Stop();
                sender.SetSending(false);
                await Task.Delay(200);
                process.Refresh();
                double cpu = (process.TotalProcessorTime - cpuBefore).TotalSeconds / watch.Elapsed.TotalSeconds * 100 / Environment.ProcessorCount;
                double allocated = (GC.GetTotalAllocatedBytes(true) - allocationBefore) / 1048576.0 / watch.Elapsed.TotalSeconds;
                Console.WriteLine($"CAPTURE {profile.Width}x{profile.Height}@{profile.FramesPerSecond}: {captured / watch.Elapsed.TotalSeconds:F1} fps capture+encode, {received} frames received, {sender.EncodingDescription}, CPU normalized {cpu:F1}%, managed allocations {allocated:F1} MiB/s, textured desktop={texture}");
                if (error != null) throw new Exception(error);
                if (received < 5 || captured < 5) throw new Exception("O teste de captura não recebeu quadros suficientes.");
                if (!texture) Console.WriteLine("NOTE: área de trabalho sem variação detectada; este teste não comprova desempenho com conteúdo real em movimento.");
            }
            Console.WriteLine("CAPTURE CHECKS PASSED. WPF rendering and gaming workloads not included.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}

