using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Avalonia.Controls;
using Avalonia.Platform;

namespace WallpaperSwitcher.Desktop.Services;

/// <summary>
/// Raises a window to the foreground on Windows.
/// </summary>
/// <remarks>
/// Windows only grants SetForegroundWindow to the process that already owns the
/// foreground, so a window restored from the tray comes up behind whatever had
/// focus. Opening the tray's context menu happens to transfer that right, which
/// is why the menu item worked while a plain click on the tray icon did not.
///
/// Attaching this thread's input queue to the foreground window's thread makes
/// the two count as one input context for the duration of the call, which is what
/// lets SetForegroundWindow succeed. This is the long-standing documented
/// workaround; the alternative (toggling Topmost) reorders the window without
/// giving it focus, so it can end up in front but not activated.
/// </remarks>
public static class ForegroundWindow
{
    public static void Raise(Window window)
    {
        if (!OperatingSystem.IsWindows())
        {
            window.Activate();
            return;
        }

        RaiseOnWindows(window);
    }

    [SupportedOSPlatform("windows")]
    private static void RaiseOnWindows(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            window.Activate();
            return;
        }

        var foreground = NativeMethods.GetForegroundWindow();
        var currentThread = NativeMethods.GetCurrentThreadId();
        var foregroundThread = foreground == IntPtr.Zero
            ? currentThread
            : NativeMethods.GetWindowThreadProcessId(foreground, out _);

        var attached = foregroundThread != currentThread
            && NativeMethods.AttachThreadInput(currentThread, foregroundThread, true);

        try
        {
            NativeMethods.ShowWindow(handle, NativeMethods.ShowRestore);
            NativeMethods.BringWindowToTop(handle);
            NativeMethods.SetForegroundWindow(handle);
        }
        finally
        {
            if (attached)
            {
                NativeMethods.AttachThreadInput(currentThread, foregroundThread, false);
            }
        }

        window.Activate();
    }

    [SupportedOSPlatform("windows")]
    private static class NativeMethods
    {
        internal const int ShowRestore = 9;

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr windowHandle);

        [DllImport("user32.dll")]
        internal static extern bool BringWindowToTop(IntPtr windowHandle);

        [DllImport("user32.dll")]
        internal static extern bool ShowWindow(IntPtr windowHandle, int command);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr windowHandle, out uint processId);

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        internal static extern bool AttachThreadInput(uint attachTo, uint attachFrom, bool attach);
    }
}
