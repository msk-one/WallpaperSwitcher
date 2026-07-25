namespace WallpaperSwitcher;

/// <summary>
/// Formats and parses the command string stored under
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c>.
/// </summary>
/// <remarks>
/// Pure string handling, kept out of the registry code so it can be tested.
/// Windows Run values use plain quoting — the backslash escaping that a
/// freedesktop <c>.desktop</c> Exec line needs would make the value fail to
/// match the executable path when it is read back.
/// </remarks>
public static class WindowsRunCommand
{
    public static string Format(string executablePath, string? argument = null)
    {
        var quoted = "\"" + executablePath + "\"";
        return string.IsNullOrWhiteSpace(argument) ? quoted : $"{quoted} {argument}";
    }

    /// <summary>
    /// Recovers the executable path from a Run value such as
    /// <c>"C:\Apps\WallpaperSwitcher.exe" --minimized</c>. Returns <c>null</c>
    /// when the value cannot be interpreted as a path.
    /// </summary>
    public static string? ParseExecutablePath(string? registryValue)
    {
        if (string.IsNullOrWhiteSpace(registryValue))
        {
            return null;
        }

        var value = registryValue.Trim();
        string candidate;

        if (value.StartsWith('"'))
        {
            var closingQuote = value.IndexOf('"', 1);
            if (closingQuote <= 1)
            {
                return null;
            }

            candidate = value[1..closingQuote];
        }
        else
        {
            var firstSpace = value.IndexOf(' ');
            candidate = firstSpace < 0 ? value : value[..firstSpace];
        }

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        try
        {
            return Path.GetFullPath(candidate);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }
}
