using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Icatya.Models;
using Icatya.Services;
using Icatya.Services.Audio;
using Icatya.Services.Video;
using NAudio.CoreAudioApi;
using NAudio.Wave;

internal static class AudioChecks
{
    private static void Require(bool condition,string message) { if(!condition)throw new Exception(message); }
    public static int Source(string[] args)
    {
        int index=Array.IndexOf(args,"--audio-source");
        int frequency=int.Parse(args[index+1]);string marker=args[index+2];
        Exception? error=null;
        var thread=new Thread(()=>{
            try {
                using var output=new WasapiOut(AudioClientShareMode.Shared,true,50);
                output.Init(new Tone(frequency));output.Play();
                var window=new Window{Title="iCatya • synthetic source "+frequency,Width=360,Height=240,Left=110,Top=110,ShowActivated=false,Background=frequency==730?Brushes.Red:Brushes.Blue};
                bool resized=false;
                var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
                timer.Tick+=(_,_)=>{if(!resized&&File.Exists(marker+".resize")){resized=true;window.Width=540;window.Height=300;}if(File.Exists(marker+".stop"))window.Close();};
                window.Loaded+=(_,_)=>{File.WriteAllText(marker,"ready");timer.Start();};
                window.Closed+=(_,_)=>{timer.Stop();Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);};
                window.Show();Dispatcher.Run();output.Stop();
            }catch(Exception ex){error=ex;File.WriteAllText(marker,ex.ToString());}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();thread.Join();
        if(error!=null){Console.Error.WriteLine(error);return 1;}return 0;
    }
    private sealed class Tone(int frequency):ISampleProvider
    {
        private long _position;
        public WaveFormat WaveFormat{get;}=WaveFormat.CreateIeeeFloatWaveFormat(48000,2);
        public int Read(float[] buffer,int offset,int count){for(int i=0;i<count;i+=2){float sample=(float)Math.Sin(2*Math.PI*frequency*_position++/48000)*0.008f;buffer[offset+i]=sample;if(i+1<count)buffer[offset+i+1]=sample;}return count;}
    }
    public static async Task<int> RunAsync(bool checkWindow = false)
    {
        try {
            var fifo=new PcmBuffer(6);fifo.Write(new float[]{1,2,3,4});fifo.Write(new float[]{5,6,7,8});var read=new float[6];fifo.Read(read);
            Require(read.SequenceEqual(new float[]{3,4,5,6,7,8}),"PCM não descarta dados antigos.");
            var mix=new float[4];AudioCaptureMixer.Mix(new float[]{.8f,-.8f,.4f,-.4f},mix,.5f);Require(Math.Abs(mix[0]-.4f)<.0001,"Volume incorreto.");
            AudioCaptureMixer.Mix(new float[]{1,1,1,1},mix,0);Require(Math.Abs(mix[0]-.4f)<.0001,"Volume zero vazou áudio.");
            using var encoder=new OpusAudioEncoder();using var decoder=new OpusAudioDecoder();
            var samples=new float[1920];for(int i=0;i<960;i++){samples[i*2]=(float)Math.Sin(i*.1)*.3f;samples[i*2+1]=0;}
            float[] decoded=Array.Empty<float>();for(int i=0;i<8;i++)decoded=decoder.Decode(encoder.Encode(samples));
            Require(decoded.Length==1920 && decoded.Where((_,i)=>i%2==0).Sum(v=>v*v)>10,"Opus estéreo não decodificou.");
            Require(decoded.Where((_,i)=>i%2==1).Sum(v=>v*v)<.1,"Opus misturou os canais.");
            Console.WriteLine("PASS PCM limitado, volumes, silêncio e Opus estéreo 48 kHz");
            await PeerAudioAsync();
            await NativeSourcesAsync(checkWindow);
            return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    private static async Task PeerAudioAsync()
    {
        using var sender=new PeerSession(true,false);using var receiver=new PeerSession(false,false);
        int packets=0;var received=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.AudioReceived+=(seq,data)=>{Interlocked.Increment(ref packets);if(data.Length>0)received.TrySetResult();};
        var offer=await sender.CreateOfferAsync();var answer=await receiver.AcceptOfferAsync(offer);sender.ApplyAnswer(answer);
        await Task.WhenAll(sender.Ready,receiver.Ready).WaitAsync(TimeSpan.FromSeconds(15));
        sender.SetSending(true);using var encoder=new OpusAudioEncoder();var pcm=new float[1920];
        for(int i=0;i<30&&!received.Task.IsCompleted;i++){sender.SendAudioEncoded(encoder.Encode(pcm));await Task.Delay(30);}
        await received.Task.WaitAsync(TimeSpan.FromSeconds(3));
        sender.SetSending(false);await Task.Delay(150);int before=packets;
        for(int i=0;i<10;i++)sender.SendAudioEncoded(encoder.Encode(pcm));await Task.Delay(150);
        Require(packets==before,"Enviou áudio depois de parar.");
        using var playback=new AudioPlayback{Volume=1};
        playback.Allow("test");
        for(int i=0;i<960;i++) { pcm[2*i]=(float)Math.Sin(2*Math.PI*730*i/48000)*.3f;pcm[2*i+1]=pcm[2*i]; }
        for(ushort i=0;i<6;i++)playback.AddPacket("test",i,encoder.Encode(pcm),false);
        var output=new float[1920];playback.Read(output,0,output.Length);Require(output.Sum(x=>x*x)>.1,"Reprodução não decodificou áudio.");
        playback.Volume=0;playback.Read(output,0,output.Length);Require(output.All(x=>x==0),"Mute não silenciou saída.");
        playback.Remove("test");playback.Volume=1;
        for(ushort i=6;i<12;i++)playback.AddPacket("test",i,encoder.Encode(pcm),false);
        playback.Read(output,0,output.Length);Require(output.All(x=>x==0),"Pacote atrasado recriou uma live encerrada.");
        Console.WriteLine("PASS Opus sobre WebRTC local, parada de envio, reprodução/mute e descarte após sair");
    }
    private static async Task NativeSourcesAsync(bool checkWindow)
    {
        string directory=Path.Combine(AppContext.BaseDirectory,"checks","audio-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        var processes=new List<(Process Process,string Marker)>();
        try {
            foreach(int tone in new[]{730,1470}){
                string marker=Path.Combine(directory,tone+".ready");
                var start=new ProcessStartInfo(@"C:\Program Files\dotnet\dotnet.exe"){UseShellExecute=false,CreateNoWindow=true};
                start.ArgumentList.Add(typeof(AudioChecks).Assembly.Location);start.ArgumentList.Add("--audio-source");start.ArgumentList.Add(tone.ToString());start.ArgumentList.Add(marker);
                var process=Process.Start(start)!;processes.Add((process,marker));
                var timeout=Stopwatch.StartNew();while(!File.Exists(marker)&&!process.HasExited&&timeout.Elapsed.TotalSeconds<15)await Task.Delay(100);
                Require(File.Exists(marker)&&File.ReadAllText(marker)=="ready","Fonte sintética não iniciou: "+(File.Exists(marker)?File.ReadAllText(marker):"timeout"));
            }
            var selected=processes[0].Process;
            var app=new AudioApplication(selected.Id,selected.StartTime.ToUniversalTime().Ticks,"synthetic-selected");
            var route=new AudioCaptureRoute(new(app));using var stop=new CancellationTokenSource();
            var task=ProcessLoopbackCapture.RunAsync(route,stop.Token);
            var pcm=new List<float>();var block=new float[1920];
            for(int i=0;i<120;i++){await Task.Delay(20);int count=route.Buffer.Read(block);if(i>15)pcm.AddRange(block.Take(count));if(task.IsFaulted)await task;}
            stop.Cancel();await task;
            double Magnitude(int hz){double a=0,b=0;int n=pcm.Count/2;for(int i=0;i<n;i++){double angle=2*Math.PI*hz*i/48000;a+=pcm[2*i]*Math.Cos(angle);b+=pcm[2*i]*Math.Sin(angle);}return Math.Sqrt(a*a+b*b)/Math.Max(1,n);}
            double wanted=Magnitude(730),excluded=Magnitude(1470);
            Console.WriteLine($"Isolation selected={wanted:F7} excluded={excluded:F7} samples={pcm.Count}");
            Require(wanted>.0003&&excluded<wanted*.12,"Falha no isolamento do áudio de processos.");
            Console.WriteLine("PASS captura real: aplicativo selecionado presente, segundo aplicativo excluído");
            if (!checkWindow) {
                var closingRoute=new AudioCaptureRoute(new(app));using var closingStop=new CancellationTokenSource();
                var closingTask=ProcessLoopbackCapture.RunAsync(closingRoute,closingStop.Token);
                await Task.Delay(250);File.WriteAllText(processes[0].Marker+".stop","");await selected.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                bool closed=false;
                try { await closingTask.WaitAsync(TimeSpan.FromSeconds(3)); }
                catch(InvalidOperationException) { closed=true; }
                finally { closingStop.Cancel(); }
                Require(closed&&closingRoute.Buffer.Count==0&&closingRoute.Peak==0,"Fonte fechada não foi interrompida/limpa.");
                Console.WriteLine("PASS fechamento da fonte interrompe áudio e limpa buffers sem trocar de aplicativo");
                return;
            }
            var source=SourceCatalog.Windows().First(w=>w.ProcessId==selected.Id);
            await Task.Run(async()=>{
                using var capture=new WindowCapture(source);using var frame=VideoFrame.Rent(1280,720);bool valid=false;
                for(int i=0;i<70;i++){if(capture.TryCopyTo(frame)){int center=360*frame.Stride+640*3;valid=frame.Pixels[center+2]>220&&frame.Pixels[center]<30;if(valid)break;}await Task.Delay(30);}
                Require(valid,"Captura da janela selecionada não preservou o vermelho sob outra janela azul.");
                File.WriteAllText(processes[0].Marker+".resize","");bool resized=false;
                for(int i=0;i<100;i++) {
                    if(capture.TryCopyTo(frame)) {int edge=360*frame.Stride+40*3;resized=frame.Pixels[edge+2]>220&&frame.Pixels[edge]<30;if(resized)break;}
                    await Task.Delay(30);
                }
                Require(resized,"Captura não acompanhou a nova largura da janela.");
                File.WriteAllText(processes[0].Marker+".stop","");await selected.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
                bool stopped=false;
                try {capture.TryCopyTo(frame);}catch(InvalidOperationException){stopped=true;}catch(ArgumentException){stopped=true;}
                Require(stopped,"Janela fechada não interrompeu a captura.");
            });
            Console.WriteLine("PASS captura WGC: janela isolada sob oclusão, redimensionamento e fechamento sem trocar fonte");
        }finally{
            foreach(var item in processes){File.WriteAllText(item.Marker+".stop","");try{await item.Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));}catch(TimeoutException){item.Process.Kill();}item.Process.Dispose();}
        }
    }
}



