using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Icatya.Services;

public sealed record AvatarOption(string Id, string Name);

public static class AvatarCatalog
{
    public const string Default = "cat";
    public static IReadOnlyList<AvatarOption> Available { get; } = Array.AsReadOnly(new[] {
        new AvatarOption("bear", "Urso"), new("cat", "Gato"), new("dog", "Cachorro"),
        new("dragon", "Dragão"), new("gorilla", "Gorila"), new("meerkat", "Suricato"),
        new("panda", "Panda"), new("penguin", "Pinguim"), new("shark", "Tubarão"), new("tiger", "Tigre")
    });
    private static readonly ConcurrentDictionary<string, ImageBrush> Brushes = new();
    public static bool IsValid(string? id) => Available.Any(option => option.Id == id);
    public static string Normalize(string? id) => IsValid(id) ? id! : Default;
    public static ImageBrush GetBrush(string? id) => Brushes.GetOrAdd(Normalize(id), key =>
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 128;
        image.UriSource = new Uri("pack://application:,,,/Icatya;component/Assets/Avatars/" + key + ".png", UriKind.Absolute);
        image.EndInit(); image.Freeze();
        var brush = new ImageBrush(image) { Stretch = Stretch.UniformToFill };
        brush.Freeze(); return brush;
    });
}
