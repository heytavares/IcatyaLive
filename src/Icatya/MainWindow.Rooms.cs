using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Icatya.Controls;
using Icatya.Models;
using Icatya.Services.Rooms;
using Icatya.Services.Video;

namespace Icatya;

public partial class MainWindow
{
    private readonly RoomIdentityStore _roomStore = new();
    private readonly RoomLibrary _roomLibrary = new();
    private RoomSession? _room;
    private RoomIdentity? _roomIdentity;
    private bool _skipRoomStartup = false;
    private readonly object _tileGate = new();
    private readonly Dictionary<string, RoomVideoTile> _tiles = new();
    private readonly Dictionary<string, RoomParticipantCard> _participantCards = new();
    private readonly Dictionary<string, Button> _participantButtons = new();
    private string? _focusedStream;
    private bool _thumbnailsHidden;
    private readonly PresentationClock _roomTimer = new();

    private async Task InitializeRoomAsync()
    {
        if (_skipRoomStartup || _closing) return;
        _roomTimer.Tick += (_, _) =>
        {
            foreach (var tile in _tiles.Values) tile.Render();
        };
        _roomIdentity = _roomStore.Load() ?? _roomLibrary.Load().FirstOrDefault()?.Identity;
        if (_roomIdentity != null) await EnterRoomAsync(_roomIdentity);
        else OpenRoomDialog();
    }
    private void JoinRoom(object sender, RoutedEventArgs e) => ToggleRoomPicker();
    private async void OpenRoomDialog(bool create = false, bool preserveCode = false)
    {
        if (_busy || _closing) return;
        RoomPicker.IsOpen = false;
        var dialog = new RoomJoinWindow(_roomIdentity ?? _roomStore.Load(), create, preserveCode) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.Identity == null) return;
        await RunActionAsync(async () =>
        {
            await ResetAsync();
            _roomIdentity = dialog.Identity;
            bool saved = _roomStore.Save(_roomIdentity);
            await EnterRoomAsync(_roomIdentity);
            if (!saved) SetStatus("Entrando na sala, mas não foi possível salvar o convite neste PC.");
        });
    }
    private Task EnterRoomAsync(RoomIdentity identity)
    {
        _roomIdentity = identity;
        _roomLibrary.Remember(identity);
        SelfNick.Text = identity.Nick;
        FooterNick.Text = identity.Nick;
        CopyRoomButton.IsEnabled = true;
        var address = RoomServiceAddress.Load();
        if (address == null)
        {
            RoomTitle.Text = "Sala salva";
            RoomDescription.Text = "Serviço aguardando configuração";
            SetStatus("Convite e nick salvos. O serviço de salas ainda precisa ser publicado na Cloudflare.");
            return Task.CompletedTask;
        }
        var room = new RoomSession(identity, clientKey: RoomClientIdentity.LoadOrCreate(), avatar: _avatarId);
        _room = room;
        room.NameChanged += () => OnUi(() =>
        {
            if (_room != room) return;
            _roomIdentity = room.Identity with { Name = room.Name };
            _roomStore.Save(_roomIdentity);
            _roomLibrary.UpdateNames([(_roomIdentity.Code, room.Name)]);
            UpdateRoomVisuals();
            if (RoomPicker.IsOpen) RenderRoomPicker();
        });
        room.MembersChanged += () => OnUi(() => { if (_room == room) UpdateControls(); });
        room.StatusChanged += message => OnUi(() => { if (_room == room) SetStatus(message); });
        room.AudioReceived += (id, sequence, packet) => { if (_room == room) _audioPlayback.AddPacket(id, sequence, packet); };
        room.FrameReceived += (id, frame) => QueueRoomFrame(room, id, frame);
        room.StreamStatusChanged += (id, text) => OnUi(() =>
        {
            if (_room == room && _tiles.TryGetValue(id, out var tile)) tile.SetStatus(text);
        });
        room.StreamStopped += id => OnUi(() =>
        {
            if (_room == room) { _audioPlayback.Remove(id); RemoveTile(id); }
        });
        room.Start(address);
        UpdateControls();
        return Task.CompletedTask;
    }
    private void CopyRoomInvite(object sender, RoutedEventArgs e)
    {
        if (_roomIdentity == null) return;
        try { System.Windows.Clipboard.SetText(_roomIdentity.Code); SetStatus("Convite da sala copiado. Seus amigos usam esse mesmo convite para entrar."); }
        catch (Exception) { SetStatus("Não foi possível copiar. Tente novamente."); }
    }
    private void UpdateRoomVisuals()
    {
        var room = _room!;
        var members = room.Members;
        ProfileConnectionStatus.Text = room.Connected ? "Na sala" : "Reconectando…";
        bool capturing = _captureTask is { IsCompleted: false };
        CaptureTitle.Text = capturing ? "Você está compartilhando sua tela" : "Seu compartilhamento está parado";
        if (!capturing) Metrics.Text = members.Count(m => m.Id != room.SelfId && m.Sharing) + " transmissões disponíveis";
        RoomTitle.Text = room.Name;
        RoomDescription.Text = room.Connected ? members.Length + (members.Length == 1 ? " pessoa" : " pessoas") : "Reconectando à sala…";
        CopyRoomButton.IsEnabled = _roomIdentity != null;
        RemoteParticipant.Visibility = Visibility.Collapsed;
        SelfLive.Visibility = _captureTask is { IsCompleted: false } ? Visibility.Visible : Visibility.Collapsed;
        ParticipantsHint.Visibility = members.Length <= 1 ? Visibility.Visible : Visibility.Collapsed;
        RoomParticipants.Children.Clear();
        _participantButtons.Clear();
        foreach (var member in members.Where(m => m.Id != room.SelfId))
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new() { Width = new GridLength(44) });
            row.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new AvatarView { Width = 34, Height = 34, Avatar = member.Avatar, HorizontalAlignment = HorizontalAlignment.Left });
            var name = new TextBlock { Text = member.Nick, FontSize = 14, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap };
            Grid.SetColumn(name, 1); row.Children.Add(name);
            if (member.Sharing)
            {
                bool watching = room.IsWatching(member.Id);
                if (watching) _audioPlayback.Allow(member.Id); else _audioPlayback.Remove(member.Id);
                var badge = new Border { Style = (Style)FindResource("LiveBadge"), Child = new TextBlock { Text = "AO VIVO", FontSize = 9, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White } };
                Grid.SetColumn(badge, 2); row.Children.Add(badge);
                if (watching) EnsureTile(member.Id, member.Nick); else RemoveTile(member.Id);
            }
            else RemoveTile(member.Id);
            var button = new Button { Content = row, Style = (Style)FindResource("ParticipantRow"), ToolTip = member.Sharing ? room.IsWatching(member.Id) ? "Focar transmissão de " + member.Nick : "Reabrir transmissão de " + member.Nick : member.Nick + " não está transmitindo" };
            string memberId = member.Id;
            button.Click += (_, _) => OpenMemberStream(memberId);
            System.Windows.Automation.AutomationProperties.SetName(button, member.Sharing ? "Assistir à transmissão de " + member.Nick : member.Nick);
            _participantButtons[member.Id] = button;
            RoomParticipants.Children.Add(button);
        }
        var ids = members.Select(m => m.Id).ToHashSet();
        foreach (var id in _tiles.Keys.Where(id => !ids.Contains(id)).ToArray()) RemoveTile(id);
        foreach (var id in _participantCards.Keys.Where(id => !ids.Contains(id)).ToArray()) _participantCards.Remove(id);
        foreach (var member in members)
        {
            string nick = member.Nick + (member.Id == room.SelfId ? " (você)" : "");
            if (_participantCards.TryGetValue(member.Id, out var card)) card.Rename(nick, member.Avatar);
            else
            {
                var created = new RoomParticipantCard(nick, member.Avatar);
                string memberId = member.Id;
                created.ReopenRequested += () => OpenMemberStream(memberId);
                _participantCards.Add(member.Id, created);
            }
            _participantCards[member.Id].SetClosedLive(member.Sharing && member.Id != room.SelfId && !room.IsWatching(member.Id));
        }
        if (_captureTask is { IsCompleted: false }) EnsureTile(room.SelfId, room.Identity.Nick + " (você)");
        else RemoveTile(room.SelfId);
        ArrangeRoomVideos();
    }
    private void EnsureTile(string id, string nick)
    {
        if (id.Length == 0 || _room?.IsWatching(id) == false) return;
        lock (_tileGate)
        {
            if (_tiles.TryGetValue(id, out var previous)) { previous.Rename(nick); return; }
            var tile = new RoomVideoTile(nick);
            if (_room != null && id != _room.SelfId) tile.SetStatus(_room.StreamStatus(id));
            tile.FocusRequested += () => { if (_focusedStream == id) ClearStreamFocus(); else FocusStream(id); };
            _tiles.Add(id, tile);
            ArrangeRoomVideos();
        }
    }
    private void QueueRoomFrame(RoomSession room, string id, VideoFrame frame)
    {
        lock (_tileGate)
            if (_room == room && _tiles.TryGetValue(id, out var tile)) tile.Queue(frame);
    }
    private void RemoveTile(string id)
    {
        lock (_tileGate)
        {
            if (!_tiles.Remove(id, out var tile)) return;
            DetachRoomCards();
            tile.Dispose();
            if (_focusedStream == id) _focusedStream = null;
            ArrangeRoomVideos();
        }
    }
    private void ArrangeRoomVideos()
    {
        DetachRoomCards();
        if (_focusedStream != null && !_tiles.ContainsKey(_focusedStream)) _focusedStream = null;
        bool focused = _focusedStream != null;
        FocusModeButton.Tag = focused ? "selected" : null;
        GridModeButton.Tag = focused ? null : "selected";
        SelfParticipantButton.Tag = _focusedStream == _room?.SelfId && _focusedStream != null ? "selected" : null;
        foreach (var pair in _participantButtons) pair.Value.Tag = pair.Key == _focusedStream ? "selected" : null;
        CloseStreamButton.Visibility = focused && _focusedStream != _room?.SelfId ? Visibility.Visible : Visibility.Collapsed;
        UpdateStageHeading();
        RoomStage.Visibility = _tiles.Count + _participantCards.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        RoomVideos.Visibility = focused ? Visibility.Collapsed : Visibility.Visible;
        FocusSurface.Visibility = ExitFocusButton.Visibility = focused ? Visibility.Visible : Visibility.Collapsed;
        UpdateFocusTray();
        foreach (var pair in _tiles)
        {
            pair.Value.SetThumbnail(focused && pair.Key != _focusedStream);
            var view = pair.Value.View;
            view.Width = view.Height = double.NaN;
            view.Margin = new Thickness(0);
            if (focused && pair.Key == _focusedStream)
            {
                FocusHost.Children.Add(view);
                view.ToolTip = "Clique para voltar à grade";
            }
            else if (focused) { view.ToolTip = "Clique para focar esta tela"; AddThumbnail(view); }
            else { view.ToolTip = "Clique para focar esta tela"; RoomVideos.Children.Add(view); }
        }
        foreach (var card in _participantCards.Values)
        {
            card.View.Width = card.View.Height = double.NaN;
            card.View.Margin = new Thickness(0);
            if (focused) AddThumbnail(card.View); else RoomVideos.Children.Add(card.View);
        }
        if (_tiles.Count == 0)
        {
            _roomTimer.Stop();
            if (_room != null && _captureTask is not { IsCompleted: false }) _renderTimer.Stop();
        }
        else
        {
            _roomTimer.Start();
            if (_room != null) _renderTimer.Start();
        }
        RefreshLiveOverlays();
    }
    private void DetachRoomCards()
    {
        RoomVideos.Children.Clear(); FocusHost.Children.Clear(); RoomThumbnails.Children.Clear();
    }
    private void AddThumbnail(FrameworkElement card)
    {
        card.Width = 160; card.Height = 90; card.Margin = new Thickness(0, 0, 8, 0);
        RoomThumbnails.Children.Add(card);
    }
    private void FocusStream(string id)
    {
        if (!_tiles.ContainsKey(id)) return;
        if (_focusedStream == id) return;
        if (_focusedStream == null) _thumbnailsHidden = false;
        _focusedStream = id;
        ArrangeRoomVideos();
        FocusHost.Focusable = true;
        FocusHost.Focus();
    }
    private void ExitFocus(object sender, RoutedEventArgs e) => ClearStreamFocus();
    private void ClearStreamFocus()
    {
        _focusedStream = null;
        _thumbnailsHidden = false;
        ArrangeRoomVideos();
    }
    private void ToggleThumbnails(object sender, RoutedEventArgs e)
    {
        _thumbnailsHidden = !_thumbnailsHidden;
        UpdateFocusTray();
        // Keep hover-only controls from staying visible after a mouse click moves away.
        FocusHost.Focusable = true;
        FocusHost.Focus();
        e.Handled = true;
    }
    private void SetFocusControlVisible(bool visible)
    {
        double opacity = visible ? 1 : 0;
        FocusControls.Opacity = opacity;
        FocusControls.IsHitTestVisible = visible;
        bool showBack = visible;
        ExitFocusButton.Opacity = showBack ? 1 : 0;
        ExitFocusButton.IsHitTestVisible = showBack;
    }
    private void FocusToggleFocused(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e) => LiveOverlayFocusChanged();
    private void FocusToggleUnfocused(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e) => LiveOverlayFocusChanged();
    private void FocusBackFocused(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e) => LiveOverlayFocusChanged();
    private void FocusBackUnfocused(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs e) => LiveOverlayFocusChanged();
    private void UpdateFocusTray()
    {
        ThumbnailScroll.Visibility = _focusedStream != null && !_thumbnailsHidden ? Visibility.Visible : Visibility.Collapsed;
        FocusTrayArrow.Data = Geometry.Parse(_thumbnailsHidden ? "M1,8 L8,1 15,8" : "M1,1 L8,8 15,1");
        var label = _thumbnailsHidden ? "Mostrar miniaturas" : "Ocultar miniaturas";
        ToggleThumbnailsButton.ToolTip = label;
        System.Windows.Automation.AutomationProperties.SetName(ToggleThumbnailsButton, label);
    }
    private async Task LeaveRoomAsync()
    {
        var room = _room;
        _room = null;
        _audioPlayback.Clear();
        _roomTimer.Stop();
        _renderTimer.Stop();
        if (room != null) await room.DisposeAsync();
        _participantCards.Clear();
        _participantButtons.Clear();
        _focusedStream = null;
        _thumbnailsHidden = false;
        foreach (var id in _tiles.Keys.ToArray()) RemoveTile(id);
        ArrangeRoomVideos();
        RoomParticipants.Children.Clear();
        ProfileConnectionStatus.Text = "Fora da sala";
        HeaderConnectionStatus.Text = "Fora da sala";
    }

    private void ChooseFocusMode(object sender, RoutedEventArgs e)
    {
        string? id = _tiles.Keys.FirstOrDefault(id => id != _room?.SelfId) ?? _tiles.Keys.FirstOrDefault();
        if (id != null) FocusStream(id);
    }
    private void OpenOwnStream(object sender, RoutedEventArgs e)
    {
        if (_room != null && _tiles.ContainsKey(_room.SelfId)) FocusStream(_room.SelfId);
    }
    private void OpenMemberStream(string id)
    {
        var room = _room;
        var member = room?.Members.FirstOrDefault(m => m.Id == id);
        if (room == null || member?.Sharing != true) return;
        room.SetWatching(id, true);
        _audioPlayback.Allow(id);
        EnsureTile(id, member.Nick);
        FocusStream(id);
        UpdateRoomVisuals();
    }
    private void CloseFocusedStream(object sender, RoutedEventArgs e)
    {
        var room = _room;
        string? id = _focusedStream;
        if (room == null || id == null || id == room.SelfId) return;
        room.SetWatching(id, false);
        _audioPlayback.Remove(id);
        RemoveTile(id);
        UpdateRoomVisuals();
        SetStatus("Live fechada só para você. Clique no cartão com o avatar para assistir novamente.");
    }
    private void UpdateStageHeading()
    {
        var member = _room?.Members.FirstOrDefault(m => m.Id == _focusedStream);
        StageTitle.Text = member != null ? member.Id == _room?.SelfId ? "Sua transmissão" : "Tela de " + member.Nick : "Telas da sala";
        StageSubtitle.Text = member != null ? "Compartilhando com a sala" : _room == null ? "Entre em uma sala para começar" :
            _tiles.Count == 0 ? "Escolha uma live na lista ou compartilhe sua tela" : _tiles.Count + (_tiles.Count == 1 ? " transmissão aberta" : " transmissões abertas");
        StageAvatar.Avatar = member?.Avatar ?? _avatarId;
        FocusModeButton.IsEnabled = _tiles.Count > 0;
    }
}


