namespace WallpaperSwitcher.Desktop.ViewModels;

/// <summary>
/// What the strip at the top of the Wallpapers page is currently saying.
/// </summary>
public enum HeroState
{
    /// <summary>
    /// Nothing is configured. The strip is hidden entirely rather than shown
    /// with dead controls — the empty panel below it carries the only action.
    /// </summary>
    NoFolder,

    /// <summary>A wallpaper is applied; shows it, the set, and the next change.</summary>
    Running,

    /// <summary>The folder has gone away, so the wallpaper is stale.</summary>
    FolderMissing
}
