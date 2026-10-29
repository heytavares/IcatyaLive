using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Icatya.Controls;

internal sealed class RoomParticipantCard
{
    private readonly TextBlock _name = new() { FontSize = 12, Foreground = Brushes.White };
    private readonly AvatarView _portrait = new() { Width = 64, Height = 64 };
    public Border View { get; }
    public RoomParticipantCard(string nick, string avatar = Services.AvatarCatalog.Default)
    {
        var colors = new[] { "#202932", "#2B2636", "#232A39", "#2B2540", "#302C2B", "#203236" };
        uint hash = 2166136261;
        foreach (char c in nick) hash = (hash ^ c) * 16777619;
        var background = (Brush)new BrushConverter().ConvertFromString(colors[hash % (uint)colors.Length])!;
        var grid = new Grid();
        _portrait.Avatar = avatar;
        // Scale the avatar down with the card when it becomes a thumbnail.
        grid.Children.Add(new Viewbox { Child = _portrait, MaxWidth = 64, MaxHeight = 64, Margin = new Thickness(8, 8, 8, 24), StretchDirection = StretchDirection.DownOnly });
        grid.Children.Add(new Border { Child = _name, Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 3, 6, 3), Margin = new Thickness(8), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Bottom });
        View = new Border { Child = grid, Background = background, CornerRadius = new CornerRadius(14), ClipToBounds = true };
        View.SizeChanged += (_, e) => View.Clip = new RectangleGeometry(new Rect(e.NewSize), 14, 14);
        Rename(nick);
    }
    public void Rename(string nick, string? avatar = null)
    {
        _name.Text = nick;
        if (avatar != null) _portrait.Avatar = avatar;
        System.Windows.Automation.AutomationProperties.SetName(View, "Participante " + nick);
    }
}
