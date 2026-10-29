using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Icatya.Controls;
using Icatya.Services.Rooms;

namespace Icatya;

public partial class MainWindow
{
    private readonly RoomDirectory _roomDirectory = new();
    private readonly Dictionary<string, RoomSummary> _roomSummaries = new();
    private readonly DispatcherTimer _roomPickerTimer = new() { Interval = TimeSpan.FromSeconds(20) };
    private CancellationTokenSource? _roomPickerCancellation;
    private bool _roomsFetching, _pickerTimerWired;
    private DateTimeOffset _roomPickerRefreshUntil;
    private void ToggleRoomPicker()
    {
        if (_busy || _closing) return;
        RoomPicker.PlacementTarget = _sidebarHidden ? SidebarToggle : JoinRoomButton;
        RoomPicker.IsOpen = !RoomPicker.IsOpen;
    }
    private async void RoomPickerOpened(object? sender, EventArgs e)
    {
        RoomSearch.Text = "";
        RenameRoomPanel.Visibility = Visibility.Collapsed;
        _roomSummaries.Clear(); RoomDirectoryStatus.Text = "Consultando pessoas online…";
        _roomPickerCancellation?.Dispose();
        _roomPickerCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _roomPickerRefreshUntil = DateTimeOffset.UtcNow.AddMinutes(2);
        if (!_pickerTimerWired)
        {
            _roomPickerTimer.Tick += async (_, _) =>
            {
                if (DateTimeOffset.UtcNow >= _roomPickerRefreshUntil)
                {
                    _roomPickerTimer.Stop(); _roomPickerCancellation?.Cancel(); _roomSummaries.Clear();
                    RoomDirectoryStatus.Text = "Reabra o seletor para atualizar quem está online.";
                    RenderRoomPicker(); return;
                }
                await RefreshRoomDirectoryAsync();
            };
            _pickerTimerWired = true;
        }
        _roomPickerTimer.Start();
        RenderRoomPicker(); RoomSearch.Focus();
        await RefreshRoomDirectoryAsync();
    }
    private void RoomPickerClosed(object? sender, EventArgs e)
    {
        _roomPickerTimer.Stop(); _roomPickerCancellation?.Cancel();
        _roomsFetching = false;
    }
    private async Task RefreshRoomDirectoryAsync()
    {
        if (_roomsFetching || !RoomPicker.IsOpen || _roomPickerCancellation == null) return;
        var request = _roomPickerCancellation;
        var token = request.Token;
        var service = RoomServiceAddress.Load();
        if (service == null) { RoomDirectoryStatus.Text = "Serviço de salas não configurado."; return; }
        _roomsFetching = true;
        try
        {
            var entries = _roomLibrary.Load();
            var summaries = await _roomDirectory.QueryAsync(service, entries.Select(room => room.Identity.Code), token);
            if (token.IsCancellationRequested || !ReferenceEquals(request, _roomPickerCancellation)) return;
            _roomSummaries.Clear();
            foreach (var summary in summaries) _roomSummaries[summary.Code] = summary;
            _roomLibrary.UpdateNames(summaries.Where(room => room.Code != _roomIdentity?.Code).Select(room => (room.Code, room.Name)));
            RoomDirectoryStatus.Text = "Pessoas online atualizadas enquanto este seletor estiver aberto.";
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or InvalidOperationException or JsonException or KeyNotFoundException or FormatException)
        {
            if (!ReferenceEquals(request, _roomPickerCancellation)) return;
            _roomSummaries.Clear();
            RoomDirectoryStatus.Text = "Não foi possível consultar quem está online. Suas salas salvas continuam disponíveis.";
        }
        finally
        {
            if (ReferenceEquals(request, _roomPickerCancellation))
            { _roomsFetching = false; if (RoomPicker.IsOpen) RenderRoomPicker(); }
        }
    }
    private void RoomSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (SavedRoomChoices != null) RenderRoomPicker();
    }
    private void RenderRoomPicker()
    {
        SavedRoomChoices.Children.Clear();
        string query = RoomSearch.Text.Trim();
        RoomSearchPlaceholder.Visibility = query.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        foreach (var saved in _roomLibrary.Load())
        {
            bool here = _roomIdentity?.Code == saved.Identity.Code && _room != null;
            _roomSummaries.TryGetValue(saved.Identity.Code, out var summary);
            string name = here ? _room!.Name : summary?.Name ?? saved.Identity.Name;
            if (!name.Contains(query, StringComparison.CurrentCultureIgnoreCase)) continue;
            var row = new Grid();
            row.ColumnDefinitions.Add(new() { Width = new GridLength(48) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            string kind = name.Contains("jogo", StringComparison.CurrentCultureIgnoreCase) ? "gamepad-2" :
                name.Contains("cinema", StringComparison.CurrentCultureIgnoreCase) || name.Contains("filme", StringComparison.CurrentCultureIgnoreCase) ? "clapperboard" : "users";
            var icon = new Border { Width = 36, Height = 36, CornerRadius = new CornerRadius(10), HorizontalAlignment = HorizontalAlignment.Left,
                Background = (Brush)FindResource(here ? "AccentPressedBrush" : "SelectedBackground"), Child = new LucideIcon { Kind = kind, Foreground = Brushes.White } };
            row.Children.Add(icon);
            var description = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,8,0) };
            var foreground = here ? (Brush)new BrushConverter().ConvertFromString("#211A36")! : (Brush)FindResource("PrimaryText");
            description.Children.Add(new TextBlock { Text = name, FontWeight = FontWeights.SemiBold, FontSize = 14, Foreground = foreground, TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis });
            string status = here ? _room!.Connected ? $"{_room.Members.Length} {(_room.Members.Length == 1 ? "pessoa" : "pessoas")} · Você está aqui" : "Reconectando…" :
                summary == null ? "Online não consultado" : summary.Count == 0 ? "Ninguém online" : $"{summary.Count} {(summary.Count == 1 ? "pessoa" : "pessoas")} online";
            var statusLine = new Grid { Margin = new Thickness(0,4,0,0) };
            statusLine.ColumnDefinitions.Add(new() { Width = GridLength.Auto }); statusLine.ColumnDefinitions.Add(new());
            if (!here && summary != null)
                statusLine.Children.Add(new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0,0,5,0), Fill = (Brush)FindResource(summary.Count > 0 ? "SuccessBrush" : "MutedText") });
            var statusText = new TextBlock { Text = status, FontSize = 11, Foreground = here ? foreground : (Brush)FindResource("SecondaryText"), TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(statusText,1); statusLine.Children.Add(statusText); description.Children.Add(statusLine);
            Grid.SetColumn(description,1); row.Children.Add(description);
            if (here)
            {
                var check = new LucideIcon { Kind = "check", Foreground = foreground }; Grid.SetColumn(check,2); row.Children.Add(check);
            }
            else if (summary != null)
            {
                var portraits = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                foreach (string avatar in summary.Avatars) portraits.Children.Add(new AvatarView { Avatar = avatar, Width = 22, Height = 22, Margin = new Thickness(-4,0,0,0) });
                Grid.SetColumn(portraits,2); row.Children.Add(portraits);
            }
            var choice = new Button { Content = row, Style = (Style)FindResource("RoomChoiceRow"), Tag = here ? "selected" : null, ToolTip = name };
            choice.Click += async (_, _) => await SwitchSavedRoomAsync(saved.Identity with { Name = name });
            System.Windows.Automation.AutomationProperties.SetName(choice, here ? name + ", sala atual" : "Entrar em " + name);
            SavedRoomChoices.Children.Add(choice);
        }
        EmptyRoomChoices.Visibility = SavedRoomChoices.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RenameRoomButton.IsEnabled = _room?.Connected == true && _room.CanRename;
        RenameRoomButton.ToolTip = RenameRoomButton.IsEnabled ? "Alterar o nome para todos os participantes" : "Conecte-se ao serviço atualizado para renomear";
    }
    private async Task SwitchSavedRoomAsync(RoomIdentity identity)
    {
        RoomPicker.IsOpen = false;
        if (_room?.Connected == true && _roomIdentity?.Code == identity.Code) return;
        await RunActionAsync(async () =>
        {
            await ResetAsync();
            _roomStore.Save(identity); await EnterRoomAsync(identity);
        });
    }
    private void CreateSavedRoom(object sender, RoutedEventArgs e) => OpenRoomDialog(create: true);
    private void JoinSavedRoom(object sender, RoutedEventArgs e) => OpenRoomDialog();
    private void ChangeNick(object sender, RoutedEventArgs e)
    {
        CloseSettingsPanel(); OpenRoomDialog(preserveCode: true);
    }
    private void BeginRenameRoom(object sender, RoutedEventArgs e)
    {
        if (_room?.CanRename != true) return;
        NewRoomName.Text = _room.Name; RenameRoomError.Text = "";
        RenameRoomPanel.Visibility = Visibility.Visible; NewRoomName.Focus(); NewRoomName.SelectAll();
    }
    private void CancelRenameRoom(object sender, RoutedEventArgs e) => RenameRoomPanel.Visibility = Visibility.Collapsed;
    private async void SaveRoomName(object sender, RoutedEventArgs e)
    {
        var room = _room;
        if (room == null) return;
        SaveRoomNameButton.IsEnabled = false;
        try
        {
            await room.RenameAsync(NewRoomName.Text);
            RenameRoomPanel.Visibility = Visibility.Collapsed;
            SetStatus("Nome enviado. A sala será atualizada para todos os participantes.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException) { RenameRoomError.Text = ex.Message; }
        finally { SaveRoomNameButton.IsEnabled = true; }
    }
    private void RoomPickerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control)
        { RoomPicker.IsOpen = false; e.Handled = true; }
    }
    private void CopySupportDiscord(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText("fvckjax"); SetStatus("Usuário do Discord copiado: fvckjax"); }
        catch (Exception ex) when (ex is not OutOfMemoryException) { SetStatus("Discord para contato: fvckjax. Não foi possível copiar agora."); }
    }
}
