using System;
using System.Runtime.InteropServices;
using FFmpeg.AutoGen;
using Icatya.Models;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Windows.Graphics.Capture;
using Windows.Graphics.DirectX;
using Windows.Graphics.DirectX.Direct3D11;
using WinRT;
using Device = SharpDX.Direct3D11.Device;

namespace Icatya.Services.Video;

// Captures only the chosen HWND, even when another application covers it.
internal sealed unsafe class WindowCapture : IDisposable
{
    private readonly object _gate = new();
    private readonly ShareSource _source;
    private Device? _device;
    private IDirect3DDevice? _winDevice;
    private GraphicsCaptureItem? _item;
    private Direct3D11CaptureFramePool? _pool;
    private GraphicsCaptureSession? _session;
    private Direct3D11CaptureFrame? _latest;
    private Texture2D? _staging;
    private SwsContext* _scale;
    private volatile bool _closed, _disposed;
    public WindowCapture(ShareSource source)
    {
        source.Validate(); _source = source;
        NativeVideoRuntime.EnsureLoaded();
        try {
            if (!GraphicsCaptureSession.IsSupported()) throw new NotSupportedException("Este Windows não oferece captura de aplicativos.");
            try { _device = new Device(DriverType.Hardware, DeviceCreationFlags.BgraSupport); }
            catch (SharpDX.SharpDXException) { _device = new Device(DriverType.Warp, DeviceCreationFlags.BgraSupport); }
            using (var dxgi = _device.QueryInterface<SharpDX.DXGI.Device>()) {
                Marshal.ThrowExceptionForHR(CreateDirect3D11DeviceFromDXGIDevice(dxgi.NativePointer, out var pointer));
                try { _winDevice = MarshalInterface<IDirect3DDevice>.FromAbi(pointer); }
                finally { Marshal.Release(pointer); }
            }
            _item = CreateItem(source.Window);
            _item.Closed += ItemClosed;
            _pool = Direct3D11CaptureFramePool.CreateFreeThreaded(_winDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, _item.Size);
            _pool.FrameArrived += Arrived;
            _session = _pool.CreateCaptureSession(_item);
            _session.IsCursorCaptureEnabled = true;
            _session.StartCapture();
        } catch (UnauthorizedAccessException ex) {
            Dispose(); throw new InvalidOperationException("O Windows não permitiu capturar esse aplicativo. Nenhuma outra janela será capturada. Você pode escolher uma tela inteira para compartilhar.", ex);
        } catch { Dispose(); throw; }
    }
    private void ItemClosed(GraphicsCaptureItem sender, object args) => _closed = true;
    private void Arrived(Direct3D11CaptureFramePool pool, object args)
    {
        lock (_gate) {
            if (_disposed) return;
            var frame = pool.TryGetNextFrame();
            if (frame == null) return;
            _latest?.Dispose(); _latest = frame;
        }
    }
    public bool TryCopyTo(VideoFrame target)
    {
        if (_closed) throw new InvalidOperationException("O aplicativo compartilhado foi fechado.");
        _source.Validate();
        Direct3D11CaptureFrame? frame;
        lock (_gate) { frame = _latest; _latest = null; }
        if (frame == null) return false;
        Windows.Graphics.SizeInt32? resized = null;
        using (frame) {
            using var surface = frame.Surface;
            var access = surface.As<IDirect3DDxgiInterfaceAccess>();
            var iid = typeof(Texture2D).GUID;
            IntPtr pointer;
            try { access.GetInterface(ref iid, out pointer); } finally { Marshal.ReleaseComObject(access); }
            using var texture = new Texture2D(pointer);
            var description = texture.Description;
            int width = Math.Min(frame.ContentSize.Width, description.Width), height = Math.Min(frame.ContentSize.Height, description.Height);
            if (width <= 0 || height <= 0) return false;
            if (_staging == null || _staging.Description.Width != description.Width || _staging.Description.Height != description.Height) {
                _staging?.Dispose();
                description.Usage = ResourceUsage.Staging; description.BindFlags = BindFlags.None;
                description.CpuAccessFlags = CpuAccessFlags.Read; description.OptionFlags = ResourceOptionFlags.None;
                _staging = new Texture2D(_device, description);
            }
            _device!.ImmediateContext.CopyResource(texture, _staging);
            var mapped = _device.ImmediateContext.MapSubresource(_staging, 0, MapMode.Read, MapFlags.None);
            try {
                double ratio = Math.Min((double)target.Width / width, (double)target.Height / height);
                int dw = Math.Max(2, (int)(width * ratio)), dh = Math.Max(2, (int)(height * ratio));
                _scale = ffmpeg.sws_getCachedContext(_scale, width, height, AVPixelFormat.AV_PIX_FMT_BGRA,
                    dw, dh, AVPixelFormat.AV_PIX_FMT_BGR24, (int)SwsFlags.SWS_FAST_BILINEAR, null, null, null);
                if (_scale == null) throw new InvalidOperationException("Não foi possível preparar a imagem do aplicativo.");
                Array.Clear(target.Pixels, 0, target.Stride * target.Height);
                fixed (byte* output = target.Pixels) {
                    byte_ptrArray4 input = default, destination = default;
                    int_array4 stride = default, destStride = default;
                    input[0] = (byte*)mapped.DataPointer; stride[0] = mapped.RowPitch;
                    destination[0] = output + (target.Height - dh) / 2 * target.Stride + (target.Width - dw) / 2 * 3;
                    destStride[0] = target.Stride;
                    ffmpeg.sws_scale(_scale, input, stride, 0, height, destination, destStride);
                }
            } finally { _device.ImmediateContext.UnmapSubresource(_staging, 0); }
            if (frame.ContentSize.Width > 0 && frame.ContentSize.Height > 0 &&
                (frame.ContentSize.Width != description.Width || frame.ContentSize.Height != description.Height)) resized = frame.ContentSize;
        }
        if (resized.HasValue) {
            lock (_gate) { _latest?.Dispose(); _latest = null; }
            // Release all acquired frames before resizing; never wait for a callback under its lock.
            _pool!.Recreate(_winDevice, DirectXPixelFormat.B8G8R8A8UIntNormalized, 2, resized.Value);
        }
        return true;
    }
    public void Dispose()
    {
        lock (_gate) {
            if (_disposed) return; _disposed = true;
            if (_pool != null) _pool.FrameArrived -= Arrived;
            if (_item != null) _item.Closed -= ItemClosed;
            _latest?.Dispose(); _latest = null;
        }
        _session?.Dispose(); _pool?.Dispose(); _staging?.Dispose();
        (_winDevice as IDisposable)?.Dispose(); _device?.Dispose();
        if (_scale != null) { ffmpeg.sws_freeContext(_scale); _scale = null; }
    }
    private static GraphicsCaptureItem CreateItem(IntPtr window)
    {
        const string name = "Windows.Graphics.Capture.GraphicsCaptureItem";
        Marshal.ThrowExceptionForHR(WindowsCreateString(name, name.Length, out var text));
        IntPtr factory = IntPtr.Zero, item = IntPtr.Zero;
        try {
            Guid iid = typeof(IGraphicsCaptureItemInterop).GUID;
            Marshal.ThrowExceptionForHR(RoGetActivationFactory(text, ref iid, out factory));
            var interop = (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factory);
            Guid itemId = new("79C3F95B-31F7-4EC2-A464-632EF5D30760");
            try { interop.CreateForWindow(window, ref itemId, out item); }
            finally { Marshal.ReleaseComObject(interop); }
            return MarshalInterface<GraphicsCaptureItem>.FromAbi(item);
        } finally { if (item != IntPtr.Zero) Marshal.Release(item); if (factory != IntPtr.Zero) Marshal.Release(factory); WindowsDeleteString(text); }
    }
    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop {
        void CreateForWindow(IntPtr window, ref Guid iid, out IntPtr item);
        void CreateForMonitor(IntPtr monitor, ref Guid iid, out IntPtr item);
    }
    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-8695F457D3C1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess { void GetInterface(ref Guid iid, out IntPtr texture); }
    [DllImport("d3d11.dll")] private static extern int CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgi, out IntPtr device);
    [DllImport("combase.dll", CharSet = CharSet.Unicode)] private static extern int WindowsCreateString(string value, int length, out IntPtr text);
    [DllImport("combase.dll")] private static extern int WindowsDeleteString(IntPtr text);
    [DllImport("combase.dll")] private static extern int RoGetActivationFactory(IntPtr text, ref Guid iid, out IntPtr factory);
}

