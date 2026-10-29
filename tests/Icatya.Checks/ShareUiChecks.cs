using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Icatya;
using Icatya.Models;
using Icatya.Services.Audio;
using Icatya.Services.Video;

internal static class ShareUiChecks
{
    private static void Require(bool condition,string text){if(!condition)throw new Exception(text);}
    public static int Run()
    {
        Exception? error=null;
        Environment.SetEnvironmentVariable("ICATYA_DATA_DIRECTORY",Path.Combine(AppContext.BaseDirectory,"checks","ui-"+Guid.NewGuid().ToString("N")));
        var ui=new Thread(()=>{
            try {
                
                var app=new System.Windows.Application { ShutdownMode=ShutdownMode.OnExplicitShutdown };
                app.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/Icatya;component/Themes/DarkTheme.xaml",UriKind.Relative) });
                var dialog=new ShareSettingsWindow(SourceCatalog.Screens()[0],CaptureProfile.Available[0],AudioCaptureSettings.Silent,false,false);
                dialog.Loaded+=(_,_)=>dialog.Dispatcher.BeginInvoke(new Action(()=>{
                    try {
                        dialog.UpdateLayout();
                        var confirm=(Button)dialog.FindName("ConfirmButton");
                        Require(confirm.IsEnabled,"Tela anterior não foi selecionada.");
                        confirm.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));dialog.UpdateLayout();
                        Require(((ScrollViewer)dialog.FindName("SettingsStep")).IsVisible,"Não abriu etapa de qualidade/som.");
                        Require(!dialog.SelectedAudio.Enabled&&dialog.SelectedAudio.Sources.Count==0,"Áudio não começou desligado.");
                        var enabled=(CheckBox)dialog.FindName("AudioEnabled");enabled.IsChecked=true;
                        Require(((StackPanel)dialog.FindName("Mixer")).IsEnabled,"Mixer não habilitou.");
                        Require(dialog.SelectedAudio.Sources.Count==0,"Selecionou fontes sem autorização.");
                        var master=(Slider)dialog.FindName("MasterVolume");master.Value=37;
                        Require(Math.Abs(dialog.SelectedAudio.MasterVolume-.37f)<.001,"Master não atualizou rascunho.");
                        ((Button)dialog.FindName("BackButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));dialog.UpdateLayout();
                        Require(((Grid)dialog.FindName("SourceStep")).IsVisible,"Não voltou à seleção.");
                        Require(dialog.SelectedSource?.IsWindow==false,"Perdeu a tela selecionada.");
                        Console.WriteLine("PASS WPF: escolha de fonte, duas etapas, áudio desligado, master e retorno sem captura");
                    }catch(Exception ex){error=ex;}
                    finally{dialog.Close();}
                }));
                dialog.ShowDialog();
                var main=new MainWindow();
                typeof(MainWindow).GetField("_skipRoomStartup",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(main,true);
                var volume=(Slider)main.FindName("PlaybackVolume");volume.Value=20;
                ((Button)main.FindName("PlaybackMute")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Require(volume.Value==0,"Botão mute não zerou volume.");
                ((Button)main.FindName("PlaybackMute")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Require(volume.Value==20,"Mute não restaurou volume anterior.");
                Console.WriteLine("PASS WPF: slider recebido e mute/restaurar");
                // Close through the application's asynchronous shutdown path.
                main.Closed+=(_,_)=>app.Shutdown();main.Show();main.Close();app.Run();
            }catch(Exception ex){error=ex;}
        });
        ui.SetApartmentState(ApartmentState.STA);ui.Start();ui.Join();
        if(error!=null){Console.Error.WriteLine(error);return 1;}
        return 0;
    }
}



