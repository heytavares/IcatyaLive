using System;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Threading;

namespace Icatya;

public partial class MainWindow
{
    // One short-lived timer for the whole stage, independent of video frame rate.
    private readonly DispatcherTimer _liveOverlayTimer = new() { Interval = TimeSpan.FromMilliseconds(150) };
    private long _lastLivePointerAt;
    private bool _livePointerInside;
    private bool _liveKeyboardNavigation;
    private void InitializeLiveOverlays() => _liveOverlayTimer.Tick += (_, _) => RefreshLiveOverlays();
    private void LiveSurfaceActivity(object sender, MouseEventArgs e)
    {
        _liveKeyboardNavigation = false;
        _livePointerInside = true;
        _lastLivePointerAt = Stopwatch.GetTimestamp();
        RefreshLiveOverlays();
        _liveOverlayTimer.Start();
    }
    private void LiveSurfaceKeyDown(object sender, KeyEventArgs e)
    {
        _liveKeyboardNavigation = true;
        RefreshLiveOverlays();
    }
    private void LiveOverlayFocusChanged()
    {
        if (Keyboard.IsKeyDown(Key.Tab)) _liveKeyboardNavigation = true;
        RefreshLiveOverlays();
    }
    private void LiveSurfaceLeft(object sender, MouseEventArgs e)
    {
        _livePointerInside = false;
        RefreshLiveOverlays();
    }
    private void RefreshLiveOverlays()
    {
        if (PlaybackOverlay == null || FocusControls == null) return;
        bool moving = PlaybackOverlay.IsMouseCaptureWithin || (_livePointerInside && _lastLivePointerAt != 0 &&
            Stopwatch.GetElapsedTime(_lastLivePointerAt).TotalSeconds < 2);
        bool keyboard = _liveKeyboardNavigation && (FocusControls.IsKeyboardFocusWithin || ExitFocusButton.IsKeyboardFocused);
        SetFocusControlVisible(moving || keyboard);
        bool volume = moving || _liveKeyboardNavigation && PlaybackOverlay.IsKeyboardFocusWithin;
        PlaybackOverlay.Opacity = volume ? 1 : 0;
        PlaybackOverlay.IsHitTestVisible = volume;
        foreach (var tile in _tiles.Values) tile.SetOverlayVisible(moving);
        if (!moving) _liveOverlayTimer.Stop();
    }
}
