using System;
using System.Collections.Generic;
using System.Threading;

namespace Icatya.Services.Audio;

public sealed record AudioApplication(int ProcessId, long StartTimeTicks, string Name)
{
    public override string ToString() => Name;
}
public sealed record AudioSourceSelection(AudioApplication Application, float Volume = 1);
public sealed record AudioCaptureSettings(bool Enabled, float MasterVolume, IReadOnlyList<AudioSourceSelection> Sources)
{
    public static AudioCaptureSettings Silent { get; } = new(false, 1, Array.Empty<AudioSourceSelection>());
}

internal sealed class AudioCaptureRoute(AudioSourceSelection selection)
{
    public AudioSourceSelection Selection { get; } = selection;
    public PcmBuffer Buffer { get; } = new();
    private float _peak;
    public float Peak { get => Volatile.Read(ref _peak); set => Volatile.Write(ref _peak, value); }
}
