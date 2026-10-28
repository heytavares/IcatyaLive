using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Icatya.Models;
using Icatya.Services.Video;
using Icatya.Services;

internal static class PresentationChecks
{
    public static void Run()
    {
        string preferencesFile = Path.Combine(AppContext.BaseDirectory, "checks", "icatya-ui-" + Guid.NewGuid().ToString("N"), "ui.json");
        try
        {
            var preferences = new UiPreferencesStore(preferencesFile);
            if (preferences.Load() != new UiPreferences()) throw new Exception("Preferências iniciais incorretas.");
            var expected = new UiPreferences(413.5, true);
            if (!preferences.Save(expected) || preferences.Load() != expected) throw new Exception("Não preservou largura fracionária e visibilidade.");
            File.WriteAllText(preferencesFile, "{invalid");
            if (preferences.Load() != new UiPreferences()) throw new Exception("Não recuperou preferências corrompidas.");
            File.WriteAllText(preferencesFile, "{\"SidebarWidth\":-1,\"SidebarHidden\":true}");
            if (preferences.Load() != new UiPreferences()) throw new Exception("Aceitou largura inválida.");
            Console.WriteLine("PASS preferências: largura livre, visibilidade, arquivo ausente e recuperação de corrupção");
        }
        finally
        {
            if (File.Exists(preferencesFile)) File.Delete(preferencesFile);
            if (File.Exists(preferencesFile + ".tmp")) File.Delete(preferencesFile + ".tmp");
            if (Directory.Exists(Path.GetDirectoryName(preferencesFile))) Directory.Delete(Path.GetDirectoryName(preferencesFile)!);
        }
        // Real WPF bitmap copies, including the padded-row case; no mock of the renderer.
        Exception? failure = null;
        var ui = new Thread(() =>
        {
            try
            {
                var presenter = new BitmapPresenter();
                foreach (int width in new[] { 1279, 1280, 1920, 1280 })
                {
                    using var frame = VideoFrame.Rent(width, 9);
                    Array.Fill(frame.Pixels, (byte)113, 0, frame.Stride * frame.Height);
                    if (!presenter.TryPresent(frame)) throw new Exception("Não apresentou um bitmap livre.");
                    var bitmap = presenter.Bitmap!;
                    var pixels = new byte[frame.Stride * frame.Height];
                    bitmap.CopyPixels(pixels, frame.Stride, 0);
                    if (pixels[0] != 113 || pixels[^1] != 113) throw new Exception("Cópia incompleta ou stride incorreto.");
                    if (!presenter.TryPresent(frame) || !ReferenceEquals(bitmap, presenter.Bitmap))
                        throw new Exception("Recriou o bitmap sem mudar de resolução.");
                }
                presenter.Clear();
                if (presenter.Bitmap != null) throw new Exception("Imagem mantida após limpar.");
            }
            catch (Exception ex) { failure = ex; }
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start(); ui.Join();
        if (failure != null) throw new Exception("Falha de apresentação WPF.", failure);
        Console.WriteLine("PASS apresentação WPF: pixels, linhas com padding, resolução e reutilização");

        using var cancel = new CancellationTokenSource();
        using (var pacer = new FramePacer(30, cancel.Token))
        {
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < 24; i++)
            {
                Thread.Sleep(5); // Work must not be added on top of every frame period.
                if (!pacer.WaitNext()) throw new Exception("Relógio cancelado inesperadamente.");
            }
            if (watch.Elapsed.TotalSeconds is < 0.70 or > 1.6) throw new Exception("Cadência fora da tolerância: " + watch.Elapsed);
            Thread.Sleep(100);
            pacer.WaitNext();
            watch.Restart();
            pacer.WaitNext();
            if (watch.Elapsed.TotalMilliseconds < 20) throw new Exception("Gerou captura em rajada após atraso.");
        }
        using (var pacer = new FramePacer(1, cancel.Token))
        {
            cancel.CancelAfter(20);
            var watch = Stopwatch.StartNew();
            if (pacer.WaitNext() || watch.ElapsedMilliseconds > 500) throw new Exception("Cancelamento lento do relógio.");
        }
        Console.WriteLine("PASS cadência, recuperação após atraso e cancelamento imediato da espera");
    }
}
