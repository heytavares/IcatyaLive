using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Icatya.Services.Video;

// Absolute deadlines avoid accumulating sleep overshoot. A local Windows timer sleeps
// without busy-waiting or changing the timer resolution of other applications.
internal sealed class FramePacer : IDisposable
{
    private readonly TimerWaitHandle _timer;
    private readonly WaitHandle[] _waits;
    private readonly long _period;
    private long _deadline;

    public FramePacer(int framesPerSecond, CancellationToken cancellation)
    {
        if (framesPerSecond is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(framesPerSecond));
        _period = Stopwatch.Frequency / framesPerSecond;
        _deadline = Stopwatch.GetTimestamp();
        var handle = CreateWaitableTimerExW(IntPtr.Zero, null, 2, 0x00100002);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            handle = CreateWaitableTimerExW(IntPtr.Zero, null, 0, 0x00100002);
        }
        if (handle.IsInvalid)
        {
            int error = Marshal.GetLastWin32Error();
            handle.Dispose();
            throw new Win32Exception(error, "Não foi possível iniciar o relógio da captura.");
        }
        _timer = new TimerWaitHandle(handle);
        _waits = [cancellation.WaitHandle, _timer];
    }

    public bool WaitNext()
    {
        _deadline += _period;
        long now = Stopwatch.GetTimestamp();
        // Skip missed slots; never produce a burst of stale catch-up captures.
        if (now >= _deadline)
        {
            _deadline = now;
            return !_waits[0].WaitOne(0);
        }
        long due = -Math.Max(1, (long)((_deadline - now) * (10_000_000.0 / Stopwatch.Frequency)));
        if (!SetWaitableTimer(_timer.SafeWaitHandle, ref due, 0, IntPtr.Zero, IntPtr.Zero, false))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return WaitHandle.WaitAny(_waits) == 1;
    }

    public void Dispose() => _timer.Dispose();

    private sealed class TimerWaitHandle : WaitHandle
    {
        public TimerWaitHandle(SafeWaitHandle handle) => SafeWaitHandle = handle;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeWaitHandle CreateWaitableTimerExW(IntPtr attributes, string? name, uint flags, uint access);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWaitableTimer(SafeWaitHandle timer, ref long due, int period, IntPtr completion, IntPtr state,
        [MarshalAs(UnmanagedType.Bool)] bool resume);
}
