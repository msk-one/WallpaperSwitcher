using System.Security;
using System.Text;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WallpaperSwitcher.Desktop.Services;

public static class LaunchAtLoginService
{
    private const string AppName = "WallpaperSwitcher";
    private const string WindowsRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Passed to the autostart entry so signing in starts the app into the tray
    /// instead of putting a window on screen. Windows only for now; the macOS
    /// LaunchAgent and Linux .desktop entries keep the current behaviour until
    /// those platforms have been re-verified.
    /// </summary>
    public const string MinimizedArgument = "--minimized";

    private const string MacLaunchAgentId = "com.wallpaperswitcher.app";
    private const string LinuxDesktopFileName = "wallpaperswitcher.desktop";

    public static bool IsEnabled()
    {
        var executablePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                return IsEnabledWindows(executablePath);
            }

            if (OperatingSystem.IsMacOS())
            {
                return File.Exists(GetMacLaunchAgentPath());
            }

            if (OperatingSystem.IsLinux())
            {
                return File.Exists(GetLinuxDesktopFilePath());
            }
        }
        catch (Exception ex) when (IsLaunchAtLoginException(ex))
        {
            return false;
        }

        return false;
    }

    public static bool TrySetEnabled(bool enabled, out string? errorMessage)
    {
        errorMessage = null;
        var executablePath = GetExecutablePath();
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            errorMessage = "Unable to resolve the app executable path.";
            return false;
        }

        try
        {
            if (OperatingSystem.IsWindows())
            {
                SetWindowsLaunchAtLogin(enabled, executablePath);
                return true;
            }

            if (OperatingSystem.IsMacOS())
            {
                SetMacLaunchAtLogin(enabled, executablePath);
                return true;
            }

            if (OperatingSystem.IsLinux())
            {
                SetLinuxLaunchAtLogin(enabled, executablePath);
                return true;
            }

            errorMessage = "Launch at login is not implemented for this operating system.";
            return false;
        }
        catch (Exception ex) when (IsLaunchAtLoginException(ex))
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool IsEnabledWindows(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(WindowsRunKey, writable: false);
        if (key?.GetValue(AppName) is not string value || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var registeredPath = WindowsRunCommand.ParseExecutablePath(value);
        if (string.IsNullOrWhiteSpace(registeredPath))
        {
            return false;
        }

        if (string.Equals(registeredPath, executablePath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // The entry points somewhere else: the portable build was moved, or the
        // user upgraded from the zip to the installer. Repointing it is what the
        // user asked for when they ticked the box, and reporting "off" here would
        // leave a dangling entry aimed at a path that may no longer exist.
        AppLog.Info($"Repointing stale launch-at-login entry from '{registeredPath}' to '{executablePath}'.");
        try
        {
            WriteWindowsRunValue(executablePath);
        }
        catch (Exception ex) when (IsLaunchAtLoginException(ex))
        {
            AppLog.Warn($"Could not repoint launch-at-login entry: {ex.Message}");
            return false;
        }

        return true;
    }

    [SupportedOSPlatform("windows")]
    private static void SetWindowsLaunchAtLogin(bool enabled, string executablePath)
    {
        if (enabled)
        {
            WriteWindowsRunValue(executablePath);
            return;
        }

        using var key = Registry.CurrentUser.CreateSubKey(WindowsRunKey);
        key.DeleteValue(AppName, throwOnMissingValue: false);
    }

    [SupportedOSPlatform("windows")]
    private static void WriteWindowsRunValue(string executablePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(WindowsRunKey);
        key.SetValue(AppName, WindowsRunCommand.Format(executablePath, MinimizedArgument));
    }

    private static void SetMacLaunchAtLogin(bool enabled, string executablePath)
    {
        var plistPath = GetMacLaunchAgentPath();
        if (!enabled)
        {
            DeleteIfExists(plistPath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(plistPath)!);
        var escapedExecutable = SecurityElement.Escape(executablePath) ?? executablePath;
        var plist = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
                <key>Label</key>
                <string>{{MacLaunchAgentId}}</string>
                <key>ProgramArguments</key>
                <array>
                    <string>{{escapedExecutable}}</string>
                </array>
                <key>RunAtLoad</key>
                <true/>
            </dict>
            </plist>
            """;

        File.WriteAllText(plistPath, plist, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static void SetLinuxLaunchAtLogin(bool enabled, string executablePath)
    {
        var desktopFilePath = GetLinuxDesktopFilePath();
        if (!enabled)
        {
            DeleteIfExists(desktopFilePath);
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(desktopFilePath)!);
        var desktopFile = $$"""
            [Desktop Entry]
            Type=Application
            Name=Wallpaper Switcher
            Comment=Keep day and night wallpapers rotating
            Exec={{QuoteDesktopExecArgument(executablePath)}}
            Terminal=false
            X-GNOME-Autostart-enabled=true
            """;

        File.WriteAllText(desktopFilePath, desktopFile, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    private static string? GetExecutablePath()
    {
        return Environment.ProcessPath is { Length: > 0 } processPath
            ? Path.GetFullPath(processPath)
            : null;
    }

    private static string GetMacLaunchAgentPath()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Library",
            "LaunchAgents",
            MacLaunchAgentId + ".plist");
    }

    private static string GetLinuxDesktopFilePath()
    {
        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var basePath = string.IsNullOrWhiteSpace(configHome)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : configHome;

        return Path.Combine(basePath, "autostart", LinuxDesktopFileName);
    }

    /// <summary>
    /// Quotes a path for the <c>Exec=</c> line of a freedesktop .desktop file,
    /// which does require backslash escaping. Windows Run values must not use
    /// this — see <see cref="WindowsRunCommand"/>.
    /// </summary>
    private static string QuoteDesktopExecArgument(string path)
    {
        return "\"" + path.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static bool IsLaunchAtLoginException(Exception ex)
    {
        return ex is IOException
            or UnauthorizedAccessException
            or SecurityException
            or ArgumentException
            or InvalidOperationException;
    }
}
