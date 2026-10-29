using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Icatya.Services.Audio;
using NAudio.CoreAudioApi;
using Forms = System.Windows.Forms;

namespace Icatya.Services.Video;

public sealed record ShareSource(string Name, string DisplayName, IntPtr Window, int ProcessId = 0, long StartTimeTicks = 0)
{
    public bool IsWindow => Window != IntPtr.Zero;
    public void Validate()
    {
        if (!IsWindow) return;
        using var process = Process.GetProcessById(ProcessId);
        _ = process.SafeHandle;
        SourceCatalog.GetWindowThreadProcessId(Window, out uint pid);
        if (process.HasExited || pid != ProcessId || process.StartTime.ToUniversalTime().Ticks != StartTimeTicks || !SourceCatalog.IsWindow(Window))
            throw new InvalidOperationException("O aplicativo selecionado foi fechado. Escolha outra fonte para compartilhar.");
    }
}

public static class SourceCatalog
{
    public static ShareSource[] Screens() => Forms.Screen.AllScreens.Select(s => new ShareSource(
        s.DeviceName + " · " + s.Bounds.Width + " × " + s.Bounds.Height, s.DeviceName, IntPtr.Zero)).ToArray();
    public static ShareSource[] Windows()
    {
        var result = new List<ShareSource>();
        EnumWindows((window, _) => {
            if (!IsWindowVisible(window) || GetWindow(window, 4) != IntPtr.Zero || GetWindowTextLength(window) == 0) return true;
            if (window == GetShellWindow() || (GetWindowLongPtr(window, -20).ToInt64() & 0x80) != 0) return true;
            if (DwmGetWindowAttribute(window, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            GetWindowThreadProcessId(window, out uint pid);
            if (pid == Environment.ProcessId) return true;
            try {
                using var process = Process.GetProcessById((int)pid);
                var title = new StringBuilder(512); GetWindowText(window, title, title.Capacity);
                result.Add(new(title.ToString(), "", window, (int)pid, process.StartTime.ToUniversalTime().Ticks));
            } catch (Exception) { /* Closed or inaccessible windows are not selectable. */ }
            return true;
        }, IntPtr.Zero);
        return result.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public static AudioApplication[] AudioApplications()
    {
        var processes = ProcessTree();
        var ids = Windows().Select(w => w.ProcessId).ToHashSet();
        try {
            using var enumerator = new MMDeviceEnumerator();
            foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
            using (device) {
                var sessions = device.AudioSessionManager.Sessions;
                for (int i = 0; i < sessions.Count; i++) using (var session = sessions[i])
                    if (session.GetProcessID != 0) ids.Add((int)session.GetProcessID);
            }
        } catch (Exception) { /* Visible applications are still available if session enumeration fails. */ }
        var result = new List<AudioApplication>();
        var roots = new HashSet<int>();
        foreach (int id in ids) {
            int root = id;
            var visited = new HashSet<int>();
            while (visited.Add(root) && processes.TryGetValue(root, out var entry) && processes.TryGetValue(entry.Parent, out var parent) &&
                string.Equals(entry.Name, parent.Name, StringComparison.OrdinalIgnoreCase)) root = entry.Parent;
            if (root != Environment.ProcessId) roots.Add(root);
        }
        foreach (int id in roots)
        try {
            using var process = Process.GetProcessById(id);
            result.Add(new(id, process.StartTime.ToUniversalTime().Ticks, process.ProcessName));
        } catch (Exception) { }
        // A browser's visible root captures its child renderers as one application.
        return result.OrderBy(a => a.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    public static bool OverlappingAudioTrees(AudioApplication[] applications)
    {
        var parents = ProcessTree(); var ids = applications.Select(a => a.ProcessId).ToHashSet();
        foreach (int id in ids) {
            int current = id; var seen = new HashSet<int>();
            while (seen.Add(current) && parents.TryGetValue(current, out var entry)) {
                current = entry.Parent;
                if (ids.Contains(current)) return true;
            }
        }
        return false;
    }
    private static Dictionary<int, (int Parent, string Name)> ProcessTree()
    {
        var result = new Dictionary<int, (int, string)>();
        var snapshot = CreateToolhelp32Snapshot(2, 0);
        if (snapshot == new IntPtr(-1)) return result;
        try {
            var entry = new ProcessEntry { Size = (uint)Marshal.SizeOf<ProcessEntry>(), Name = "" };
            if (Process32First(snapshot, ref entry)) do { result[(int)entry.Id] = ((int)entry.Parent, entry.Name); } while (Process32Next(snapshot, ref entry));
        } finally { CloseHandle(snapshot); }
        return result;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct ProcessEntry {
        public uint Size, Usage, Id; public IntPtr Heap; public uint Module, Threads, Parent; public int Priority; public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string Name;
    }
    [DllImport("kernel32.dll")] private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint process);
    [DllImport("kernel32.dll", EntryPoint="Process32FirstW", CharSet=CharSet.Unicode)] private static extern bool Process32First(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll", EntryPoint="Process32NextW", CharSet=CharSet.Unicode)] private static extern bool Process32Next(IntPtr snapshot, ref ProcessEntry entry);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint relation);
    [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern int GetWindowTextLength(IntPtr window);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    private delegate bool WindowCallback(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, IntPtr parameter);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
