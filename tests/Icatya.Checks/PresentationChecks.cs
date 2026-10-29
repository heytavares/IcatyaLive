using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Runtime.InteropServices;
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
                    var pixels = new byte[width * 4 * frame.Height];
                    bitmap.CopyPixels(pixels, width * 4, 0);
                    for (int i = 0; i < pixels.Length; i += 4)
                        if (pixels[i] != 113 || pixels[i+1] != 113 || pixels[i+2] != 113 || pixels[i+3] != 255)
                            throw new Exception("Conversão BGR32 incompleta ou stride incorreto.");
                    if (!presenter.TryPresent(frame) || !ReferenceEquals(bitmap, presenter.Bitmap))
                        throw new Exception("Recriou o bitmap sem mudar de resolução.");
                }
                presenter.Clear();
                if (presenter.Bitmap != null) throw new Exception("Imagem mantida após limpar.");
                using var large = VideoFrame.Rent(1920,1080);
                Array.Fill(large.Pixels,(byte)91,0,large.Stride*large.Height);
                if(!presenter.TryPresent(large,true)||presenter.Bitmap!.PixelWidth!=320||presenter.Bitmap.PixelHeight!=180)
                    throw new Exception("Miniatura ainda cria superfície de tamanho completo.");
                if(!presenter.TryPresent(large,false)||presenter.Bitmap!.PixelWidth!=1920||presenter.Bitmap.PixelHeight!=1080)
                    throw new Exception("Focar uma miniatura não restaura a resolução completa.");
                CheckPixelBoundaries();
            }
            catch (Exception ex) { failure = ex; }
        });
        ui.SetApartmentState(ApartmentState.STA);
        ui.Start(); ui.Join();
        if (failure != null) throw new Exception("Falha de apresentação WPF.", failure);
        Console.WriteLine("PASS apresentação WPF BGR32: pixels, padding, SIMD/escalar, limites, miniaturas, resolução e reutilização");

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

    private static void CheckPixelBoundaries()
    {
        foreach(int width in new[]{1,2,3,4,5,6,7,8,9,13,17,31,32,33,1279})
        {
            const int height=3, guard=16;
            int sourceStride=width*3+7, destinationStride=width*4+12;
            var source=new byte[sourceStride*height];
            new Random(width).NextBytes(source);
            using var frame=new VideoFrame(source,width,height,sourceStride);
            byte[]? previous=null;
            foreach(bool vectorize in new[]{false,true})
            {
                var expected=new byte[destinationStride*height+guard*2];Array.Fill(expected,(byte)0xCC);
                IntPtr memory=Marshal.AllocHGlobal(expected.Length);
                try
                {
                    Marshal.Copy(expected,0,memory,expected.Length);
                    PresentationPixels.Copy(frame,memory+guard,destinationStride,width,height,vectorize:vectorize);
                    var result=new byte[expected.Length];Marshal.Copy(memory,result,0,result.Length);
                    for(int y=0;y<height;y++)for(int x=0;x<width;x++)
                    {
                        int input=y*sourceStride+x*3, output=guard+y*destinationStride+x*4;
                        expected[output]=source[input];expected[output+1]=source[input+1];expected[output+2]=source[input+2];expected[output+3]=255;
                    }
                    if(!result.AsSpan().SequenceEqual(expected))throw new Exception("Cor, padding ou limite de memória incorreto, largura "+width);
                    if(previous!=null&&!result.AsSpan().SequenceEqual(previous))throw new Exception("SIMD e fallback divergem.");
                    previous=result;
                }
                finally { Marshal.FreeHGlobal(memory); }
            }
        }
    }
}
