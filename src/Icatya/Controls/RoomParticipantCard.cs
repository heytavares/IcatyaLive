using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

namespace Icatya.Controls;

internal sealed class RoomParticipantCard
{
    private readonly TextBlock _name = new() { FontSize = 12, Foreground = Brushes.White };
    private readonly AvatarView _portrait = new() { Width = 64, Height = 64 };
    private readonly AvatarView _closedPortrait = new() { Width = 112, Height = 112 };
    private readonly TextBlock _closedName = new() { FontSize = 24, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 360 };
    private readonly Grid _normal = new();
    private readonly Viewbox _closed;
    private readonly Brush _background;
    private readonly Brush _closedBackground;
    public bool IsClosedLive { get; private set; }
    public event Action? ReopenRequested;
    public Border View { get; }
    public RoomParticipantCard(string nick, string avatar = Services.AvatarCatalog.Default)
    {
        var colors = new[] { "#202932", "#2B2636", "#232A39", "#2B2540", "#302C2B", "#203236" };
        uint hash = 2166136261;
        foreach (char c in nick) hash = (hash ^ c) * 16777619;
        _background = (Brush)new BrushConverter().ConvertFromString(colors[hash % (uint)colors.Length])!;
        var backdrop = new RadialGradientBrush { Center = new Point(.5, .42), GradientOrigin = new Point(.5, .42), RadiusX = .8, RadiusY = .9 };
        backdrop.GradientStops.Add(new(Color.FromRgb(62, 53, 85), 0));
        backdrop.GradientStops.Add(new(Color.FromRgb(26, 26, 40), .55));
        backdrop.GradientStops.Add(new(Color.FromRgb(13, 15, 21), 1));
        backdrop.Freeze(); _closedBackground = backdrop;
        var grid = _normal;
        _portrait.Avatar = avatar;
        // Scale the avatar down with the card when it becomes a thumbnail.
        grid.Children.Add(new Viewbox { Child = _portrait, MaxWidth = 64, MaxHeight = 64, Margin = new Thickness(8, 8, 8, 24), StretchDirection = StretchDirection.DownOnly });
        grid.Children.Add(new Border { Child = _name, Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom });
        var portrait = new Grid { Width = 120, Height = 120, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0,0,0,18) };
        _closedPortrait.Avatar = avatar;
        portrait.Children.Add(new Border { Child = _closedPortrait, BorderBrush = new SolidColorBrush(Color.FromRgb(169,156,213)), BorderThickness = new Thickness(3), CornerRadius = new CornerRadius(60) });
        portrait.Children.Add(new System.Windows.Shapes.Ellipse { Width = 20, Height = 20, Fill = new SolidColorBrush(Color.FromRgb(230,62,76)), Stroke = Brushes.White, StrokeThickness = 2, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0,0,2,4) });
        var message = new StackPanel(); message.Children.Add(portrait); message.Children.Add(_closedName);
        message.Children.Add(new TextBlock { Text = "está em live", FontSize = 19, Foreground = Brushes.White, TextAlignment = TextAlignment.Center, Margin = new Thickness(0,6,0,0) });
        message.Children.Add(new TextBlock { Text = "Clique para assistir", FontSize = 12, Foreground = new SolidColorBrush(Color.FromRgb(187,180,208)), TextAlignment = TextAlignment.Center, Margin = new Thickness(0,18,0,0) });
        _closed = new Viewbox { Child = message, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(24), MaxWidth = 408, MaxHeight = 290, Visibility = Visibility.Collapsed };
        var content = new Grid(); content.Children.Add(_normal); content.Children.Add(_closed);
        View = new Border { Child = content, Background = _background, CornerRadius = new CornerRadius(14), ClipToBounds = true };
        View.MouseLeftButtonUp += (_,e) => { if (IsClosedLive) { ReopenRequested?.Invoke(); e.Handled = true; } };
        View.KeyDown += (_,e) => { if (IsClosedLive && e.Key is Key.Enter or Key.Space) { ReopenRequested?.Invoke(); e.Handled = true; } };
        View.SizeChanged += (_, e) => View.Clip = new RectangleGeometry(new Rect(e.NewSize), 14, 14);
        Rename(nick);
    }
    public void Rename(string nick, string? avatar = null)
    {
        _name.Text = nick;
        _closedName.Text = nick;
        if (avatar != null) _portrait.Avatar = avatar;
        if (avatar != null) _closedPortrait.Avatar = avatar;
        System.Windows.Automation.AutomationProperties.SetName(View, "Participante " + nick);
    }
    public void SetClosedLive(bool closed)
    {
        IsClosedLive = closed;
        _normal.Visibility = closed ? Visibility.Collapsed : Visibility.Visible;
        _closed.Visibility = closed ? Visibility.Visible : Visibility.Collapsed;
        View.Background = closed ? _closedBackground : _background;
        View.Focusable = closed;
        View.Cursor = closed ? Cursors.Hand : Cursors.Arrow;
        View.ToolTip = closed ? "Assistir à transmissão de " + _name.Text : null;
        System.Windows.Automation.AutomationProperties.SetName(View, closed ? _name.Text + " está em live. Clique para assistir" : "Participante " + _name.Text);
    }
}
