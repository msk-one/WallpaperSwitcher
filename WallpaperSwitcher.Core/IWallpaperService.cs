namespace WallpaperSwitcher;

public interface IWallpaperService
{
    bool TryApply(string wallpaperPath, out string? errorMessage);
}
