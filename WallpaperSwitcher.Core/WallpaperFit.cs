namespace WallpaperSwitcher;

/// <summary>
/// How the wallpaper image is scaled onto the desktop. Currently honoured on
/// Windows only; macOS and Linux desktops manage this themselves.
/// </summary>
public enum WallpaperFit
{
    /// <summary>Scale to cover the screen, cropping the overflow.</summary>
    Fill,

    /// <summary>Scale to fit entirely on screen, letterboxing the remainder.</summary>
    Fit,

    /// <summary>Stretch to the screen size, ignoring aspect ratio.</summary>
    Stretch,

    /// <summary>Draw at native size in the middle of the screen.</summary>
    Center,

    /// <summary>Repeat at native size across the screen.</summary>
    Tile,

    /// <summary>Stretch a single image across all monitors.</summary>
    Span
}
