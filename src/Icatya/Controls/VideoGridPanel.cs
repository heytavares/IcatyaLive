using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Icatya.Controls;

// Keeps every card 16:9 and centers incomplete rows, without stretching video.
public sealed class VideoGridPanel : Panel
{
    private const double Gap = 8;
    public static (int Columns, double Width, double Height) Calculate(int count, double width, double height)
    {
        if (count <= 0 || !double.IsFinite(width) || !double.IsFinite(height)) return (1, 0, 0);
        int best = 1;
        double bestWidth = 0;
        for (int columns = 1; columns <= count; columns++)
        {
            int rows = (count + columns - 1) / columns;
            double w = Math.Max(0, (width - Gap * (columns - 1)) / columns);
            double h = Math.Max(0, (height - Gap * (rows - 1)) / rows);
            w = Math.Min(w, h * 16 / 9);
            if (w > bestWidth) { bestWidth = w; best = columns; }
        }
        return (best, bestWidth, bestWidth * 9 / 16);
    }
    protected override Size MeasureOverride(Size available)
    {
        var visible = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
        var layout = Calculate(visible.Length, available.Width, available.Height);
        foreach (var child in visible) child.Measure(new Size(layout.Width, layout.Height));
        return new Size(double.IsFinite(available.Width) ? available.Width : 0, double.IsFinite(available.Height) ? available.Height : 0);
    }
    protected override Size ArrangeOverride(Size final)
    {
        var visible = InternalChildren.Cast<UIElement>().Where(c => c.Visibility != Visibility.Collapsed).ToArray();
        var layout = Calculate(visible.Length, final.Width, final.Height);
        int rows = (visible.Length + layout.Columns - 1) / layout.Columns;
        double top = Math.Max(0, (final.Height - rows * layout.Height - Math.Max(0, rows - 1) * Gap) / 2);
        for (int i = 0; i < visible.Length; i++)
        {
            int row = i / layout.Columns, column = i % layout.Columns;
            int inRow = Math.Min(layout.Columns, visible.Length - row * layout.Columns);
            double left = (final.Width - inRow * layout.Width - Math.Max(0, inRow - 1) * Gap) / 2;
            visible[i].Arrange(new Rect(left + column * (layout.Width + Gap), top + row * (layout.Height + Gap), layout.Width, layout.Height));
        }
        return final;
    }
}
