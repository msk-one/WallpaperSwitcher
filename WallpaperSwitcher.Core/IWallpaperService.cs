namespace WallpaperSwitcher;

public interface IWallpaperService
{
    bool TryApply(string wallpaperPath, WallpaperFit fit, out string? errorMessage);
}
