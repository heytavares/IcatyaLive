using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MoabLive.Models;
using MoabLive.Services;
using Forms = System.Windows.Forms;

namespace MoabLive;

public partial class MainWindow : Window
{
    private readonly ScreenCaptureService _capture = new();
    private readonly DispatcherTimer _renderTimer = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly CancellationTokenSource _lifetime = new();
    private CancellationTokenSource? _captureCancellation;
    private Task? _captureTask;
    private PeerSession? _peer;
    private VideoFrame? _latestFrame;
    private WriteableBitmap? _displayBitmap;
    private MainWindow? _testViewer;
    private bool _busy;
    private bool _answerApplied;
    private volatile bool _closing;
    private volatile bool _remotePaused;
    private volatile bool _renderEnabled;
    private long _frameCount;
    private long _lastFrameCount;
    private readonly Stopwatch _measurement = Stopwatch.StartNew();
    private DateTime _lastFrameAt;
    private int _displayWidth;
    private int _displayHeight;
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
        foreach (var screen in Forms.Screen.AllScreens)
            Displays.Items.Add($"{screen.DeviceName} · {screen.Bounds.Width} × {screen.Bounds.Height}");
        Displays.SelectedIndex = 0;
        Profiles.ItemsSource = CaptureProfile.Available;
        Profiles.SelectedIndex = 0;
        _renderTimer.Tick += RenderLatest;
        Closing += async (_, args) =>
        {
            if (_allowClose) return;
            args.Cancel = true;
            if (_closing) return;
            _closing = true;
            _lifetime.Cancel();
            await ResetAsync();
            _renderTimer.Stop();
            _lifetime.Dispose();
            _allowClose = true;
            Close();
        };
    }

    private void SetStatus(string text)
    {
        if (!_closing) Status.Text = text;
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
        OfferButton.IsEnabled = AcceptButton.IsEnabled = SoloButton.IsEnabled = !_busy && _peer == null;
        Incoming.IsEnabled = !_busy;
        AnswerButton.IsEnabled = !_busy && _peer is { IsSender: true, IsConnected: false } && !_answerApplied;
        DisconnectButton.IsEnabled = !_busy && _peer != null;
        CopyButton.IsEnabled = !_busy && !string.IsNullOrEmpty(Outgoing.Text);
        StartButton.IsEnabled = !_busy && !capturing && !receiving && (_peer == null || _peer.IsConnected);
        StopButton.IsEnabled = !_busy && capturing;
        Displays.IsEnabled = Profiles.IsEnabled = !_busy && !capturing && !receiving;
        StartButton.Content = _peer == null ? "Iniciar prévia local" : "Transmitir tela";
        CaptureTitle.Text = receiving ? "Tela recebida" : "Sua tela";
        CaptureHelp.Text = receiving ? "Aguardando quem transmite iniciar a captura." :
            "A captura só começa quando você clicar no botão abaixo.";
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
            UpdateControls();
        });
        peer.FrameReceived += frame =>
        {
            if (_peer == peer && !_remotePaused && _renderEnabled) QueueFrame(frame);
        };
        peer.RemotePaused += () => OnUi(() =>
        {
            if (_peer != peer) return;
            _remotePaused = true;
            ClearImage("O outro lado parou de compartilhar.");
            SetStatus("Compartilhamento pausado pelo outro lado.");
        });
        peer.RemoteResumed += () => OnUi(() =>
        {
            if (_peer != peer) return;
            _remotePaused = false;
            _renderTimer.Start();
            SetStatus("O outro lado iniciou a transmissão.");
        });
        peer.ConnectionLost += () => OnUi(async () =>
        {
            if (_peer != peer) return;
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
    });

    private async void WatchConnection(PeerSession peer)
    {
        try
        {
            await peer.Ready.WaitAsync(TimeSpan.FromSeconds(35), _lifetime.Token);
            if (_peer == peer) UpdateControls();
        }
        catch (TimeoutException)
        {
            if (_peer == peer) SetStatus("Não conectou em 35 segundos. Confira a resposta e o firewall; esta rede pode impedir conexão direta. Desconecte para tentar novamente.");
        }
        catch (OperationCanceledException) { }
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
        _renderEnabled = false;
        var peer = _peer;
        _peer = null;
        if (peer != null) await Task.Run(peer.Dispose);
        if (_testViewer is { _closing: false }) _testViewer.Close();
        _testViewer = null;
        Outgoing.Clear();
        Incoming.Clear();
        ClearImage("Sua tela aparece aqui");
    }

    private async void TestSolo(object sender, RoutedEventArgs e) => await RunActionAsync(async () =>
    {
        await StopCaptureAsync();
        SetStatus("Abrindo uma conexão local de teste…");
        var viewer = new MainWindow { Title = "MoabLive · Recebendo teste local", Width = 1040, Height = 800 };
        _testViewer = viewer;
        viewer.Show();
        var transmit = NewPeer(true, false);
        var receive = viewer.NewPeer(false, false);
        viewer.UpdateControls();
        try
        {
            var offer = await Task.Run(() => transmit.CreateOfferAsync(_lifetime.Token));
            var answer = await Task.Run(() => receive.AcceptOfferAsync(offer, _lifetime.Token));
            await Task.Run(() => transmit.ApplyAnswer(answer));
            await Task.WhenAll(transmit.Ready, receive.Ready).WaitAsync(TimeSpan.FromSeconds(20), _lifetime.Token);
            SetStatus("Teste local conectado. Clique em Transmitir tela para enviar à segunda janela.");
            viewer.SetStatus("Conexão local pronta. Inicie a transmissão na primeira janela.");
            viewer.UpdateControls();
        }
        catch { await ResetAsync(); throw; }
    });

    private async void StartPreview(object sender, RoutedEventArgs e) => await RunActionAsync(async () =>
    {
        var screens = Forms.Screen.AllScreens;
        if (Displays.SelectedIndex < 0 || Displays.SelectedIndex >= screens.Length)
            throw new InvalidOperationException("A lista de monitores mudou. Reabra o MoabLive.");
        var profile = (CaptureProfile)Profiles.SelectedItem;
        var peer = _peer;
        if (peer is { IsConnected: false }) throw new InvalidOperationException("Aguarde a conexão antes de transmitir.");
        _captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _renderEnabled = true;
        _renderTimer.Start();
        _frameCount = _lastFrameCount = 0;
        _measurement.Restart();
        peer?.SetSending(true);
        _captureTask = _capture.RunAsync(screens[Displays.SelectedIndex].Bounds, profile, frame =>
        {
            if (_captureCancellation?.IsCancellationRequested != false) return;
            QueueFrame(frame);
            peer?.SendFrame(frame, (uint)(1000 / profile.FramesPerSecond));
        }, _captureCancellation.Token);
        _ = ObserveCaptureAsync(_captureTask);
        SetStatus(peer == null ? "Prévia local ativa. Nenhuma imagem enviada." : "Transmitindo a tela ao outro PC. Use Parar captura para interromper.");
        await Task.CompletedTask;
    });
    private async Task ObserveCaptureAsync(Task task)
    {
        try { await task; }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_captureTask != task) return;
            _renderEnabled = false;
            _peer?.SetSending(false);
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
        ClearImage("Captura parada.");
    }

    private void QueueFrame(VideoFrame frame)
    {
        // A single-slot mailbox replaces an older unrendered frame; the UI cannot build a queue.
        Interlocked.Exchange(ref _latestFrame, frame);
        Interlocked.Increment(ref _frameCount);
    }
    private void RenderLatest(object? sender, EventArgs e)
    {
        var frame = Interlocked.Exchange(ref _latestFrame, null);
        if (_renderEnabled && frame != null)
        {
            if (_displayBitmap == null || _displayBitmap.PixelWidth != frame.Width || _displayBitmap.PixelHeight != frame.Height)
            {
                _displayBitmap = new WriteableBitmap(frame.Width, frame.Height, 96, 96, PixelFormats.Bgr24, null);
                Preview.Source = _displayBitmap;
            }
            _displayBitmap.WritePixels(new Int32Rect(0, 0, frame.Width, frame.Height), frame.Pixels, frame.Stride, 0);
            _displayWidth = frame.Width; _displayHeight = frame.Height;
            _lastFrameAt = DateTime.UtcNow;
            Placeholder.Visibility = Visibility.Collapsed;
        }
        if (_measurement.ElapsedMilliseconds < 1000) return;
        long count = Interlocked.Read(ref _frameCount);
        var fps = (count - _lastFrameCount) / _measurement.Elapsed.TotalSeconds;
        _lastFrameCount = count;
        _measurement.Restart();
        if (_renderEnabled && _lastFrameAt != default)
        {
            Metrics.Text = $"{(_peer is { IsSender: false } ? "Recebido" : "Capturado")} · {_displayWidth} × {_displayHeight} · {fps:F1} fps";
            if (_peer is { IsSender: false } && !_remotePaused && DateTime.UtcNow - _lastFrameAt > TimeSpan.FromSeconds(5))
                Metrics.Text = "Sem novos quadros há mais de 5 segundos. Verifique a conexão do outro lado.";
        }
    }
    private void ClearImage(string message)
    {
        _renderTimer.Stop();
        Interlocked.Exchange(ref _latestFrame, null);
        _displayBitmap = null;
        Preview.Source = null;
        PlaceholderText.Text = message;
        Placeholder.Visibility = Visibility.Visible;
        Metrics.Text = "Aguardando novos quadros.";
        _lastFrameAt = default;
    }
}


