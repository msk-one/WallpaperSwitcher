using System.Text;

namespace WallpaperSwitcher.Desktop.Services;

/// <summary>
/// Minimal append-only file log.
/// </summary>
/// <remarks>
/// Deliberately not a logging framework. This app ships as a self-contained
/// single file, so every dependency is payload the user downloads, and the only
/// thing we actually need is a record of why a tray-resident process stopped
/// working when there was no window on screen to show an error in.
/// </remarks>
public static class AppLog
{
    private const int RetainedDays = 7;
    private static readonly object Gate = new();
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public static string LogDirectory { get; } = Path.Combine(SettingsStore.AppDataDirectory, "logs");

    public static void Initialize()
    {
        PruneOldLogs();

        var version = typeof(AppLog).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion ?? "unknown";

        Info($"Wallpaper Switcher {version} starting");
        Info($"OS: {Environment.OSVersion.VersionString} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})");
        Info($"Executable: {Environment.ProcessPath ?? "unknown"}");
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Warn(string message)
    {
        Write("WARN", message);
    }

    public static void Error(string message, Exception? exception = null)
    {
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");
    }

    private static void Write(string level, string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}{Environment.NewLine}";

        try
        {
            lock (Gate)
            {
                Directory.CreateDirectory(LogDirectory);
                File.AppendAllText(CurrentLogPath(), line, Utf8NoBom);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // A logger that can throw is worse than no logger.
        }
    }

    private static string CurrentLogPath()
    {
        return Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");
    }

    private static void PruneOldLogs()
    {
        try
        {
            if (!Directory.Exists(LogDirectory))
            {
                return;
            }

            var stale = Directory.GetFiles(LogDirectory, "app-*.log")
                .OrderByDescending(path => path, StringComparer.Ordinal)
                .Skip(RetainedDays);

            foreach (var path in stale)
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Retention is best effort.
        }
    }
}
