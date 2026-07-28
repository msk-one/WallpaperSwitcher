using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace WallpaperSwitcher.Desktop.Theming;

/// <summary>
/// The WinUI semantic colour layer the design is specified against, as a theme
/// dictionary the app merges under the Fluent theme.
/// </summary>
/// <remarks>
/// Avalonia's own Fluent theme ships per-control keys (ButtonBackground,
/// CheckBoxForegroundChecked, and so on) but not the semantic layer that WinUI
/// names — verified by enumerating every key the theme exposes in both Avalonia
/// 11.3 and 12.0. Those names come from WinUI, which FluentAvalonia repackages;
/// FluentAvalonia is net10.0-only at 3.x and Avalonia-11-only at the net9.0
/// compatible 2.4.1, so neither fits this project.
///
/// The values below are WinUI's published light and dark values, not invented
/// ones. Keeping the WinUI names means the design hands off literally, and every
/// colour in the window resolves through one dictionary that swaps with
/// ThemeVariant — which is what replaces the old ThemePalette and its manual
/// ActualThemeVariantChanged rebuild.
/// </remarks>
public static class FluentTokens
{
    public static ResourceDictionary Create()
    {
        var resources = new ResourceDictionary();
        resources.ThemeDictionaries[ThemeVariant.Light] = BuildLight();
        resources.ThemeDictionaries[ThemeVariant.Dark] = BuildDark();
        return resources;
    }

    private static ResourceDictionary BuildLight()
    {
        var d = new ResourceDictionary();

        // Backgrounds
        Add(d, "SolidBackgroundFillColorBaseBrush", 0xFF, 0xF3, 0xF3, 0xF3);
        Add(d, "LayerFillColorDefaultBrush", 0xFF, 0xF9, 0xF9, 0xF9);
        Add(d, "CardBackgroundFillColorDefaultBrush", 0xFF, 0xFB, 0xFB, 0xFB);
        Add(d, "CardBackgroundFillColorSecondaryBrush", 0xFF, 0xFF, 0xFF, 0xFF);
        Add(d, "ControlAltFillColorSecondaryBrush", 0xFF, 0xEB, 0xEB, 0xEB);
        Add(d, "SubtleFillColorSecondaryBrush", 0x09, 0x00, 0x00, 0x00);
        Add(d, "SubtleFillColorTertiaryBrush", 0x06, 0x00, 0x00, 0x00);
        Add(d, "ControlFillColorDefaultBrush", 0xB3, 0xFF, 0xFF, 0xFF);
        Add(d, "ControlFillColorSecondaryBrush", 0x80, 0xF9, 0xF9, 0xF9);
        Add(d, "ControlFillColorTertiaryBrush", 0x4D, 0xF9, 0xF9, 0xF9);
        Add(d, "FooterFillColorBrush", 0x06, 0x00, 0x00, 0x00);

        // Strokes
        Add(d, "ControlStrokeColorDefaultBrush", 0x0F, 0x00, 0x00, 0x00);
        Add(d, "CardStrokeColorDefaultBrush", 0x0F, 0x00, 0x00, 0x00);
        Add(d, "DividerStrokeColorDefaultBrush", 0x14, 0x00, 0x00, 0x00);
        Add(d, "SurfaceStrokeColorDefaultBrush", 0x66, 0x75, 0x75, 0x75);
        Add(d, "FocusStrokeColorOuterBrush", 0xE4, 0x00, 0x00, 0x00);

        // Text
        Add(d, "TextFillColorPrimaryBrush", 0xE4, 0x00, 0x00, 0x00);
        Add(d, "TextFillColorSecondaryBrush", 0x9B, 0x00, 0x00, 0x00);
        Add(d, "TextFillColorTertiaryBrush", 0x72, 0x00, 0x00, 0x00);
        Add(d, "TextOnAccentFillColorPrimaryBrush", 0xFF, 0xFF, 0xFF, 0xFF);

        // Accent
        Add(d, "AccentFillColorDefaultBrush", 0xFF, 0x00, 0x5F, 0xB8);
        Add(d, "AccentFillColorSecondaryBrush", 0xE6, 0x00, 0x5F, 0xB8);
        Add(d, "AccentFillColorTertiaryBrush", 0xCC, 0x00, 0x5F, 0xB8);

        // Semantic
        Add(d, "SystemFillColorCriticalBrush", 0xFF, 0xC4, 0x2B, 0x1C);

        // Schedule bar. The night band is a scrim over the track rather than a
        // named WinUI colour, because WinUI has no token for "the other half".
        Add(d, "ScheduleNightFillBrush", 0x6B, 0x00, 0x00, 0x00);
        Add(d, "ScheduleNightForegroundBrush", 0xFF, 0xFF, 0xFF, 0xFF);

        return d;
    }

    private static ResourceDictionary BuildDark()
    {
        var d = new ResourceDictionary();

        Add(d, "SolidBackgroundFillColorBaseBrush", 0xFF, 0x20, 0x20, 0x20);
        Add(d, "LayerFillColorDefaultBrush", 0xFF, 0x27, 0x27, 0x27);
        Add(d, "CardBackgroundFillColorDefaultBrush", 0xFF, 0x2B, 0x2B, 0x2B);
        Add(d, "CardBackgroundFillColorSecondaryBrush", 0xFF, 0x32, 0x32, 0x32);
        Add(d, "ControlAltFillColorSecondaryBrush", 0xFF, 0x3A, 0x3A, 0x3A);
        Add(d, "SubtleFillColorSecondaryBrush", 0x0F, 0xFF, 0xFF, 0xFF);
        Add(d, "SubtleFillColorTertiaryBrush", 0x0A, 0xFF, 0xFF, 0xFF);
        Add(d, "ControlFillColorDefaultBrush", 0x0F, 0xFF, 0xFF, 0xFF);
        Add(d, "ControlFillColorSecondaryBrush", 0x15, 0xFF, 0xFF, 0xFF);
        Add(d, "ControlFillColorTertiaryBrush", 0x08, 0xFF, 0xFF, 0xFF);
        Add(d, "FooterFillColorBrush", 0x0B, 0xFF, 0xFF, 0xFF);

        Add(d, "ControlStrokeColorDefaultBrush", 0x12, 0xFF, 0xFF, 0xFF);
        Add(d, "CardStrokeColorDefaultBrush", 0x19, 0x00, 0x00, 0x00);
        Add(d, "DividerStrokeColorDefaultBrush", 0x15, 0xFF, 0xFF, 0xFF);
        Add(d, "SurfaceStrokeColorDefaultBrush", 0x66, 0x75, 0x75, 0x75);
        Add(d, "FocusStrokeColorOuterBrush", 0xFF, 0xFF, 0xFF, 0xFF);

        Add(d, "TextFillColorPrimaryBrush", 0xFF, 0xFF, 0xFF, 0xFF);
        Add(d, "TextFillColorSecondaryBrush", 0xC8, 0xFF, 0xFF, 0xFF);
        Add(d, "TextFillColorTertiaryBrush", 0x8B, 0xFF, 0xFF, 0xFF);
        Add(d, "TextOnAccentFillColorPrimaryBrush", 0xFF, 0x00, 0x00, 0x00);

        Add(d, "AccentFillColorDefaultBrush", 0xFF, 0x60, 0xCD, 0xFF);
        Add(d, "AccentFillColorSecondaryBrush", 0xE6, 0x60, 0xCD, 0xFF);
        Add(d, "AccentFillColorTertiaryBrush", 0xCC, 0x60, 0xCD, 0xFF);

        Add(d, "SystemFillColorCriticalBrush", 0xFF, 0xFF, 0x99, 0xA4);

        Add(d, "ScheduleNightFillBrush", 0x57, 0xFF, 0xFF, 0xFF);
        Add(d, "ScheduleNightForegroundBrush", 0xFF, 0x14, 0x14, 0x14);

        return d;
    }

    private static void Add(ResourceDictionary target, string key, byte a, byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.ToImmutable();
        target[key] = brush;
    }
}
