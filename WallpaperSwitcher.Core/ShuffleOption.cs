namespace WallpaperSwitcher;

public sealed record ShuffleOption(ShuffleCadence Value, string Label)
{
    public override string ToString() => Label;
}
