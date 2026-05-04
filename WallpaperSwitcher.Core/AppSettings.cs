namespace WallpaperSwitcher;

public sealed class AppSettings
{
    public string WallpaperDirectory { get; set; } = string.Empty;

    public string? WallpaperFolderBookmark { get; set; }

    public TimeSpan DayStartsAt { get; set; } = TimeSpan.FromHours(6);

    public TimeSpan NightStartsAt { get; set; } = TimeSpan.FromHours(18);

    public ShuffleCadence ShuffleCadence { get; set; } = ShuffleCadence.Daily;

    public List<WallpaperAssignment> Assignments { get; set; } = [];
}

public sealed class WallpaperAssignment
{
    public string Path { get; set; } = string.Empty;

    public WallpaperCategory Category { get; set; } = WallpaperCategory.Ignore;
}
