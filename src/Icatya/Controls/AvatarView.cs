using System;
using System.Windows;
using System.Windows.Media;
using Icatya.Services;

namespace Icatya.Controls;

public sealed class AvatarView : FrameworkElement
{
    public static readonly DependencyProperty AvatarProperty = DependencyProperty.Register(nameof(Avatar), typeof(string), typeof(AvatarView),
        new FrameworkPropertyMetadata(AvatarCatalog.Default, FrameworkPropertyMetadataOptions.AffectsRender));
    public string Avatar { get => (string)GetValue(AvatarProperty); set => SetValue(AvatarProperty, value); }
    protected override void OnRender(DrawingContext drawingContext)
    {
        double radius = Math.Min(ActualWidth, ActualHeight) / 2;
        if (radius > 0) drawingContext.DrawEllipse(AvatarCatalog.GetBrush(Avatar), null, new Point(ActualWidth / 2, ActualHeight / 2), radius, radius);
    }
}
