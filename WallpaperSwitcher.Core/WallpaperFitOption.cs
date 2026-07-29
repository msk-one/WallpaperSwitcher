namespace WallpaperSwitcher;

public sealed record WallpaperFitOption(WallpaperFit Value, string Label)
{
    public override string ToString() => Label;
}
