using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Icatya.Models;
using Icatya.Services;
using Icatya.Services.Video;


namespace Icatya;

public partial class MainWindow : Window
{
    private readonly ScreenCaptureService _capture = new();
    private readonly PresentationClock _renderTimer = new();
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;
    private PeerSession? _peer;
    private readonly VideoFrameMailbox _frameMailbox = new();
    private readonly BitmapPresenter _presenter = new();
    private DurationMetric _presentTime = new();
    private bool _busy;
    private bool _answerApplied;
    private volatile bool _closing;
    private volatile bool _remotePaused;
    private volatile bool _renderEnabled;
    private long _frameCount;
    private long _lastFrameCount;
    private long _displayedFrames, _lastDisplayedFrames, _replacedFrames, _renderBusyFrames;
    private double _inputFps, _displayFps;
    private Controls.RoomVideoTile? _measuredRoomTile;
    private long _lastAudioUiAt;
    private readonly Stopwatch _measurement = Stopwatch.StartNew();
    private DateTime _lastFrameAt;
    private int _displayWidth;
    private int _displayHeight;
    private bool _allowClose;
    private readonly UiPreferencesStore _preferences = new();
    private double _sidebarWidth = 300;
    private bool _sidebarHidden;
    private bool _rememberLayout = true;
    private CaptureProfile _selectedProfile = CaptureProfile.Available[0];

    public MainWindow()
    {
        InitializeComponent();
        InitializeProfile();
        InitializeAudio();
        SourceInitialized += (_, _) => Services.Windowing.DarkTitleBar.Attach(this);
        var preferences = _preferences.Load();
        _sidebarWidth = preferences.SidebarWidth;
        _sidebarHidden = preferences.SidebarHidden;
        Loaded += async (_, _) => { ApplySidebarLayout(); await InitializeRoomAsync(); };
        UpdateControls();
        _renderTimer.Tick += RenderLatest;
        Closing += async (_, args) =>
        {
            if (_allowClose) return;
            args.Cancel = true;
            if (_closing) return;
            _closing = true;
            RoomPicker.IsOpen = false;
            SaveSidebarLayout();
            _lifetime.Cancel();
            // Defer even when ResetAsync completes synchronously: Close cannot run
            // recursively while WPF is still raising its first Closing event.
            await Dispatcher.Yield(DispatcherPriority.Background);
            await ResetAsync();
            _renderTimer.Stop();
            _audioPlayback.Dispose();
            _lifetime.Dispose();
            _allowClose = true;
            Close();
        };
    }

    private void SetStatus(string text)
    {
        if (!_closing) { Status.Text = text; SettingsStatus.Text = text; }
    }
    private void OpenSettings(object sender, RoutedEventArgs e)
    {
        RoomPicker.IsOpen = false;
        SettingsOverlay.Visibility = Visibility.Visible;
        Workspace.IsEnabled = false;
        SettingsClose.Focus();
    }
    private void CloseSettings(object sender, RoutedEventArgs e) => CloseSettingsPanel();
    private void CloseSettingsPanel()
    {
        SettingsOverlay.Visibility = Visibility.Collapsed;
        Workspace.IsEnabled = true;
        if (_sidebarHidden) HeaderSettingsButton.Focus(); else SettingsButton.Focus();
    }
    private void WindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && Keyboard.Modifiers == ModifierKeys.Control && SettingsOverlay.Visibility != Visibility.Visible)
        { ToggleRoomPicker(); e.Handled = true; }
        else if (e.Key == Key.Escape && RoomPicker.IsOpen)
        { RoomPicker.IsOpen = false; e.Handled = true; }
        else if (e.Key == Key.Escape && SettingsOverlay.Visibility == Visibility.Visible)
        {
            CloseSettingsPanel();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _focusedStream != null)
        {
            ClearStreamFocus();
            e.Handled = true;
        }
    }
    private void OnUi(Action action)
    {
        if (!_closing && !Dispatcher.HasShutdownStarted)
            Dispatcher.BeginInvoke(new Action(() => { if (!_closing) action(); }));
    }
    private void UpdateControls()
    {
        bool capturing = _captureTask is { IsCompleted: false };
        bool receiving = _peer is { IsSender: false };
        OfferButton.IsEnabled = AcceptButton.IsEnabled = !_busy && _peer == null && _room == null;
        Incoming.IsEnabled = !_busy;
        Incoming.IsReadOnly = receiving || _answerApplied;
        OfferButton.Visibility = receiving ? Visibility.Collapsed : Visibility.Visible;
        AcceptButton.Visibility = _peer is { IsSender: true } ? Visibility.Collapsed : Visibility.Visible;
        AnswerButton.Visibility = _peer is { IsSender: true } ? Visibility.Visible : Visibility.Collapsed;
        RoleHint.Text = _peer == null ? "Escolha: transmitir sua tela ou assistir a outro PC." :
            receiving ? "VOCÊ ASSISTE · devolva a resposta ao transmissor" :
            _answerApplied ? "VOCÊ TRANSMITE · resposta aplicada" : "VOCÊ TRANSMITE · envie o convite e aguarde a resposta";
        IncomingHint.Text = _peer is { IsSender: true } ? "Cole AQUI a resposta enviada pelo PC que vai assistir:" :
            receiving ? "Convite aceito. Copie a resposta gerada abaixo:" : "Para assistir, cole AQUI o convite recebido:";
        OutgoingHint.Text = receiving ? "Sua RESPOSTA: envie este texto ao PC que criou o convite." :
            _peer != null ? "Seu CONVITE: envie este texto ao PC que vai assistir." : "O texto gerado aparecerá aqui.";
        CopyButton.Content = receiving ? "Copiar resposta" : "Copiar convite";
        AnswerButton.IsEnabled = !_busy && _peer is { IsSender: true, IsConnected: false } && !_answerApplied && !string.IsNullOrWhiteSpace(Incoming.Text);
        DisconnectButton.IsEnabled = !_busy && _peer != null;
        CopyButton.IsEnabled = !_busy && !string.IsNullOrEmpty(Outgoing.Text);
        ShareButton.IsEnabled = !_busy && !receiving && (_peer == null || _peer.IsConnected);
        ShareButton.Visibility = StopButton.Visibility = receiving ? Visibility.Collapsed : Visibility.Visible;
        StopButton.IsEnabled = !_busy && capturing;
        ShareButton.ToolTip = capturing ? "Alterar tela, qualidade e FPS" : "Compartilhar tela / configurar qualidade";
        CaptureTitle.Text = receiving ? "Assistindo à transmissão" : capturing ? "Você está compartilhando sua tela" : "Seu compartilhamento está parado";
        LeaveButton.IsEnabled = !_busy && (_room != null || _peer != null);
        JoinRoomButton.IsEnabled = !_busy;
        UpdateParticipantVisuals();
    }
    private void UpdateParticipantVisuals()
    {
        HeaderConnectionStatus.Text = _room?.Connected == true || _peer?.IsConnected == true ? "Conectado" : _room != null || _peer != null ? "Conectando…" : "Fora da sala";
        ConnectionDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, _room?.Connected == true || _peer?.IsConnected == true ? "SuccessBrush" : "MutedText");
        if (_room != null) { UpdateRoomVisuals(); return; }
        bool connected = _peer?.IsConnected == true;
        bool capturing = _captureTask is { IsCompleted: false };
        RoomTitle.Text = _peer == null ? _roomIdentity != null ? "Sala salva" : "Sem sala" : "Conexão direta";
        RoomDescription.Text = connected ? "Conectado" : _peer != null ? "Aguardando conexão" : _roomIdentity != null ? "Entre para voltar à sala" : "Crie ou entre em uma sala";
        CopyRoomButton.IsEnabled = _roomIdentity != null;
        RemoteParticipant.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
        ParticipantsHint.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
        SelfLive.Visibility = capturing ? Visibility.Visible : Visibility.Collapsed;
        RemoteLive.Visibility = connected && _peer is { IsSender: false } && !_remotePaused && _lastFrameAt != default
            ? Visibility.Visible : Visibility.Collapsed;
        UpdateStageHeading();
    }

    private async Task RunActionAsync(Func<Task> action)
    {
        if (_busy || _closing) return;
        _busy = true;
        UpdateControls();
        try { await action(); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally { _busy = false; if (!_closing) UpdateControls(); }
    }

    private PeerSession NewPeer(bool sender, bool useStun)
    {
        var peer = new PeerSession(sender, useStun);
        _peer = peer;
        _answerApplied = false;
        _remotePaused = false;
        peer.StatusChanged += message => OnUi(() =>
        {
            if (_peer != peer) return;
            SetStatus(message);
            if (peer.IsConnected && !peer.IsSender)
            {
                _renderEnabled = true;
                if (!_remotePaused) _renderTimer.Start();
            }
            UpdateControls();
        });
        peer.AudioReceived += (sequence, packet) => { if (_peer == peer && !_remotePaused) _audioPlayback.AddPacket("manual", sequence, packet); };
        peer.FrameReceived += frame =>
        {
            if (_peer == peer && !_remotePaused && _renderEnabled) QueueFrame(frame);
        };
        peer.RemotePaused += () => OnUi(() =>
        {
            if (_peer != peer) return;
            _remotePaused = true;
            _audioPlayback.Remove("manual");
            ClearImage("O outro lado parou de compartilhar.");
            SetStatus("Compartilhamento pausado pelo outro lado.");
        });
        peer.RemoteResumed += () => OnUi(() =>
        {
            if (_peer != peer) return;
            _audioPlayback.Allow("manual");
            bool wasWaiting = _remotePaused || !_renderEnabled;
            _remotePaused = false;
            _renderEnabled = true;
            _renderTimer.Start();
            if (wasWaiting) SetStatus("O outro lado iniciou a transmissão.");
        });
        peer.ConnectionLost += () => OnUi(async () =>
        {
            if (_peer != peer) return;
            _audioPlayback.Remove("manual");
            await StopCaptureAsync();
            _renderEnabled = false;
            ClearImage("Conexão interrompida.");
            UpdateControls();
        });
        peer.Error += message => OnUi(async () =>
        {
            if (_peer != peer) return;
            await StopCaptureAsync();
            SetStatus(message);
            if (peer.IsConnected && !peer.IsSender)
            {
                _renderEnabled = true;
                if (!_remotePaused) _renderTimer.Start();
            }
            UpdateControls();
        });
        if (!sender) { _renderEnabled = true; _renderTimer.Start(); }
        return peer;
    }

    private async void CreateOffer(object sender, RoutedEventArgs e) => await RunActionAsync(async () =>
    {
        await StopCaptureAsync();
        SetStatus("Preparando convite e consultando a rede…");
        var peer = NewPeer(true, true);
        try
        {
            Outgoing.Text = await Task.Run(() => peer.CreateOfferAsync(_lifetime.Token));
            SetStatus("Envie o convite ao amigo. Depois cole e aplique a resposta dele nesta janela.");
        }
        catch { await ResetAsync(); throw; }
    });

    private async void AcceptOffer(object sender, RoutedEventArgs e) => await RunActionAsync(async () =>
    {
        var code = Incoming.Text;
        var invite = InviteCodec.Decode(code);
        if (invite.Kind != "offer") throw new InvalidOperationException("Cole um convite, não uma resposta.");
        await StopCaptureAsync();
        SetStatus("Preparando resposta…");
        var peer = NewPeer(false, true);
        try
        {
            Outgoing.Text = await Task.Run(() => peer.AcceptOfferAsync(code, _lifetime.Token));
            SetStatus("Envie esta resposta a quem criou o convite e mantenha esta janela aberta.");
            WatchConnection(peer);
        }
        catch { await ResetAsync(); throw; }
    });

    private async void ApplyAnswer(object sender, RoutedEventArgs e) => await RunActionAsync(async () =>
    {
        var peer = _peer ?? throw new InvalidOperationException("Crie um convite primeiro.");
        var code = Incoming.Text;
        await Task.Run(() => peer.ApplyAnswer(code));
        _answerApplied = true;
        SetStatus("Resposta aplicada. Tentando conectar…");
        WatchConnection(peer);
        CloseSettingsPanel();
    });

    private async void WatchConnection(PeerSession peer)
    {
        try
        {
            await peer.Ready.WaitAsync(TimeSpan.FromSeconds(peer.IsSender ? 40 : PeerSession.ManualSignalingTimeoutSeconds + 10), _lifetime.Token);
            if (_peer == peer) UpdateControls();
        }
        catch (TimeoutException)
        {
            if (_peer == peer) SetStatus("Ainda não conectou. Confirme que a resposta foi aplicada no PC que criou o convite. Copie o diagnóstico se o problema continuar.");
        }
        catch (OperationCanceledException) { }
    }

    private void IncomingChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (IsLoaded) UpdateControls();
    }
    private void CopyDiagnostic(object sender, RoutedEventArgs e)
    {
        try
        {
            var diagnostic = _peer?.DiagnosticSummary() ?? "iCatya 0.7.3: nenhuma sessão aberta.\n";
            diagnostic += _peer?.PerformanceSummary();
            diagnostic += _room?.PerformanceSummary();
            diagnostic += _room?.DiagnosticSummary();
            diagnostic += $"Apresentações de telas da sala: {_tiles.Values.Sum(tile => tile.PresentedFrames)}\nTentativas com apresentação ocupada: {_tiles.Values.Sum(tile => tile.BusyPresentationAttempts)}\n";
            diagnostic += $"Capacidade gráfica WPF: {System.Windows.Media.RenderCapability.Tier >> 16}; modo: {System.Windows.Media.RenderOptions.ProcessRenderMode}; sessão remota: {SystemParameters.IsRemoteSession}\n";
            var roomRender = _roomTimer.CallbackTime.Snapshot();
            diagnostic += $"Ciclos de composição da sala: {_roomTimer.TickCount}; trabalho médio/máximo: {roomRender.AverageMs:F2}/{roomRender.MaximumMs:F2} ms\n";
            foreach (var tile in _tiles) diagnostic += $"Tela {tile.Key[..Math.Min(8, tile.Key.Length)]}: " + tile.Value.PresentationSummary();
            diagnostic += $"Exibição habilitada: {_renderEnabled}\nPausa informada pelo outro lado: {_remotePaused}\n";
            diagnostic += $"FPS processados/recebidos: {_inputFps:F1}\nFPS exibidos: {_displayFps:F1}\nQuadros substituídos antes de exibir: {Interlocked.Read(ref _replacedFrames)}\nQuadros pulados com apresentação ocupada: {_renderBusyFrames}\n";
            var capture = _capture.CaptureTime.Snapshot();
            var presentation = _presentTime.Snapshot();
            diagnostic += $"Captura média/máxima: {capture.AverageMs:F2}/{capture.MaximumMs:F2} ms\nQuadros substituídos antes de codificar: {_capture.DroppedFrames}\nApresentação média/máxima: {presentation.AverageMs:F2}/{presentation.MaximumMs:F2} ms\n";
            System.Windows.Clipboard.SetText(diagnostic);
            SetStatus("Diagnóstico copiado. Ele não inclui convites, senhas ou endereços IP.");
        }
        catch (Exception) { SetStatus("Não foi possível copiar o diagnóstico. Tente novamente."); }
    }
    private async void CopyCode(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText(Outgoing.Text); SetStatus("Texto copiado. Envie ao outro lado pelo chat."); }
        catch (Exception) { Outgoing.SelectAll(); Outgoing.Focus(); SetStatus("Use Ctrl+C para copiar o texto selecionado."); }
        await Task.CompletedTask;
    }
    private async void Disconnect(object sender, RoutedEventArgs e) => await RunActionAsync(async () =>
    {
        await ResetAsync();
        SetStatus("Desconectado. Você pode iniciar uma nova sessão.");
    });

    private async Task ResetAsync()
    {
        await StopCaptureAsync();
        await LeaveRoomAsync();
        _audioPlayback.Clear();
        _renderEnabled = false;
        var peer = _peer;
        _peer = null;
        if (peer != null) await Task.Run(peer.Dispose);
        Outgoing.Clear();
        Incoming.Clear();
        ClearImage("Sua tela aparece aqui");
    }

    private async void OpenShareSettings(object sender, RoutedEventArgs e)
    {
        if (!ShareButton.IsEnabled) return;
        var settings = new ShareSettingsWindow(_selectedSource, _selectedProfile, _selectedAudio,
            _captureTask is { IsCompleted: false }, _peer?.IsConnected == true || _room?.Connected == true) { Owner = this };
        if (settings.ShowDialog() != true || _closing) return;
        await RunActionAsync(async () =>
        {
            _selectedSource = settings.SelectedSource;
            _selectedAudio = settings.SelectedAudio;
            _selectedProfile = settings.SelectedProfile;
            await StopCaptureAsync();
            await StartCaptureAsync();
        });
    }

    private async Task StartCaptureAsync()
    {
        var source = _selectedSource ?? throw new InvalidOperationException("Escolha um aplicativo ou uma tela para compartilhar.");
        source.Validate();
        var profile = _selectedProfile;
        var peer = _peer;
        var room = _room;
        if (room != null)
        {
            if (!room.Connected) throw new InvalidOperationException("Aguarde a conexão com a sala antes de compartilhar.");
            SetStatus("Preparando o vídeo da sala…");
            await Task.Run(() => room.ConfigureVideo(profile));
            VideoEngine.Text = "Compressão: " + room.EncodingDescription;
        }
        if (peer is { IsConnected: false }) throw new InvalidOperationException("Aguarde a conexão antes de transmitir.");
        if (peer != null)
        {
            SetStatus("Preparando o codificador de vídeo…");
            await Task.Run(() => peer.ConfigureVideo(profile));
            VideoEngine.Text = "Compressão: " + peer.EncodingDescription;
            VideoEngine.ToolTip = string.IsNullOrEmpty(peer.FallbackReason) ? null : peer.FallbackReason;
        }
        else if (room == null) VideoEngine.Text = "Prévia local · sem compressão ou envio";
        _captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _renderEnabled = true;
        _renderTimer.Start();
        _frameCount = _lastFrameCount = 0;
        ResetDisplayMeasurements();
        _measurement.Restart();
        peer?.SetSending(true);
        room?.SetSharing(true);
        _captureTask = _capture.RunAsync(source, profile, frame =>
        {
            if (_captureCancellation?.IsCancellationRequested != false) return;
            if (room == null) QueueFrame(frame);
            else Interlocked.Increment(ref _frameCount);
            peer?.SendFrame(frame);
            room?.SendFrame(frame);
            if (room != null) QueueRoomFrame(room, room.SelfId, frame);
        }, _captureCancellation.Token);
        StartAudio();
        _ = ObserveCaptureAsync(_captureTask);
        SetStatus(room != null ? "Compartilhando com a sala. Use o botão vermelho para parar." : peer == null ? "Prévia local ativa. Nenhuma imagem enviada." : "Transmitindo a tela ao outro PC. Use Parar captura para interromper.");
        await Task.CompletedTask;
    }

    private void ToggleSidebar(object sender, RoutedEventArgs e)
    {
        _sidebarHidden = !_sidebarHidden;
        ApplySidebarLayout();
        SaveSidebarLayout();
    }

    private void ContentSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsLoaded) ApplySidebarLayout();
    }

    private void ApplySidebarLayout()
    {
        Sidebar.Visibility = SidebarDivider.Visibility = _sidebarHidden ? Visibility.Collapsed : Visibility.Visible;
        ProfileCard.Visibility = _sidebarHidden ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.MinWidth = _sidebarHidden ? 0 : 220;
        SidebarColumn.MaxWidth = _sidebarHidden ? 0 : Math.Max(220, ContentGrid.ActualWidth - 272);
        SidebarColumn.Width = new GridLength(_sidebarHidden ? 0 : Math.Min(_sidebarWidth, SidebarColumn.MaxWidth));
        DividerColumn.Width = new GridLength(_sidebarHidden ? 0 : 12);
        var label = _sidebarHidden ? "Mostrar barra lateral" : "Ocultar barra lateral";
        SidebarToggle.ToolTip = label;
        HeaderSettingsButton.Visibility = _sidebarHidden ? Visibility.Visible : Visibility.Collapsed;
        System.Windows.Automation.AutomationProperties.SetName(SidebarToggle, label);
    }

    private void SidebarResized(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
    {
        if (_sidebarHidden) return;
        _sidebarWidth = SidebarColumn.ActualWidth;
        SaveSidebarLayout();
    }

    private void SaveSidebarLayout()
    {
        if (_rememberLayout && !_preferences.Save(new(_sidebarWidth, _sidebarHidden, _audioPlayback.Volume)))
            SetStatus("Não foi possível salvar o tamanho da barra lateral. Você pode continuar usando o aplicativo.");
    }
    private async Task ObserveCaptureAsync(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_captureTask != task) return;
            _renderEnabled = false;
            _peer?.SetSending(false);
            _room?.SetSharing(false);
            await StopAudioAsync();
            ClearImage("Captura interrompida.");
            SetStatus("Falha na captura: " + ex.Message);
        }
        if (!_closing) UpdateControls();
    }
    private async void StopPreview(object sender, RoutedEventArgs e) => await RunActionAsync(async () =>
    {
        await StopCaptureAsync();
        SetStatus("Captura parada. Nenhuma imagem nova é enviada.");
    });
    private async Task StopCaptureAsync()
    {
        _renderEnabled = false;
        _captureCancellation?.Cancel();
        await StopAudioAsync();
        if (_captureTask != null)
        {
            try { await _captureTask; }
            catch (OperationCanceledException) { }
            catch (Exception) { /* Reported by ObserveCaptureAsync. */ }
        }
        _captureTask = null;
        _captureCancellation?.Dispose();
        _captureCancellation = null;
        if (_peer is { IsSender: true } peer) await Task.Run(() => peer.SetSending(false));
        _room?.SetSharing(false);
        ClearImage("Captura parada.");
    }

    private void QueueFrame(VideoFrame frame)
    {
        // A single-slot mailbox replaces an older unrendered frame; the UI cannot build a queue.
        if (_frameMailbox.Queue(frame)) Interlocked.Increment(ref _replacedFrames);
        Interlocked.Increment(ref _frameCount);
    }
    private void RenderLatest(object? sender, EventArgs e)
    {
        long now = Stopwatch.GetTimestamp();
        if (_lastAudioUiAt == 0 || Stopwatch.GetElapsedTime(_lastAudioUiAt, now).TotalMilliseconds >= 100)
        {
            RefreshAudioStatus(); _lastAudioUiAt = now;
        }
        if (_room != null)
        {
            var visibleTile = (_focusedStream == null ? null : _tiles.GetValueOrDefault(_focusedStream)) ??
                _tiles.GetValueOrDefault(_room.SelfId) ?? _tiles.Values.FirstOrDefault();
            if (visibleTile != null && visibleTile.SourceWidth > 0)
            {
                if (!ReferenceEquals(_measuredRoomTile, visibleTile))
                { _measuredRoomTile = visibleTile; _lastDisplayedFrames = visibleTile.PresentedFrames; }
                if (_displayedFrames != visibleTile.PresentedFrames) _lastFrameAt = DateTime.UtcNow;
                _displayedFrames = visibleTile.PresentedFrames;
                _displayWidth = visibleTile.SourceWidth;
                _displayHeight = visibleTile.SourceHeight;
            }
        }
        using var frame = _frameMailbox.Take();
        if (_renderEnabled && frame != null)
        {
            long started = Stopwatch.GetTimestamp();
            if (_presenter.TryPresent(frame))
            {
                if (Preview.Source != _presenter.Bitmap) Preview.Source = _presenter.Bitmap;
                _displayedFrames++;
                _presentTime.Record(started);
                _displayWidth = frame.Width; _displayHeight = frame.Height;
                _lastFrameAt = DateTime.UtcNow;
                Placeholder.Visibility = Visibility.Collapsed;
                if (_peer is { IsSender: false, IsConnected: true } && !_remotePaused)
                    RemoteLive.Visibility = Visibility.Visible;
            }
            else { _renderBusyFrames++; _frameMailbox.Retry(frame); }
        }
        if (_measurement.ElapsedMilliseconds < 1000) return;
        long count = Interlocked.Read(ref _frameCount);
        _inputFps = (count - _lastFrameCount) / _measurement.Elapsed.TotalSeconds;
        _displayFps = (_displayedFrames - _lastDisplayedFrames) / _measurement.Elapsed.TotalSeconds;
        _lastFrameCount = count;
        _lastDisplayedFrames = _displayedFrames;
        _measurement.Restart();
        if ((_renderEnabled || _room != null) && _lastFrameAt != default)
        {
            string input = _room != null && _captureTask is not { IsCompleted: false } ? "" :
                $"{(_peer is { IsSender: false } ? "recebidos" : "processados")} {_inputFps:F1} fps · ";
            Metrics.Text = $"{_displayWidth} × {_displayHeight} · {input}exibidos {_displayFps:F1} fps";
            if (_peer is { IsSender: false } && !_remotePaused && DateTime.UtcNow - _lastFrameAt > TimeSpan.FromSeconds(5))
                Metrics.Text = "Sem novos quadros há mais de 5 segundos. Verifique a conexão do outro lado.";
        }
    }
    private void ClearImage(string message)
    {
        _renderTimer.Stop();
        _frameMailbox.Clear();
        _presenter.Clear();
        Preview.Source = null;
        PlaceholderText.Text = message;
        Placeholder.Visibility = Visibility.Visible;
        Metrics.Text = "Aguardando novos quadros.";
        _lastFrameAt = default;
        ResetDisplayMeasurements();
        UpdateParticipantVisuals();
    }
    private void ResetDisplayMeasurements()
    {
        Interlocked.Exchange(ref _frameCount, 0);
        _lastFrameCount = _displayedFrames = _lastDisplayedFrames = _renderBusyFrames = 0;
        Interlocked.Exchange(ref _replacedFrames, 0);
        _inputFps = _displayFps = 0;
        _measuredRoomTile = null; _lastAudioUiAt = 0;
        _presentTime = new();
        _measurement.Restart();
    }
}
