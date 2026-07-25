using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace WallpaperSwitcher.Desktop.Services;

public sealed class PlatformWallpaperService : IWallpaperService
{
    private const uint SetDesktopWallpaper = 0x0014;
    private const uint UpdateIniFile = 0x0001;
    private const uint SendWinIniChange = 0x0002;

    public bool TryApply(string wallpaperPath, WallpaperFit fit, out string? errorMessage)
    {
        errorMessage = null;

        if (!File.Exists(wallpaperPath))
        {
            errorMessage = "The selected wallpaper file no longer exists.";
            return false;
        }

        // Windows accepts a corrupt or empty file and paints the desktop black
        // rather than reporting an error, so the file has to be vetted here or a
        // bad image silently blanks the desktop for the rest of the cycle.
        if (!ImageFileProbe.LooksLikeImage(wallpaperPath, out var probeReason))
        {
            errorMessage = $"'{Path.GetFileName(wallpaperPath)}' is not a usable image: {probeReason}.";
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return TryApplyWindows(wallpaperPath, fit, out errorMessage);
        }

        if (OperatingSystem.IsMacOS())
        {
            return TryApplyMacOS(wallpaperPath, out errorMessage);
        }

        if (OperatingSystem.IsLinux())
        {
            return TryApplyLinux(wallpaperPath, out errorMessage);
        }

        errorMessage = "Changing wallpaper is not implemented for this operating system.";
        return false;
    }

    [SupportedOSPlatform("windows")]
    private static bool TryApplyWindows(string wallpaperPath, WallpaperFit fit, out string? errorMessage)
    {
        errorMessage = null;

        // SystemParametersInfo does not carry the fit mode; it reads whatever is
        // already in HKCU\Control Panel\Desktop. Writing it first is what makes
        // the app's own setting authoritative instead of inheriting whatever the
        // machine happened to be left on.
        ApplyWindowsFit(fit);

        if (!NativeMethods.SystemParametersInfo(
                SetDesktopWallpaper,
                0,
                wallpaperPath,
                UpdateIniFile | SendWinIniChange))
        {
            errorMessage = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        return true;
    }

    [SupportedOSPlatform("windows")]
    private static void ApplyWindowsFit(WallpaperFit fit)
    {
        // WallpaperStyle / TileWallpaper pairs, per the documented shell values.
        var (style, tile) = fit switch
        {
            WallpaperFit.Fill => ("10", "0"),
            WallpaperFit.Fit => ("6", "0"),
            WallpaperFit.Stretch => ("2", "0"),
            WallpaperFit.Center => ("0", "0"),
            WallpaperFit.Tile => ("0", "1"),
            WallpaperFit.Span => ("22", "0"),
            _ => ("10", "0")
        };

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true);
            if (key is null)
            {
                return;
            }

            key.SetValue("WallpaperStyle", style, RegistryValueKind.String);
            key.SetValue("TileWallpaper", tile, RegistryValueKind.String);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            // The wallpaper itself still applies; it just keeps the previous fit.
            AppLog.Warn($"Could not set wallpaper fit to {fit}: {ex.Message}");
        }
    }

    private static bool TryApplyMacOS(string wallpaperPath, out string? errorMessage)
    {
        var script = "tell application \"System Events\" to tell every desktop to set picture to POSIX file "
            + QuoteAppleScriptString(Path.GetFullPath(wallpaperPath));

        return TryRun("/usr/bin/osascript", ["-e", script], out errorMessage);
    }

    private static bool TryApplyLinux(string wallpaperPath, out string? errorMessage)
    {
        var fullPath = Path.GetFullPath(wallpaperPath);
        var uri = new Uri(fullPath).AbsoluteUri;
        var errors = new List<string>();

        if (TryRun("gsettings", ["set", "org.gnome.desktop.background", "picture-uri", uri], out var gsettingsError))
        {
            TryRun("gsettings", ["set", "org.gnome.desktop.background", "picture-uri-dark", uri], out _);
            errorMessage = null;
            return true;
        }

        AddError(errors, "gsettings", gsettingsError);

        if (TryRun("plasma-apply-wallpaperimage", [fullPath], out var plasmaError))
        {
            errorMessage = null;
            return true;
        }

        AddError(errors, "plasma-apply-wallpaperimage", plasmaError);

        if (TryApplyXfce(fullPath, errors, out errorMessage))
        {
            return true;
        }

        if (TryRun("swww", ["img", fullPath], out var swwwError))
        {
            errorMessage = null;
            return true;
        }

        AddError(errors, "swww", swwwError);

        if (TryRun("feh", ["--bg-fill", fullPath], out var fehError))
        {
            errorMessage = null;
            return true;
        }

        AddError(errors, "feh", fehError);

        errorMessage = "Unable to change wallpaper on this Linux desktop. Tried gsettings, KDE Plasma, XFCE, swww, and feh."
            + (errors.Count == 0 ? string.Empty : $" Last error: {errors[^1]}");
        return false;
    }

    private static bool TryApplyXfce(string wallpaperPath, List<string> errors, out string? errorMessage)
    {
        errorMessage = null;

        if (!TryRun(
                "xfconf-query",
                ["-c", "xfce4-desktop", "-l"],
                out var listError,
                out var output))
        {
            AddError(errors, "xfconf-query", listError);
            return false;
        }

        var imageProperties = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => line.EndsWith("/last-image", StringComparison.Ordinal))
            .ToList();

        foreach (var property in imageProperties)
        {
            if (TryRun("xfconf-query", ["-c", "xfce4-desktop", "-p", property, "-s", wallpaperPath], out _))
            {
                errorMessage = null;
                return true;
            }
        }

        errorMessage = "XFCE wallpaper properties were not found.";
        return false;
    }

    private static bool TryRun(string fileName, IReadOnlyList<string> arguments, out string? errorMessage)
    {
        return TryRun(fileName, arguments, out errorMessage, out _);
    }

    private static bool TryRun(
        string fileName,
        IReadOnlyList<string> arguments,
        out string? errorMessage,
        out string output)
    {
        output = string.Empty;
        errorMessage = null;

        try
        {
            using var process = new Process();
            process.StartInfo.FileName = fileName;
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;

            process.Start();

            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                errorMessage = $"{fileName} timed out.";
                return false;
            }

            output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();

            if (process.ExitCode == 0)
            {
                return true;
            }

            errorMessage = string.IsNullOrWhiteSpace(error)
                ? $"{fileName} exited with code {process.ExitCode}."
                : error.Trim();
            return false;
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            errorMessage = ex.Message;
            return false;
        }
    }

    private static string QuoteAppleScriptString(string value)
    {
        return "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
    }

    private static void AddError(List<string> errors, string tool, string? error)
    {
        if (!string.IsNullOrWhiteSpace(error))
        {
            errors.Add($"{tool}: {error}");
        }
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);
    }
}
