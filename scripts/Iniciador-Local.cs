using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

// Small Windows launcher: keeps the runtime and DLLs inside app/, not beside the source folders.
internal static class IcatyaLocalLauncher
{
    internal static string FindApplication(string root)
    {
        string packages = Path.Combine(root, "app");
        if (!Directory.Exists(packages)) return null;
        Version best = null;
        string result = null;
        foreach (string folder in Directory.GetDirectories(packages))
        {
            Version version;
            if (!Version.TryParse(Path.GetFileName(folder), out version)) continue;
            string exe = Path.Combine(folder, "Icatya.exe");
            if (!File.Exists(exe) || !File.Exists(Path.Combine(folder, "Icatya.dll")) ||
                !File.Exists(Path.Combine(folder, "Icatya.runtimeconfig.json"))) continue;
            if (best == null || version > best) { best = version; result = exe; }
        }
        return result;
    }

    [STAThread]
    private static int Main(string[] args)
    {
        bool check = args.Length == 1 && args[0] == "--check";
        try
        {
            string exe = FindApplication(AppDomain.CurrentDomain.BaseDirectory);
            if (exe == null) throw new FileNotFoundException("Os arquivos do iCatya não foram encontrados na pasta app. Mantenha este executável na pasta principal do projeto.");
            if (check) return 0;
            Process.Start(new ProcessStartInfo(exe) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = false });
            return 0;
        }
        catch (Exception ex)
        {
            if (!check) MessageBox.Show(ex.Message, "iCatya", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return 1;
        }
    }
}
