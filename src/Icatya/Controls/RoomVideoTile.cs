using System;
using System.Threading;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using Icatya.Models;
using Icatya.Services.Video;

namespace Icatya.Controls;

// One latest frame per stream: slow rendering never accumulates video in memory.
internal sealed class RoomVideoTile : IDisposable
{
    private readonly BitmapPresenter _presenter = new();
    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly TextBlock _waiting = new() { Text = "Conectando à tela…", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 };
    private readonly TextBlock _name = new() { Margin = new Thickness(10, 7, 10, 7), FontSize = 13 };
    private readonly Border _liveBadge;
    private readonly object _gate = new();
    private readonly VideoFrameMailbox _frames = new();
    private bool _disposed;
    private bool _thumbnail;
    private long _lastPresentedAt;
    public int SourceWidth { get; private set; }
    public int SourceHeight { get; private set; }
    public string PresentationSummary()
    {
        var upload = _presenter.UploadTime.Snapshot();
        return $"Quadros exibidos: {PresentedFrames}; tentativas sem superfície livre: {BusyPresentationAttempts}; bloqueios evitados: {_presenter.AlternativeSurfaceFrames}; upload médio/máximo: {upload.AverageMs:F2}/{upload.MaximumMs:F2} ms; superfície: {_presenter.Bitmap?.PixelWidth}×{_presenter.Bitmap?.PixelHeight}; miniatura: {_thumbnail}\n";
    }
    public Border View { get; }
    public long PresentedFrames { get; private set; }
    public long BusyPresentationAttempts { get; private set; }
    public event Action? FocusRequested;
    public RoomVideoTile(string nick)
    {
        RenderOptions.SetBitmapScalingMode(_image, BitmapScalingMode.LowQuality);
        _name.Text = nick;
        var grid = new Grid();
        grid.Children.Add(_image); grid.Children.Add(_waiting);
        _name.Margin = new Thickness(0);
        _name.FontSize = 12;
        _name.Foreground = Brushes.White;
        grid.Children.Add(new Border { Child = _name, Background = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom });
        var live = new StackPanel { Orientation = Orientation.Horizontal };
        live.Children.Add(new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Fill = Brushes.White, Margin = new Thickness(0, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center });
        live.Children.Add(new TextBlock { Text = "AO VIVO", FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White });
        _liveBadge = new Border { Child = live, Background = new SolidColorBrush(Color.FromRgb(230, 62, 76)), CornerRadius = new CornerRadius(9), Padding = new Thickness(8, 5, 8, 5), Margin = new Thickness(14), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Opacity = 0, IsHitTestVisible = false };
        grid.Children.Add(_liveBadge);
        View = new Border { Child = grid, Background = new SolidColorBrush(Color.FromRgb(12, 13, 16)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(41, 44, 53)), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(14), ClipToBounds = true,
            Focusable = true, Cursor = Cursors.Hand, ToolTip = "Clique para focar esta tela" };
        View.MouseLeftButtonUp += (_, e) => { FocusRequested?.Invoke(); e.Handled = true; };
        View.KeyDown += (_, e) => { if (e.Key is Key.Enter or Key.Space) { FocusRequested?.Invoke(); e.Handled = true; } };
        View.SizeChanged += (_, e) => View.Clip = new RectangleGeometry(new Rect(e.NewSize), 14, 14);
        Rename(nick);
    }
    public void SetOverlayVisible(bool visible) => _liveBadge.Opacity = visible ? 1 : 0;
    public void Rename(string nick)
    {
        _name.Text = "▣ " + nick;
        System.Windows.Automation.AutomationProperties.SetName(View, "Focar transmissão de " + nick);
    }
    public void Queue(VideoFrame frame)
    {
        lock (_gate) { if (_disposed) return; _frames.Queue(frame); }
    }
    public void SetStatus(string text)
    {
        _waiting.Text = text;
        _waiting.Visibility = Visibility.Visible;
    }
    public void SetThumbnail(bool value)
    {
        if (_thumbnail == value) return;
        _thumbnail = value; _lastPresentedAt = 0;
    }
    public void Render()
    {
        if (!View.IsVisible) return;
        long now = Stopwatch.GetTimestamp();
        if (_thumbnail && _lastPresentedAt != 0 && Stopwatch.GetElapsedTime(_lastPresentedAt, now).TotalMilliseconds < 1000d / 12) return;
        using var frame = _frames.Take();
        if (frame == null) return;
        if (!_presenter.TryPresent(frame, _thumbnail)) { BusyPresentationAttempts++; _frames.Retry(frame); return; }
        if (_image.Source != _presenter.Bitmap) _image.Source = _presenter.Bitmap;
        SourceWidth = frame.Width; SourceHeight = frame.Height;
        _lastPresentedAt = now;
        PresentedFrames++;
        _waiting.Visibility = Visibility.Collapsed;
    }
    public void Dispose()
    {
        lock (_gate) { _disposed = true; _frames.Dispose(); }
        _image.Source = null;
        _presenter.Clear();
    }
}

