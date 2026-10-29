using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Icatya.Controls;
using Icatya.Models;
using Icatya.Services.Video;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Icatya;component/Themes/DarkTheme.xaml", UriKind.Relative) });
        var label = new TextBlock { Foreground = Brushes.White, FontSize = 16, Margin = new Thickness(12) };
        var stage = new Grid();
        var root = new DockPanel { Background = new SolidColorBrush(Color.FromRgb(13, 14, 17)) };
        DockPanel.SetDock(label, Dock.Top); root.Children.Add(label); root.Children.Add(stage);
        var window = new Window { Title = "iCatya · teste de exibição sintético", Width = 1280, Height = 850, Content = root, WindowStartupLocation = WindowStartupLocation.CenterScreen };
        var results = new List<object>();
        window.Loaded += async (_, _) =>
        {
            try
            {
                foreach (var sample in new[] { (1280,720,0), (1920,1080,0), (1920,1080,3) })
                {
                    stage.Children.Clear(); stage.RowDefinitions.Clear();
                    stage.RowDefinitions.Add(new RowDefinition()); stage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var tiles = new List<RoomVideoTile> { new("Teste · imagem principal") };
                    stage.Children.Add(tiles[0].View);
                    var tray = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                    Grid.SetRow(tray, 1); stage.Children.Add(tray);
                    for (int i = 0; i < sample.Item3; i++)
                    {
                        var tile = new RoomVideoTile("Miniatura " + (i + 1));
                        tile.View.Width = 160; tile.View.Height = 90;
                        // Optional until the thumbnail optimization is introduced.
                        typeof(RoomVideoTile).GetMethod("SetThumbnail")?.Invoke(tile, new object[] { true });
                        tiles.Add(tile); tray.Children.Add(tile.View);
                    }
                    using var stop = new CancellationTokenSource();
                    var producer = Task.Factory.StartNew(() =>
                    {
                        using var pacer = new FramePacer(60, stop.Token);
                        int index = 0;
                        while (!stop.IsCancellationRequested)
                        {
                            using var frame = VideoFrame.Rent(sample.Item1, sample.Item2);
                            // Moving synthetic color blocks; never captures the desktop or any application.
                            for (int y = 0; y < frame.Height; y++)
                                Array.Fill(frame.Pixels, (byte)((y / 16 + index) % 200 + 20), y * frame.Stride, frame.Stride);
                            foreach (var tile in tiles) tile.Queue(frame);
                            index++; if (!pacer.WaitNext()) break;
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    var clock = new PresentationClock();
                    long ticks = 0, copyTicks = 0;
                    clock.Tick += (_, _) =>
                    {
                        long start = Stopwatch.GetTimestamp();
                        foreach (var tile in tiles) tile.Render();
                        copyTicks += Stopwatch.GetTimestamp() - start; ticks++;
                    };
                    clock.Start();
                    label.Text = $"Teste local: {sample.Item1} × {sample.Item2}, {sample.Item3} miniaturas · sem captura de tela";
                    await Task.Delay(1200);
                    long before = tiles[0].PresentedFrames, busyBefore = tiles.Sum(t => t.BusyPresentationAttempts), tickBefore = ticks, copyBefore = copyTicks;
                    var watch = Stopwatch.StartNew();
                    await Task.Delay(6000);
                    watch.Stop(); clock.Stop(); stop.Cancel(); await producer;
                    results.Add(new { width = sample.Item1, height = sample.Item2, thumbnails = sample.Item3,
                        fps = (tiles[0].PresentedFrames - before) / watch.Elapsed.TotalSeconds,
                        compositionTicksPerSecond = (ticks - tickBefore) / watch.Elapsed.TotalSeconds,
                        busy = tiles.Sum(t => t.BusyPresentationAttempts) - busyBefore,
                        uploadMs = ticks == tickBefore ? 0 : (copyTicks - copyBefore) * 1000d / Stopwatch.Frequency / (ticks - tickBefore),
                        tier = RenderCapability.Tier >> 16 });
                    foreach (var tile in tiles) tile.Dispose();
                }
                File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "presentation-results.json"), JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex) { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "presentation-error.txt"), ex.ToString()); }
            finally { window.Close(); app.Shutdown(); }
        };
        window.Show(); app.Run();
    }
}
