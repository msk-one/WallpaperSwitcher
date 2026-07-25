using Avalonia.Controls;
using Avalonia.Platform;

namespace WallpaperSwitcher.Desktop.Services;

/// <summary>
/// The window icon and the tray glyph are separate assets: the tray version is
/// simplified and transparent so it reads correctly at menu-bar size.
/// </summary>
public static class AppIcons
{
    public static WindowIcon LoadAppIcon()
    {
        return new WindowIcon(AssetLoader.Open(new Uri("avares://WallpaperSwitcher/Assets/AppIcon.png")));
    }

    public static WindowIcon LoadTrayIcon()
    {
        return new WindowIcon(AssetLoader.Open(new Uri("avares://WallpaperSwitcher/Assets/TrayIcon.png")));
    }
}
