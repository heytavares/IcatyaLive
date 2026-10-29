using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Icatya.Models;
using Icatya.Services;
using Icatya.Services.Video;

internal static class BackgroundChecks
{
    private static void Require(bool value,string text){if(!value)throw new Exception(text);}
    public static async Task<int> RunAsync()
    {
        try {
            using(var mailbox=new VideoFrameMailbox()) {
                using var first=VideoFrame.Rent(16,16);using var newest=VideoFrame.Rent(16,16);
                mailbox.Queue(first);using(var taken=mailbox.Take()!) {
                    Require(mailbox.Retry(taken),"Não preservou quadro ocupado para o próximo desenho.");
                }
                using(var retry=mailbox.Take()!) {
                    mailbox.Queue(newest);Require(!mailbox.Retry(retry),"Quadro antigo substituiu um mais novo.");
                }
                using var result=mailbox.Take();Require(ReferenceEquals(result,newest),"Não apresentou o quadro mais recente.");
                mailbox.Dispose();Require(!mailbox.Retry(first)&&mailbox.Take()==null,"Aceitou quadro depois de encerrar.");
            }
            Console.WriteLine("PASS mailbox: retenção após apresentação ocupada, preferência pelo mais recente e encerramento");
            string Json(string version="0.7.1",string name="Icatya-0.7.1-Windows-x64.zip",string host="github.com",string digest="sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") =>
                System.Text.Json.JsonSerializer.Serialize(new{draft=false,prerelease=false,tag_name="v"+version,assets=new[]{new{name,state="uploaded",size=1000,browser_download_url=$"https://{host}/heytavares/IcatyaLive/releases/download/v{version}/{name}",digest}}});
            var installed=new Version(0,7,0);Require(UpdateService.Parse(Json(),installed)?.Version==new Version(0,7,1),"Não identificou versão mais nova.");
            Require(UpdateService.Parse(Json().Replace("v0.7.1", "V0.7.1"),installed)?.Version==new Version(0,7,1),"Não aceitou prefixo V maiúsculo usado na Release.");
            Require(UpdateService.Parse(Json(),new Version(0,7,1))==null,"Ofereceu reinstalar a mesma versão.");
            Require(UpdateService.Parse(Json(),new Version(0,8,0))==null,"Ofereceu downgrade.");
            foreach(var invalid in new[]{Json(name:"wrong.zip"),Json(host:"example.com"),Json(digest:""),Json(version:"0.7.1-beta"),"{}"}) {
                bool rejected=false;try{UpdateService.Parse(invalid,installed);}catch(InvalidOperationException){rejected=true;}
                Require(rejected,"Aceitou Release/pacote inválido.");
            }
            Console.WriteLine("PASS atualizador: versão nova, igualdade/downgrade, pacote ausente, origem e SHA256");
            using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(35));
            var live=await new UpdateService().CheckAsync(new Version(0,0,0),stop.Token);
            Require(live!=null,"Não encontrou Release pública para teste.");
            Console.WriteLine($"PASS consulta real ao GitHub: {live!.Version}, {live.PackageName}, SHA256 presente; nenhum download/instalação");
            CheckUpdateButtonWithoutShowingWindow();
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static void CheckUpdateButtonWithoutShowingWindow()
    {
        Exception? failure=null;
        string? previous=Environment.GetEnvironmentVariable("ICATYA_DATA_DIRECTORY");
        Environment.SetEnvironmentVariable("ICATYA_DATA_DIRECTORY",System.IO.Path.Combine(AppContext.BaseDirectory,"checks","headless-update-"+Guid.NewGuid().ToString("N")));
        var ui=new Thread(()=> {
            try {
                var app=new System.Windows.Application { ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown };
                SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(System.Windows.Threading.Dispatcher.CurrentDispatcher));
                app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary { Source=new Uri("/Icatya;component/Themes/DarkTheme.xaml",UriKind.Relative) });
                var main=new Icatya.MainWindow(); // Never Show/ShowDialog; no HWND or mouse automation.
                var button=(System.Windows.Controls.Button)main.FindName("UpdateButton");
                var status=(System.Windows.Controls.TextBlock)main.FindName("UpdateStatus");
                button.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                Require(!button.IsEnabled && status.Text.Contains("Consultando"),"Botão não iniciou consulta assíncrona.");
                var frame=new System.Windows.Threading.DispatcherFrame();
                var watch=Stopwatch.StartNew();
                var timer=new System.Windows.Threading.DispatcherTimer { Interval=TimeSpan.FromMilliseconds(50) };
                timer.Tick+=(_,_)=> { if(button.IsEnabled||watch.Elapsed.TotalSeconds>35){timer.Stop();frame.Continue=false;} };
                timer.Start();System.Windows.Threading.Dispatcher.PushFrame(frame);
                Require(button.IsEnabled && (status.Text.Contains("atualizado")||status.Text.Contains("disponível")),"Consulta do botão falhou: "+status.Text);
                app.Shutdown();
            }catch(Exception ex){failure=ex;}
        });
        ui.SetApartmentState(ApartmentState.STA);ui.Start();ui.Join();
        Environment.SetEnvironmentVariable("ICATYA_DATA_DIRECTORY",previous);
        if(failure!=null)throw new Exception("Falha no botão de atualizar (sem janela).",failure);
        Console.WriteLine("PASS botão real: consulta assíncrona e resultado inline, sem mostrar janela ou executar instalador");
    }
    public static async Task<int> BenchmarkAsync()
    {
        try {
            Process.GetCurrentProcess().PriorityClass=ProcessPriorityClass.BelowNormal;
            foreach(var profile in CaptureProfile.Available) {
                using var sender=new PeerSession(true,false);using var receiver=new PeerSession(false,false);
                int received=0;receiver.FrameReceived+=_=>Interlocked.Increment(ref received);
                sender.ApplyAnswer(await receiver.AcceptOfferAsync(await sender.CreateOfferAsync()));
                await Task.WhenAll(sender.Ready,receiver.Ready).WaitAsync(TimeSpan.FromSeconds(15));sender.ConfigureVideo(profile);sender.SetSending(true);
                using var frame=VideoFrame.Rent(profile.Width,profile.Height);
                // Synthetic moving color blocks; no desktop/window capture or UI.
                for(int y=0;y<frame.Height;y++)for(int x=0;x<frame.Width;x++) {
                    int i=y*frame.Stride+x*3;frame.Pixels[i]=(byte)(x/16*13);frame.Pixels[i+1]=(byte)(y/16*19);frame.Pixels[i+2]=(byte)((x+y)/16*7);
                }
                var watch=Stopwatch.StartNew();int sent=0;
                using var stop=new CancellationTokenSource();using var pacer=new FramePacer(profile.FramesPerSecond,stop.Token);
                while(watch.Elapsed.TotalSeconds<3) {
                    int x=sent*19%(frame.Width-64);
                    for(int row=30;row<94;row++)Array.Fill(frame.Pixels,(byte)(sent%2==0?230:20),row*frame.Stride+x*3,192);
                    sender.SendFrame(frame);sent++;pacer.WaitNext();
                }
                watch.Stop();sender.SetSending(false);await Task.Delay(150);
                var encode=sender.EncodeTime.Snapshot();var network=sender.SendTime.Snapshot();var decode=receiver.DecodeTime.Snapshot();
                Console.WriteLine($"SYNTHETIC {profile.Width}x{profile.Height}@{profile.FramesPerSecond}: sent {sent/watch.Elapsed.TotalSeconds:F1}, received {received/watch.Elapsed.TotalSeconds:F1} fps; encode {encode.AverageMs:F2} ms; send {network.AverageMs:F2} ms; decode {decode.AverageMs:F2} ms; {sender.EncodingDescription}");
                Require(received>10,"Não recebeu vídeo sintético suficiente.");
            }
            Console.WriteLine("BENCHMARK OK. Não inclui captura real, composição WPF nem redes externas; PC em uso.");return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
}
