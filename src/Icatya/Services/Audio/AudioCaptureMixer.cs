using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Icatya.Services.Audio;

public sealed class AudioCaptureMixer : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task? _running;
    public event Action<byte[]>? PacketReady;
    public event Action<string>? Warning;
    public float Peak { get; private set; }
    public void Start(AudioCaptureSettings settings)
    {
        if (_running != null) throw new InvalidOperationException("O áudio já foi iniciado.");
        _running = settings.Enabled && settings.Sources.Count > 0 ? RunAsync(settings) : Task.CompletedTask;
    }
    public static void Mix(ReadOnlySpan<float> source, Span<float> target, float gain)
    {
        gain = float.IsFinite(gain) ? Math.Clamp(gain, 0, 1) : 0;
        for (int i = 0; i < Math.Min(source.Length, target.Length); i++) target[i] += source[i] * gain;
    }
    private async Task RunAsync(AudioCaptureSettings settings)
    {
        using var codec = new OpusAudioEncoder();
        var routes = settings.Sources.Select(source => new AudioCaptureRoute(source)).ToArray();
        var tasks = routes.Select(route => ObserveSourceAsync(route)).ToArray();
        var mixed = new float[1920];
        var scratch = new float[1920];
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(20));
        try
        {
            while (await timer.WaitForNextTickAsync(_stop.Token))
            {
                Array.Clear(mixed);
                foreach (var route in routes)
                {
                    route.Buffer.Read(scratch);
                    Mix(scratch, mixed, route.Selection.Volume);
                }
                float peak = 0;
                for (int i = 0; i < mixed.Length; i++)
                {
                    mixed[i] = Math.Clamp(mixed[i] * Math.Clamp(settings.MasterVolume, 0, 1), -1, 1);
                    peak = Math.Max(peak, Math.Abs(mixed[i]));
                }
                Peak = peak;
                if (_stop.IsCancellationRequested) break;
                PacketReady?.Invoke(codec.Encode(mixed));
                if (tasks.All(task => task.IsCompleted)) break;
            }
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        finally { _stop.Cancel(); await Task.WhenAll(tasks); Peak = 0; }
    }
    private async Task ObserveSourceAsync(AudioCaptureRoute route)
    {
        try { await ProcessLoopbackCapture.RunAsync(route, _stop.Token); }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Warning?.Invoke("Áudio de " + route.Selection.Application.Name + " indisponível: " + ex.Message + ". Essa fonte não será enviada."); }
    }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_running != null) await _running;
        _stop.Dispose();
    }
}
