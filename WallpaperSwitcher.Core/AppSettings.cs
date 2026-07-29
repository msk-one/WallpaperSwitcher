namespace WallpaperSwitcher;

public sealed class AppSettings
{
    /// <summary>
    /// Format marker for the settings file. Version 2 stores assignment paths
    /// relative to <see cref="WallpaperDirectory"/> and omits Ignore entries.
    /// </summary>
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public const int CurrentSchemaVersion = 2;

    public string WallpaperDirectory { get; set; } = string.Empty;

    public string? WallpaperFolderBookmark { get; set; }

    public TimeSpan DayStartsAt { get; set; } = TimeSpan.FromHours(6);

    public TimeSpan NightStartsAt { get; set; } = TimeSpan.FromHours(18);

    public ShuffleCadence ShuffleCadence { get; set; } = ShuffleCadence.Daily;

    public WallpaperFit WallpaperFit { get; set; } = WallpaperFit.Fill;

    /// <summary>
    /// Start into the tray without showing the main window. Set by the
    /// "Start minimized" checkbox and passed to the autostart entry as
    /// <c>--minimized</c>.
    /// </summary>
    public bool StartMinimized { get; set; }

    /// <summary>
    /// What the window's close button does. <see cref="WindowCloseAction.Ask"/>
    /// until the user answers the prompt with "remember this" ticked.
    /// </summary>
    public WindowCloseAction CloseAction { get; set; } = WindowCloseAction.Ask;

    public List<WallpaperAssignment> Assignments { get; set; } = [];
}

public sealed class WallpaperAssignment
{
    /// <summary>
    /// Relative to <see cref="AppSettings.WallpaperDirectory"/> when the image
    /// lives inside it, absolute otherwise.
    /// </summary>
    /// <remarks>
    /// Saving rewrites absolute paths into this form; loading does not reverse
    /// it. Callers match through
    /// <see cref="WallpaperSelectionService.ResolveCategory"/>, which tries the
    /// relative key and then the absolute one, so a settings file written before
    /// schema 2 keeps working without a migration pass.
    /// </remarks>
    public string Path { get; set; } = string.Empty;

    public WallpaperCategory Category { get; set; } = WallpaperCategory.Ignore;
}
