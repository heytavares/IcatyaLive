using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Icatya.Controls;

// Selected Lucide vectors are bundled as frozen WPF geometry; no browser, font or runtime package.
public sealed class LucideIcon : FrameworkElement
{
    private static readonly Lazy<Dictionary<string, Geometry>> Icons = new(() =>
    {
        var resources = new ResourceDictionary { Source = new Uri("/Icatya;component/Themes/Lucide.xaml", UriKind.Relative) };
        var icons = new Dictionary<string, Geometry>(StringComparer.Ordinal);
        foreach (string key in resources.Keys)
        {
            var geometry = (Geometry)resources[key];
            geometry.Freeze(); icons.Add(key, geometry);
        }
        return icons;
    });
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(LucideIcon),
        new FrameworkPropertyMetadata("monitor", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ForegroundProperty = Control.ForegroundProperty.AddOwner(typeof(LucideIcon),
        new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public LucideIcon() { Width = Height = 20; IsHitTestVisible = false; }
    protected override void OnRender(DrawingContext context)
    {
        if (!Icons.Value.TryGetValue(Kind, out var geometry)) return;
        double scale = Math.Min(ActualWidth, ActualHeight) / 24;
        if (scale <= 0) return;
        var pen = new Pen(Foreground, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        context.PushTransform(new TranslateTransform((ActualWidth - 24 * scale) / 2, (ActualHeight - 24 * scale) / 2));
        context.PushTransform(new ScaleTransform(scale, scale));
        context.DrawGeometry(null, pen, geometry);
        context.Pop(); context.Pop();
    }
}
