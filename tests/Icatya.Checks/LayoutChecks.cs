using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Icatya;
using Icatya.Controls;
using Icatya.Services;
using Icatya.Services.Rooms;

// Real WPF controls, rendered offscreen. Never Show/ShowDialog, capture the desktop or move the mouse.
internal static class LayoutChecks
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private)!.GetValue(target)!;
    private static void Set(object target, string name, object value) => target.GetType().GetField(name, Private)!.SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private)!.Invoke(target, args);
    private static T Control<T>(MainWindow main, string name) => (T)main.FindName(name);
    private static void Click(MainWindow main, string name) => Control<Button>(main, name).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    public static int Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            MainWindow? main = null;
            RoomSession? room = null;
            try
            {
                Environment.SetEnvironmentVariable("ICATYA_DATA_DIRECTORY", Path.Combine(Directory.GetCurrentDirectory(), "work", "layout-profile-" + Guid.NewGuid().ToString("N")));
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/Icatya;component/Themes/DarkTheme.xaml", UriKind.Relative) });
                main = new MainWindow();
                Set(main, "_skipRoomStartup", true); Set(main, "_rememberLayout", false);
                room = new RoomSession(new(RoomIdentity.CreateCode(), "jax"), false);
                typeof(RoomSession).GetProperty("SelfId")!.SetValue(room, "self");
                var members = Field<IDictionary>(room, "_members");
                members["self"] = new RoomMember("self", "jax", false, "shark");
                members["fox"] = new RoomMember("fox", "Mattretta", true, "dog");
                members["panda"] = new RoomMember("panda", "kina", false, "panda");
                members["tiger"] = new RoomMember("tiger", "Tick", true, "tiger");
                typeof(RoomSession).GetProperty("Connected")!.SetValue(room, true);
                typeof(RoomSession).GetProperty("CanRename")!.SetValue(room, true);
                Set(main, "_room", room);
                Set(main, "_roomIdentity", room.Identity); Set(main, "_avatarId", "shark");
                Control<TextBlock>(main, "SelfNick").Text = "jax";
                Control<TextBlock>(main, "FooterNick").Text = "jax";
                Control<AvatarView>(main, "SelfAvatar").Avatar = "shark";
                Control<AvatarView>(main, "FooterAvatar").Avatar = "shark";
                Call(main, "UpdateControls");
                Call(main, "FocusStream", "fox");
                var root = (FrameworkElement)main.Content;
                main.Content = null; // A standalone visual tree, with no hidden/visible HWND.
                TextElement.SetForeground(root, (Brush)app.FindResource("PrimaryText"));
                TextElement.SetFontFamily(root, new FontFamily("Segoe UI"));
                TextElement.SetFontSize(root, 14);
                TextOptions.SetTextRenderingMode(root, TextRenderingMode.Grayscale);
                var output = Path.Combine(Directory.GetCurrentDirectory(), "work", "ui-new-design");
                Directory.CreateDirectory(output);
                PaintTiles(main);
                Resize(root, 1440, 900); Call(main, "ApplySidebarLayout");
                Call(main, "SetFocusControlVisible", true);
                Snapshot(root, 1440, 900, Path.Combine(output, "foco.png"));
                Require(Control<Button>(main, "CopyRoomButton").IsEnabled, "Invite button did not remain available");
                Require(Control<TextBlock>(main, "StageTitle").Text == "Tela de Mattretta", "Focused title does not follow participant");
                var arrow = Control<Button>(main, "ToggleThumbnailsButton");
                var close = Control<Button>(main, "CloseStreamButton");
                Require(close.TranslatePoint(new Point(), root).X > arrow.TranslatePoint(new Point(), root).X, "Close live must be beside the thumbnail arrow");
                Click(main, "CloseStreamButton");
                Require(!room.IsWatching("fox") && room.IsWatching("tiger"), "Close changed another stream's subscription");
                Require(room.Members.Length == 4 && room.Members[1].Sharing, "Close altered membership or remote sharing");
                Require(!Field<Dictionary<string, RoomVideoTile>>(main, "_tiles").ContainsKey("fox"), "Closed tile was re-created");
                Require(Field<Dictionary<string, RoomVideoTile>>(main, "_tiles").ContainsKey("tiger"), "Close removed another live");
                Call(main, "UpdateControls");
                Require(!Field<Dictionary<string, RoomVideoTile>>(main, "_tiles").ContainsKey("fox"), "Presence refresh reopened a closed tile");
                Field<Dictionary<string, Button>>(main, "_participantButtons")["fox"].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Require(room.IsWatching("fox") && Field<string>(main, "_focusedStream") == "fox", "Participant cannot reopen and focus the live");
                PaintTiles(main);
                Click(main, "ToggleThumbnailsButton");
                Require(Control<ScrollViewer>(main, "ThumbnailScroll").Visibility == Visibility.Collapsed, "Thumbnail toggle does not hide the tray");
                Call(main, "SetFocusControlVisible", true);
                Snapshot(root, 1440, 900, Path.Combine(output, "foco-sem-miniaturas.png"));
                Click(main, "GridModeButton");
                Snapshot(root, 1440, 900, Path.Combine(output, "grade.png"));
                Click(main, "FocusModeButton");
                PaintTiles(main);
                Snapshot(root, 1040, 720, Path.Combine(output, "compacto.png"));
                Require(Control<TextBlock>(main, "StageTitle").ActualWidth > 80, "Stage heading is unusable at minimum size");
                Click(main, "SidebarToggle");
                Snapshot(root, 1040, 720, Path.Combine(output, "sem-lateral.png"));
                Require(Control<Border>(main, "ProfileCard").Visibility == Visibility.Collapsed, "Profile did not collapse with sidebar");
                Click(main, "HeaderSettingsButton");
                Snapshot(root, 1040, 720, Path.Combine(output, "configuracoes.png"));
                Require(!Control<Expander>(main, "AdvancedConnection").IsExpanded, "Manual connection is not collapsed by default");
                Require(main.FindName("SoloButton") == null, "Local self-test remains in the user interface");
                Call(main, "CloseSettingsPanel");
                Click(main, "SidebarToggle");
                var library = Field<RoomLibrary>(main, "_roomLibrary");
                library.Remember(new(RoomIdentity.CreateCode(), "jax", "Cinema"));
                library.Remember(new(RoomIdentity.CreateCode(), "jax", "Jogos"));
                library.Remember(room.Identity);
                Call(main, "RenderRoomPicker");
                Require(Control<StackPanel>(main, "SavedRoomChoices").Children.Count == 3, "Saved-room selector does not render the library");
                Control<TextBox>(main, "RoomSearch").Text = "cinema";
                Require(Control<StackPanel>(main, "SavedRoomChoices").Children.Count == 1, "Room search does not filter names");
                Control<TextBox>(main, "RoomSearch").Text = "missing";
                Require(Control<TextBlock>(main, "EmptyRoomChoices").Visibility == Visibility.Visible, "Search has no empty state");
                Control<TextBox>(main, "RoomSearch").Text = "";
                Require(Control<Button>(main, "RenameRoomButton").IsEnabled, "Shared-room rename action unavailable");
                Click(main,"RenameRoomButton");
                Require(Control<StackPanel>(main,"RenameRoomPanel").Visibility==Visibility.Visible && Control<TextBox>(main,"NewRoomName").Text==room.Name,
                    "Rename editor does not start with current shared name");
                Call(main,"CancelRenameRoom",main,new RoutedEventArgs());
                // Render the actual popup card offscreen without opening a native popup window.
                var popup = Control<System.Windows.Controls.Primitives.Popup>(main, "RoomPicker");
                var card = Control<Border>(main, "RoomPickerCard");
                popup.Child = null;
                card.SetResourceReference(Border.BackgroundProperty, "PanelBackground");
                card.HorizontalAlignment = HorizontalAlignment.Left; card.VerticalAlignment = VerticalAlignment.Top;
                card.Margin = new Thickness(180,145,0,0);
                ((Grid)root).Children.Add(card);
                Snapshot(root,1440,900,Path.Combine(output,"seletor-salas.png"));
                ((Grid)root).Children.Remove(card); card.Margin = new Thickness(0); popup.Child = card;
                Console.WriteLine("PASS room selector: saved rooms, name search, empty state, collapsed advanced section and removed self-test");
                Console.WriteLine("PASS WPF offscreen: focus/grid, selected participant, close/reopen one live, thumbnail toggle, minimum size, sidebar and settings");
                Console.WriteLine("PASS no app window, desktop capture, user preference writes or mouse/keyboard automation; PNGs: " + output);
            }
            catch (Exception ex) { failure = ex; }
            finally
            {
                if (main != null)
                {
                    Field<Icatya.Services.Video.PresentationClock>(main, "_roomTimer").Stop();
                    Field<Icatya.Services.Video.PresentationClock>(main, "_renderTimer").Stop();
                    Field<Icatya.Services.Audio.AudioPlayback>(main, "_audioPlayback").Dispose();
                    Set(main, "_allowClose", true); Set(main, "_closing", true);
                }
                if (room != null) room.DisposeAsync().AsTask().GetAwaiter().GetResult();
                Application.Current?.Shutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) { Console.Error.WriteLine(failure); return 1; }
        return 0;
    }
    private static void Resize(FrameworkElement root, int width, int height)
    {
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
    }
    private static void Snapshot(FrameworkElement root, int width, int height, string path)
    {
        Resize(root, width, height);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
    private static void PaintTiles(MainWindow main)
    {
        foreach (var (id, tile) in Field<Dictionary<string, RoomVideoTile>>(main, "_tiles"))
        {
            Field<Image>(tile, "_image").Source = Landscape(id == "fox");
            Field<TextBlock>(tile, "_waiting").Visibility = Visibility.Collapsed;
        }
    }
    private static DrawingImage Landscape(bool warm)
    {
        var drawing = new DrawingGroup();
        using (var context = drawing.Open())
        {
            context.DrawRectangle(new LinearGradientBrush(warm ? Color.FromRgb(83, 91, 144) : Color.FromRgb(45, 91, 126), Color.FromRgb(235, 166, 143), 90), null, new Rect(0, 0, 1280, 720));
            context.DrawEllipse(new SolidColorBrush(Color.FromRgb(255, 220, 170)), null, new Point(850, 240), 54, 54);
            context.DrawGeometry(new SolidColorBrush(Color.FromRgb(75, 85, 116)), null, Geometry.Parse("M0,440 L150,230 290,380 510,150 700,360 870,190 1050,350 1170,210 1280,340 1280,720 0,720Z"));
            context.DrawGeometry(new SolidColorBrush(Color.FromRgb(35, 57, 78)), null, Geometry.Parse("M0,560 L220,350 390,490 610,330 820,530 1080,360 1280,500 1280,720 0,720Z"));
            context.DrawGeometry(new SolidColorBrush(Color.FromRgb(24, 41, 52)), null, Geometry.Parse("M0,660 L210,510 420,650 630,570 820,680 1080,540 1280,610 1280,720 0,720Z"));
        }
        var image = new DrawingImage(drawing); image.Freeze(); return image;
    }
}
