using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using NAudio.Wave;

namespace Icatya.Services.Audio;

// Positive selection only: the Windows audio engine includes the chosen process tree.
// No device-wide loopback fallback is used, including on activation/device errors.
public sealed class ProcessLoopbackCapture
{
    public static bool Supported => OperatingSystem.IsWindowsVersionAtLeast(10, 0, 20348);
    public static void ValidateProcess(AudioApplication application, Process process)
    {
        if (process.HasExited || process.Id != application.ProcessId || process.StartTime.ToUniversalTime().Ticks != application.StartTimeTicks)
            throw new InvalidOperationException("O aplicativo de áudio foi fechado ou reiniciado: " + application.Name);
    }
    internal static Task RunAsync(AudioCaptureRoute route, CancellationToken stop) => Task.Factory.StartNew(
        () => Run(route, stop), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    private static void Run(AudioCaptureRoute route, CancellationToken stop)
    {
        if (!Supported) throw new NotSupportedException("Áudio por aplicativo exige Windows 11 ou Windows com build 20348 ou posterior.");
        Marshal.ThrowExceptionForHR(CoInitializeEx(IntPtr.Zero, 0));
        try
        {
            using var process = Process.GetProcessById(route.Selection.Application.ProcessId);
            _ = process.SafeHandle; // Hold the original process object so its PID cannot be recycled during capture.
            ValidateProcess(route.Selection.Application, process);
            using var activation = new Activation((uint)process.Id);
            using var client = activation.GetClient(stop);
            ValidateProcess(route.Selection.Application, process);
            using var samplesReady = new AutoResetEvent(false);
            var format = new WaveFormat(48000, 16, 2);
            client.Initialize(AudioClientShareMode.Shared,
                AudioClientStreamFlags.Loopback | AudioClientStreamFlags.EventCallback | AudioClientStreamFlags.AutoConvertPcm | AudioClientStreamFlags.SrcDefaultQuality,
                0, 0, format, Guid.Empty);
            client.SetEventHandle(samplesReady.SafeWaitHandle.DangerousGetHandle());
            var capture = client.AudioCaptureClient;
            float[] samples = new float[Math.Max(1920, client.BufferSize * 2)];
            var handles = new[] { samplesReady, stop.WaitHandle };
            client.Start();
            try
            {
                while (!stop.IsCancellationRequested && !process.HasExited)
                {
                    if (WaitHandle.WaitAny(handles, 100) == 1) break;
                    int available;
                    while ((available = capture.GetNextPacketSize()) > 0 && !stop.IsCancellationRequested)
                    {
                        var data = capture.GetBuffer(out int frames, out AudioClientBufferFlags flags);
                        try
                        {
                            int count = checked(frames * 2);
                            if (samples.Length < count) samples = new float[count];
                            if ((flags & AudioClientBufferFlags.Silent) != 0) Array.Clear(samples, 0, count);
                            else
                            {
                                unsafe
                                {
                                    var pcm = new ReadOnlySpan<short>((void*)data, count);
                                    for (int i = 0; i < count; i++) samples[i] = pcm[i] / 32768f;
                                }
                            }
                            float peak = 0;
                            for (int i = 0; i < count; i++) peak = Math.Max(peak, Math.Abs(samples[i]));
                            route.Peak = peak;
                            route.Buffer.Write(samples.AsSpan(0, count));
                        }
                        finally { capture.ReleaseBuffer(frames); }
                    }
                }
                if (!stop.IsCancellationRequested) throw new InvalidOperationException("Fonte de áudio fechada: " + route.Selection.Application.Name);
            }
            finally { client.Stop(); route.Buffer.Clear(); route.Peak = 0; }
        }
        finally { CoUninitialize(); }
    }

    [ComVisible(true), Guid("94EA2B94-E9CC-49E0-C0FF-EE64CA8F5B90"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAgile { }
    [ComVisible(true), Guid("41D949AB-9862-444A-80F6-C261334DA5EB"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICompletion
    {
        [PreserveSig] int ActivateCompleted(IActivationOperation operation);
    }
    [ComImport, Guid("72A22D78-CDE4-431D-B8CC-843A71199B6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IActivationOperation
    {
        [PreserveSig] int GetActivateResult(out int result, [MarshalAs(UnmanagedType.IUnknown)] out object client);
    }
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    private struct BlobVariant
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(8)] public uint Size;
        [FieldOffset(16)] public IntPtr Data;
    }
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class Activation : ICompletion, IAgile, ICustomQueryInterface, IDisposable
    {
        private readonly TaskCompletionSource<object> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IntPtr _parameters, _variant;
        private IActivationOperation? _operation;
        private bool _abandoned;
        private readonly object _gate = new();
        private IntPtr _marshaler;
        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr pointer)
        {
            pointer = IntPtr.Zero;
            if (iid != new Guid("00000003-0000-0000-C000-000000000046")) return CustomQueryInterfaceResult.NotHandled;
            if (_marshaler == IntPtr.Zero) return CustomQueryInterfaceResult.Failed;
            return Marshal.QueryInterface(_marshaler, in iid, out pointer) == 0 ? CustomQueryInterfaceResult.Handled : CustomQueryInterfaceResult.Failed;
        }
        public Activation(uint pid)
        {
            Marshal.ThrowExceptionForHR(CoCreateFreeThreadedMarshaler(IntPtr.Zero, out _marshaler));
            _parameters = Marshal.AllocCoTaskMem(12);
            Marshal.WriteInt32(_parameters, 0, 1); // AUDIOCLIENT_ACTIVATION_TYPE_PROCESS_LOOPBACK
            Marshal.WriteInt32(_parameters, 4, unchecked((int)pid));
            Marshal.WriteInt32(_parameters, 8, 0); // INCLUDE_TARGET_PROCESS_TREE
            _variant = Marshal.AllocCoTaskMem(24);
            Marshal.StructureToPtr(new BlobVariant { Type = 65, Size = 12, Data = _parameters }, _variant, false);
            Guid iid = typeof(IAudioClient).GUID;
            int result = ActivateAudioInterfaceAsync("VAD\\Process_Loopback", ref iid, _variant, this, out _operation);
            if (result < 0) {
                Free();
                var marshaler = Interlocked.Exchange(ref _marshaler, IntPtr.Zero);
                if (marshaler != IntPtr.Zero) Marshal.Release(marshaler);
                Marshal.ThrowExceptionForHR(result);
            }
        }
        public AudioClient GetClient(CancellationToken stop)
        {
            try { return new AudioClient((IAudioClient)_ready.Task.WaitAsync(TimeSpan.FromSeconds(10), stop).GetAwaiter().GetResult()); }
            catch {
                lock (_gate) {
                    _abandoned = true;
                    if (_ready.Task.IsCompletedSuccessfully) Marshal.ReleaseComObject(_ready.Task.Result);
                }
                throw;
            }
        }
        public int ActivateCompleted(IActivationOperation operation)
        {
            try
            {
                Marshal.ThrowExceptionForHR(operation.GetActivateResult(out int hr, out object client));
                Marshal.ThrowExceptionForHR(hr);
                lock (_gate) {
                    if (_abandoned) Marshal.ReleaseComObject(client); else _ready.TrySetResult(client);
                }
            }
            catch (Exception ex) { _ready.TrySetException(ex); }
            finally { Free(); }
            return 0;
        }
        private void Free()
        {
            var variant = Interlocked.Exchange(ref _variant, IntPtr.Zero);
            var parameters = Interlocked.Exchange(ref _parameters, IntPtr.Zero);
            if (variant != IntPtr.Zero) Marshal.FreeCoTaskMem(variant);
            if (parameters != IntPtr.Zero) Marshal.FreeCoTaskMem(parameters);
        }
        public void Dispose()
        {
            // The callback owns activation memory until the asynchronous operation completes.
            if (_ready.Task.IsCompleted) Free();
            GC.KeepAlive(_operation);
            var marshaler = Interlocked.Exchange(ref _marshaler, IntPtr.Zero);
            if (marshaler != IntPtr.Zero) Marshal.Release(marshaler);
        }
    }
    [DllImport("Mmdevapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int ActivateAudioInterfaceAsync(string device, ref Guid iid, IntPtr parameters,
        [MarshalAs(UnmanagedType.Interface)] ICompletion completion, out IActivationOperation operation);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved, uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("ole32.dll")] private static extern int CoCreateFreeThreadedMarshaler(IntPtr outer, out IntPtr marshaler);
}

