using System;
using System.IO;

internal static class LauncherChecks
{
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static string Package(string root, string version, bool complete)
    {
        string dir = Path.Combine(root, "app", version);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Icatya.exe"), "test fixture, never executed");
        if (complete)
        {
            File.WriteAllText(Path.Combine(dir, "Icatya.dll"), "test fixture");
            File.WriteAllText(Path.Combine(dir, "Icatya.runtimeconfig.json"), "{}");
        }
        return Path.Combine(dir, "Icatya.exe");
    }
    private static int Main(string[] args)
    {
        try
        {
            string root = Path.Combine(args[0], "launcher-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            Assert(IcatyaLocalLauncher.FindApplication(root) == null, "Missing app directory not handled");
            string oldest = Package(root, "0.7.2", true);
            Assert(IcatyaLocalLauncher.FindApplication(root) == oldest, "Valid package not found");
            Package(root, "9.0.0", false);
            Package(root, "not-a-version", true);
            string newest = Package(root, "1.0.10", true);
            Package(root, "1.0.2", true);
            Assert(IcatyaLocalLauncher.FindApplication(root) == newest, "Version ordering or incomplete package selection failed");
            Console.WriteLine("PASS local launcher: missing package, numeric version order and incomplete/invalid directories; no application launched.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
