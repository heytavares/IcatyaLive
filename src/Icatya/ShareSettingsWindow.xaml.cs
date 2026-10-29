using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Icatya.Controls;
using Icatya.Models;
using Icatya.Services.Audio;
using Icatya.Services.Video;

namespace Icatya;

public partial class ShareSettingsWindow : Window
{
    private sealed record AudioRow(AudioApplication App, CheckBox Selected, Slider Volume);
    private readonly List<AudioRow> _audioRows = new();
    private readonly List<(ShareSource Source, Border Card)> _cards = new();
    private readonly string _confirmLabel;
    private bool _screens, _settings;
    public ShareSource? SelectedSource { get; private set; }
    public AudioCaptureSettings SelectedAudio => new(AudioEnabled.IsChecked == true, (float)MasterVolume.Value / 100,
        _audioRows.Where(r => r.Selected.IsChecked == true).Select(r => new AudioSourceSelection(r.App, (float)r.Volume.Value / 100)).ToArray());
    public CaptureProfile SelectedProfile => CaptureProfile.Available.First(p => p.Height == (Resolution.SelectedIndex == 1 ? 1080 : 720) && p.FramesPerSecond == (FrameRate.SelectedIndex == 1 ? 60 : 30));
    public ShareSettingsWindow(ShareSource? source, CaptureProfile profile, AudioCaptureSettings audio, bool capturing, bool connected)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Services.Windowing.DarkTitleBar.Attach(this);
        SelectedSource = source;
        _screens = source?.IsWindow == false;
        Resolution.ItemsSource = new[] { "720p", "1080p" }; FrameRate.ItemsSource = new[] { "30 fps", "60 fps — experimental" };
        Resolution.SelectedIndex = profile.Height == 1080 ? 1 : 0; FrameRate.SelectedIndex = profile.FramesPerSecond == 60 ? 1 : 0;
        _confirmLabel = capturing ? "Aplicar e reiniciar transmissão" : connected ? "Iniciar transmissão" : "Iniciar prévia";
        PopulateAudio(audio);
        AudioEnabled.IsEnabled = ProcessLoopbackCapture.Supported;
        AudioEnabled.IsChecked = audio.Enabled && ProcessLoopbackCapture.Supported;
        if (!ProcessLoopbackCapture.Supported) AudioHint.Text = "A captura de áudio por aplicativo exige Windows 11 (build 20348 ou posterior). O vídeo continua disponível.";
        MasterVolume.Value = audio.MasterVolume * 100;
        Loaded += (_, _) => PopulateSources();
    }
    private void PopulateSources()
    {
        SourceCards.Children.Clear(); _cards.Clear();
        ApplicationsTab.Style = (Style)FindResource(_screens ? (object)"Secondary" : typeof(Button));
        ScreensTab.Style = (Style)FindResource(_screens ? (object)typeof(Button) : "Secondary");
        foreach (var source in _screens ? SourceCatalog.Screens() : SourceCatalog.Windows()) {
            var content = new StackPanel();
            if (source.IsWindow) content.Children.Add(new WindowThumbnail(source.Window) { Width=300,Height=160,Margin=new Thickness(4) });
            else content.Children.Add(new TextBlock { Text="▣",FontSize=72,Height=168,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=(Brush)FindResource("SecondaryText") });
            content.Children.Add(new TextBlock { Text=source.Name,TextTrimming=TextTrimming.CharacterEllipsis,TextWrapping=TextWrapping.NoWrap,Margin=new Thickness(10,8,10,12),ToolTip=source.Name });
            var card = new Border { Child=content,Width=316,Margin=new Thickness(0,0,12,12),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(2),Background=(Brush)FindResource("PanelBackground"),Focusable=true,Cursor=System.Windows.Input.Cursors.Hand };
            card.MouseLeftButtonDown += (_, _) => Choose(source);
            card.KeyDown += (_, e) => { if (e.Key is System.Windows.Input.Key.Enter or System.Windows.Input.Key.Space) { Choose(source); e.Handled=true; } };
            System.Windows.Automation.AutomationProperties.SetName(card,source.Name);
            _cards.Add((source,card)); SourceCards.Children.Add(card);
        }
        if (SelectedSource != null && _cards.Any(c => c.Source == SelectedSource)) Choose(SelectedSource);
        else { SelectedSource=null;ConfirmButton.IsEnabled=false; }
        SourceHint.Text = _cards.Count==0 ? "Nenhuma fonte disponível. Abra um aplicativo e atualize a lista." : _screens ? "A tela inteira inclui tudo o que aparecer nesse monitor." : "Uma janela minimizada pode pausar a imagem. Conteúdo protegido pode aparecer preto.";
    }
    private void Choose(ShareSource source)
    {
        SelectedSource=source;ConfirmButton.IsEnabled=true;
        foreach(var item in _cards) item.Card.BorderBrush=(Brush)FindResource(item.Source==source ? "AccentBrush" : "BorderBrush");
    }
    private void ShowApplications(object sender,RoutedEventArgs e) { _screens=false;PopulateSources(); }
    private void ShowScreens(object sender,RoutedEventArgs e) { _screens=true;PopulateSources(); }
    private void RefreshSources(object sender,RoutedEventArgs e) => PopulateSources();
    private void SelectionChanged(object sender,SelectionChangedEventArgs e) {
        if(Summary==null || Resolution.SelectedIndex<0 || FrameRate.SelectedIndex<0) return;
        Summary.Text=$"{SelectedProfile.Width} × {SelectedProfile.Height} · {SelectedProfile.FramesPerSecond} fps · {SelectedProfile.Bitrate/1_000_000} Mbps";
    }
    private void AudioToggled(object sender,RoutedEventArgs e) { if(Mixer==null)return;Mixer.IsEnabled=AudioEnabled.IsChecked==true;Mixer.Opacity=Mixer.IsEnabled?1:0.45; }
    private void MasterChanged(object sender,RoutedPropertyChangedEventArgs<double> e) { if(MasterPercent!=null)MasterPercent.Text=$"{e.NewValue:0}%"; }
    private void RefreshAudio(object sender,RoutedEventArgs e) => PopulateAudio(SelectedAudio);
    private void PopulateAudio(AudioCaptureSettings settings)
    {
        AudioRows.Children.Clear();_audioRows.Clear();
        foreach(var app in SourceCatalog.AudioApplications()) {
            var old=settings.Sources.FirstOrDefault(s=>s.Application.ProcessId==app.ProcessId && s.Application.StartTimeTicks==app.StartTimeTicks);
            var grid=new Grid { Margin=new Thickness(0,4,0,4) };
            grid.ColumnDefinitions.Add(new(){Width=new GridLength(1,GridUnitType.Star)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(48)});grid.ColumnDefinitions.Add(new(){Width=new GridLength(150)});
            var caption=new StackPanel { Orientation=Orientation.Horizontal };
            caption.Children.Add(new Image { Source=Services.ApplicationIcon.Load(app.ProcessId), Width=22, Height=22, Margin=new Thickness(0,0,9,0) });
            caption.Children.Add(new TextBlock { Text=app.Name, TextTrimming=TextTrimming.CharacterEllipsis, MaxWidth=230, VerticalAlignment=VerticalAlignment.Center, TextWrapping=TextWrapping.NoWrap });
            var check=new CheckBox{Content=caption,IsChecked=old!=null,VerticalAlignment=VerticalAlignment.Center,ToolTip="Compartilhar somente o som de "+app.Name+" e de seus processos filhos"};
            var percent=new TextBlock{Text=$"{(old?.Volume??1)*100:0}%",VerticalAlignment=VerticalAlignment.Center};
            var volume=new Slider{Style=(Style)FindResource("AudioSlider"),Minimum=0,Maximum=100,Value=(old?.Volume??1)*100,IsEnabled=old!=null};
            System.Windows.Automation.AutomationProperties.SetName(volume,"Volume de "+app.Name);
            volume.ValueChanged+=(_,e)=>percent.Text=$"{e.NewValue:0}%";
            check.Checked+=(_,_)=>volume.IsEnabled=true;check.Unchecked+=(_,_)=>volume.IsEnabled=false;
            Grid.SetColumn(percent,1);Grid.SetColumn(volume,2);grid.Children.Add(check);grid.Children.Add(percent);grid.Children.Add(volume);
            AudioRows.Children.Add(grid);_audioRows.Add(new(app,check,volume));
        }
        if(_audioRows.Count==0)AudioRows.Children.Add(new TextBlock{Text="Nenhum aplicativo disponível. Abra seu jogo ou player e atualize.",TextWrapping=TextWrapping.Wrap});
    }
    private void Back(object sender,RoutedEventArgs e) {
        _settings=false; SettingsStep.Visibility=Visibility.Collapsed;SourceStep.Visibility=Visibility.Visible;BackButton.Visibility=Visibility.Collapsed;
        ConfirmButton.Content="Próximo";Heading.Text="O que vamos compartilhar?";Subtitle.Text="Escolha um aplicativo ou uma tela inteira.";PopulateSources();
    }
    private void Confirm(object sender,RoutedEventArgs e) {
        if(SelectedSource==null)return;
        if(!_settings) {
            // Unload thumbnails before showing controls on the second page.
            SourceCards.Children.Clear();SourceStep.Visibility=Visibility.Collapsed;SettingsStep.Visibility=Visibility.Visible;BackButton.Visibility=Visibility.Visible;
            _settings=true;SelectedName.Text=SelectedSource.Name;Heading.Text="Qualidade e som";Subtitle.Text="Ajuste a transmissão antes de começar.";ConfirmButton.Content=_confirmLabel;
            return;
        }
        try { if(SelectedAudio.Enabled && SourceCatalog.OverlappingAudioTrees(SelectedAudio.Sources.Select(s=>s.Application).ToArray())) throw new InvalidOperationException("Escolha apenas o aplicativo principal ou seus processos filhos, para não duplicar o áudio."); SelectedSource.Validate(); foreach(var s in SelectedAudio.Sources) { using var p=Process.GetProcessById(s.Application.ProcessId);ProcessLoopbackCapture.ValidateProcess(s.Application,p); } DialogResult=true; }
        catch(Exception ex) { AudioHint.Text=ex.Message+" Atualize a lista e selecione novamente."; }
    }
}
