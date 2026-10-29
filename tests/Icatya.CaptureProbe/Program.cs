using System;
using System.IO;
using System.Threading;
using System.Windows;
using Icatya;
using Icatya.Models;
using Icatya.Services.Audio;
using Icatya.Services.Video;
if(args.Length>0&&args[0]=="--audio-source")return AudioChecks.Source(args);
using var log=new StreamWriter(Path.Combine(AppContext.BaseDirectory,"capture-probe-result.txt"),false){AutoFlush=true};
Console.SetOut(log);Console.SetError(log);
int result=await AudioChecks.RunAsync(true);
Console.WriteLine("NATIVE_EXIT="+result);
if(result==0) {
    Environment.SetEnvironmentVariable("ICATYA_DATA_DIRECTORY",Path.Combine(AppContext.BaseDirectory,"qa-profile"));
    var ui=new Thread(()=>{
        try {
            var app=new System.Windows.Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
            app.Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/Icatya;component/Themes/DarkTheme.xaml",UriKind.Relative)});
            var main=new MainWindow { Title="iCatya • revisão",Width=1240,Height=900 };
            typeof(MainWindow).GetField("_skipRoomStartup",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(main,true);
            main.Show();
            new ShareSettingsWindow(SourceCatalog.Screens()[0],CaptureProfile.Available[0],AudioCaptureSettings.Silent,false,false){Owner=main,Title="Compartilhar • revisão iCatya"}.ShowDialog();
            main.Closed+=(_,_)=>app.Shutdown();app.Run();
        }catch(Exception ex){Console.Error.WriteLine(ex);}
    });
    ui.SetApartmentState(ApartmentState.STA);ui.Start();ui.Join();
}
Console.WriteLine("EXIT="+result);return result;
