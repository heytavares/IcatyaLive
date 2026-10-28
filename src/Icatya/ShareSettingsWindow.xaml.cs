using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Icatya.Models;
using Forms = System.Windows.Forms;

namespace Icatya;

public partial class ShareSettingsWindow : Window
{
    private readonly Forms.Screen[] _screens = Forms.Screen.AllScreens;
    public string SelectedDisplayName => _screens[Displays.SelectedIndex].DeviceName;
    public CaptureProfile SelectedProfile => CaptureProfile.Available.First(p =>
        p.Height == (Resolution.SelectedIndex == 1 ? 1080 : 720) &&
        p.FramesPerSecond == (FrameRate.SelectedIndex == 1 ? 60 : 30));

    public ShareSettingsWindow(string displayName, CaptureProfile profile, bool capturing, bool connected)
    {
        InitializeComponent();
        SourceInitialized += (_, _) => Services.Windowing.DarkTitleBar.Attach(this);
        foreach (var screen in _screens)
            Displays.Items.Add($"{screen.DeviceName} · {screen.Bounds.Width} × {screen.Bounds.Height}");
        Displays.SelectedIndex = Math.Max(0, Array.FindIndex(_screens, s => s.DeviceName == displayName));
        Resolution.ItemsSource = new[] { "720p", "1080p" };
        FrameRate.ItemsSource = new[] { "30 fps", "60 fps" };
        Resolution.SelectedIndex = profile.Height == 1080 ? 1 : 0;
        FrameRate.SelectedIndex = profile.FramesPerSecond == 60 ? 1 : 0;
        ConfirmButton.Content = capturing ? "Aplicar alterações" : connected ? "Iniciar transmissão" : "Iniciar prévia";
        ConfirmButton.IsEnabled = _screens.Length > 0;
    }

    private void SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Summary == null || Resolution.SelectedIndex < 0 || FrameRate.SelectedIndex < 0) return;
        Summary.Text = $"{SelectedProfile.Width} × {SelectedProfile.Height} · {SelectedProfile.FramesPerSecond} fps · {SelectedProfile.Bitrate / 1_000_000} Mbps";
    }
    private void Confirm(object sender, RoutedEventArgs e)
    {
        if (Displays.SelectedIndex >= 0) DialogResult = true;
    }
}
