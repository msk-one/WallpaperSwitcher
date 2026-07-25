using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WallpaperSwitcher.Desktop.Services;

/// <summary>
/// Reapplies the wallpaper after events that can leave the desktop showing
/// something other than what the schedule chose.
/// </summary>
/// <remarks>
/// The one-minute watchdog already recovers a missed schedule boundary, but it
/// asks for a non-forced apply, which short-circuits when the target file and
/// cycle key are unchanged. So if anything else changes the wallpaper — Windows
/// Spotlight, applying a theme, or the shell resetting it after a display driver
/// reset on resume — the app would not reclaim it until the next boundary, which
/// on the weekly cadence can be seven days away.
///
/// Windows only; nothing here changes behaviour on Linux or macOS.
/// </remarks>
public sealed class WindowsSystemEventsBridge : IDisposable
{
    private readonly Action _reapply;
    private bool _subscribed;

    private WindowsSystemEventsBridge(Action reapply)
    {
        _reapply = reapply;
    }

    public static WindowsSystemEventsBridge? CreateIfSupported(Action reapply)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        var bridge = new WindowsSystemEventsBridge(reapply);
        bridge.Subscribe();
        return bridge;
    }

    [SupportedOSPlatform("windows")]
    private void Subscribe()
    {
        try
        {
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnSystemStateChanged;
            SystemEvents.DisplaySettingsChanged += OnSystemStateChanged;
            _subscribed = true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.ExternalException)
        {
            // SystemEvents needs a message pump; if it is unavailable the
            // watchdog still covers schedule boundaries.
            AppLog.Warn($"System event notifications are unavailable: {ex.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs args)
    {
        if (args.Mode == PowerModes.Resume)
        {
            AppLog.Info("Resumed from sleep; reapplying the wallpaper.");
            _reapply();
        }
    }

    private void OnSystemStateChanged(object? sender, EventArgs args)
    {
        AppLog.Info("System time or display settings changed; reapplying the wallpaper.");
        _reapply();
    }

    public void Dispose()
    {
        if (!_subscribed || !OperatingSystem.IsWindows())
        {
            return;
        }

        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnSystemStateChanged;
        SystemEvents.DisplaySettingsChanged -= OnSystemStateChanged;
        _subscribed = false;
    }
}
