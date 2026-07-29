namespace WallpaperSwitcher;

public sealed record WindowCloseActionOption(WindowCloseAction Value, string Label)
{
    public override string ToString() => Label;
}
