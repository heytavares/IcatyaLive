using System;
using Icatya.Models;

namespace Icatya.Services.Video;

// Owns one retained frame. A busy renderer may retry it, unless a newer frame arrived.
internal sealed class VideoFrameMailbox : IDisposable
{
    private readonly object _gate = new();
    private VideoFrame? _frame;
    private bool _disposed;
    public bool Queue(VideoFrame frame)
    {
        lock (_gate) {
            if (_disposed) return false;
            var previous = _frame; _frame = frame.Retain();
            previous?.Dispose(); return previous != null;
        }
    }
    public VideoFrame? Take() { lock (_gate) { var result=_frame; _frame=null; return result; } }
    public bool Retry(VideoFrame frame)
    {
        lock (_gate) {
            if (_disposed || _frame != null) return false;
            _frame=frame.Retain(); return true;
        }
    }
    public void Clear() { lock (_gate) { _frame?.Dispose(); _frame=null; } }
    public void Dispose() { lock (_gate) { _disposed=true; Clear(); } }
}
