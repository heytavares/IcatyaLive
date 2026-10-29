using System;
using System.Threading.Tasks;
using System.Windows;
using Icatya.Services.Audio;
using Icatya.Services.Video;

namespace Icatya;

public partial class MainWindow
{
    private readonly AudioPlayback _audioPlayback = new();
    private AudioCaptureMixer? _audioCapture;
    private AudioCaptureSettings _selectedAudio = AudioCaptureSettings.Silent;
    private ShareSource? _selectedSource;
    private double _unmutedVolume = 65;
    private void InitializeAudio()
    {
        _audioPlayback.Warning += message => OnUi(() => SetStatus(message));
        PlaybackVolume.Value = _preferences.Load().PlaybackVolume * 100;
        _audioPlayback.Volume = (float)PlaybackVolume.Value / 100;
    }
    private void PlaybackVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_audioPlayback == null || PlaybackMute == null) return;
        _audioPlayback.Volume = (float)e.NewValue / 100;
        PlaybackMutedMark.Visibility = e.NewValue == 0 ? Visibility.Visible : Visibility.Collapsed;
        PlaybackVolume.ToolTip = $"Volume recebido: {e.NewValue:0}%";
        if (e.NewValue > 0) _unmutedVolume = e.NewValue;
        if (IsLoaded) SaveSidebarLayout();
    }
    private void MutePlayback(object sender, RoutedEventArgs e) => PlaybackVolume.Value = PlaybackVolume.Value > 0 ? 0 : _unmutedVolume;
    private void RefreshAudioStatus()
    {
        var capture=_audioCapture;
        AudioState.Text=capture==null?"Áudio da live desligado":$"Áudio · {_selectedAudio.Sources.Count} aplicativo(s) selecionado(s)";
        AudioState.ToolTip=capture==null?null:string.Join(", ",System.Linq.Enumerable.Select(_selectedAudio.Sources,s=>s.Application.Name));
        AudioLevelTrack.Visibility=capture==null?Visibility.Collapsed:Visibility.Visible;
        AudioLevel.Width=Math.Clamp((capture?.Peak??0)*60,0,60);
    }
    private void StartAudio()
    {
        if (!_selectedAudio.Enabled || _selectedAudio.Sources.Count == 0 || _peer == null && _room == null) return;
        var capture = new AudioCaptureMixer();
        _audioCapture = capture;
        var peer = _peer; var room = _room;
        capture.Warning += message => OnUi(() => { if (_audioCapture == capture) SetStatus(message); });
        capture.PacketReady += packet => {
            if (_audioCapture != capture || _captureCancellation?.IsCancellationRequested != false) return;
            peer?.SendAudioEncoded(packet); room?.SendAudioEncoded(packet);
        };
        capture.Start(_selectedAudio);
        RefreshAudioStatus();
    }
    private async Task StopAudioAsync()
    {
        var audio = _audioCapture; _audioCapture = null;
        RefreshAudioStatus();
        if (audio == null) return;
        try { await audio.DisposeAsync(); }
        catch (Exception ex) { if (!_closing) SetStatus("O áudio foi interrompido: " + ex.Message); }
    }
}


