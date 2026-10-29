using System.Runtime.InteropServices;

namespace Icatya;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        SetCurrentProcessExplicitAppUserModelID("IcatyaLive.iCatya");
        base.OnStartup(e);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}

