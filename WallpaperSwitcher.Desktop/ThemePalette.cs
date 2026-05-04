using Avalonia.Media;
using Avalonia.Styling;

namespace WallpaperSwitcher.Desktop;

public sealed record ThemePalette(
    Color WindowBackground,
    Color CardBackground,
    Color Border,
    Color HeaderBackground,
    Color Text,
    Color MutedText,
    Color InputBackground,
    Color InputBorder,
    Color Accent,
    Color AccentHover,
    Color AccentText,
    Color ButtonBackground,
    Color ButtonHover,
    Color ButtonPressed,
    Color ButtonText,
    Color ActiveButtonBackground,
    Color ActiveButtonText)
{
    public static ThemePalette FromTheme(ThemeVariant theme)
    {
        var key = theme.Key?.ToString();
        var isDark = string.Equals(key, ThemeVariant.Dark.Key?.ToString(), StringComparison.OrdinalIgnoreCase);

        return isDark
            ? new ThemePalette(
                Color.Parse("#15191D"),
                Color.Parse("#20262B"),
                Color.Parse("#3B454E"),
                Color.Parse("#2A3238"),
                Color.Parse("#F3F6F8"),
                Color.Parse("#B7C0C8"),
                Color.Parse("#11171D"),
                Color.Parse("#56626D"),
                Color.Parse("#2EA77A"),
                Color.Parse("#37BC8A"),
                Colors.White,
                Color.Parse("#29313A"),
                Color.Parse("#35404A"),
                Color.Parse("#1D252D"),
                Color.Parse("#F3F6F8"),
                Color.Parse("#0A7D66"),
                Colors.White)
            : new ThemePalette(
                Color.Parse("#F4EFE7"),
                Color.Parse("#FCFAF6"),
                Color.Parse("#D9D0C4"),
                Color.Parse("#EFE7DD"),
                Color.Parse("#2F241C"),
                Color.Parse("#6A5A4C"),
                Color.Parse("#FFFFFF"),
                Color.Parse("#B8AB9B"),
                Color.Parse("#0B765F"),
                Color.Parse("#0D8A70"),
                Colors.White,
                Color.Parse("#F8F4EE"),
                Color.Parse("#EFE7DD"),
                Color.Parse("#E5D9CB"),
                Color.Parse("#2F241C"),
                Color.Parse("#0B765F"),
                Colors.White);
    }

    public SolidColorBrush Brush(Color color)
    {
        return new SolidColorBrush(color);
    }
}
