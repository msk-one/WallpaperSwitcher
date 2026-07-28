using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;

// System.IO.Path comes in via ImplicitUsings and collides with the shape.
using PathShape = Avalonia.Controls.Shapes.Path;

namespace WallpaperSwitcher.Desktop.Theming;

/// <summary>
/// The icon set, as vector geometry.
/// </summary>
/// <remarks>
/// The design specifies Segoe Fluent Icons glyphs. That font ships with Windows
/// and exists on neither macOS nor Linux, where those code points render as
/// empty boxes — so using the glyphs directly would leave the app looking broken
/// on two of its three targets. Drawing the same shapes as geometry renders
/// identically everywhere and carries no font dependency.
///
/// All paths are authored in a 24x24 box so they have consistent visual weight
/// at any rendered size.
/// </remarks>
public static class Icons
{
    public sealed record Icon(string Data, bool Stroked, double StrokeThickness = 2d);

    // Filled
    public static readonly Icon Sun = new(
        "M12,7 A5,5 0 1,1 11.99,7 Z " +
        "M11,1.5 H13 V5 H11 Z M11,19 H13 V22.5 H11 Z " +
        "M1.5,11 H5 V13 H1.5 Z M19,11 H22.5 V13 H19 Z " +
        "M18.72,3.87 L20.13,5.28 L17.66,7.76 L16.24,6.34 Z " +
        "M5.28,3.87 L3.87,5.28 L6.34,7.76 L7.76,6.34 Z " +
        "M18.72,20.13 L20.13,18.72 L17.66,16.24 L16.24,17.66 Z " +
        "M5.28,20.13 L3.87,18.72 L6.34,16.24 L7.76,17.66 Z", false);

    public static readonly Icon Moon = new(
        "M13.5,2.5 A9.5,9.5 0 1,0 21.5,10.5 A7.6,7.6 0 1,1 13.5,2.5 Z", false);

    public static readonly Icon Folder = new(
        "M2.5,6 A2,2 0 0,1 4.5,4 H9.4 L11.6,6.4 H19.5 A2,2 0 0,1 21.5,8.4 V18 A2,2 0 0,1 19.5,20 H4.5 A2,2 0 0,1 2.5,18 Z", false);

    public static readonly Icon Next = new(
        "M6,5 L14.5,12 L6,19 Z M16.5,5 H19 V19 H16.5 Z", false);

    // Drawn as strokes rather than a filled gear: at 16px a filled gear's teeth
    // merge into the body and it reads as a blob.
    public static readonly Icon Settings = new(
        "M12,8.6 A3.4,3.4 0 1,1 11.99,8.6 " +
        "M12,2.2 V5.2 M12,18.8 V21.8 M2.2,12 H5.2 M18.8,12 H21.8 " +
        "M5.07,5.07 L7.19,7.19 M16.81,16.81 L18.93,18.93 " +
        "M18.93,5.07 L16.81,7.19 M7.19,16.81 L5.07,18.93", true, 2.1);

    // Even-odd so the exclamation mark is punched out of the triangle.
    public static readonly Icon Warning = new(
        "F0 M12,2.6 L22.8,21.4 H1.2 Z M11,8.6 H13 V14.8 H11 Z M11,16.4 H13 V18.4 H11 Z", false);

    // Stroked
    public static readonly Icon Refresh = new(
        "M19.9,13.4 A8,8 0 1,1 17.9,7.1 M18.4,2.4 V7.6 H13.2", true);

    public static readonly Icon Check = new("M4.5,12.5 L9.5,17.5 L19.5,6.5", true);

    public static readonly Icon Close = new("M5.5,5.5 L18.5,18.5 M18.5,5.5 L5.5,18.5", true);

    public static readonly Icon Image = new(
        "M3,5 H21 V19 H3 Z M6,16.2 L10,11.2 L13,14.7 L16,11.2 L21,16.8 " +
        "M16.6,8.6 A1.5,1.5 0 1,1 16.59,8.6", true);

    // Circle plus a slash. The slash has to be a separate subpath or the arc
    // closes into it and the "no" reading is lost.
    public static readonly Icon Block = new(
        "M12,3.6 A8.4,8.4 0 1,1 11.99,3.6 Z M6.06,6.06 L17.94,17.94", true, 2.2);

    public static readonly Icon OpenExternal = new(
        "M14,4 H20 V10 M20,4 L11.5,12.5 " +
        "M18,14 V19 A1.5,1.5 0 0,1 16.5,20.5 H5.5 A1.5,1.5 0 0,1 4,19 V8 A1.5,1.5 0 0,1 5.5,6.5 H10.5", true);

    public static readonly Icon Clock = new(
        "M12,3.5 A8.5,8.5 0 1,0 11.99,3.5 M12,7 V12.4 L15.8,14.6", true);

    /// <summary>
    /// Builds a control that renders <paramref name="icon"/> at
    /// <paramref name="size"/>, inheriting the current foreground unless a brush
    /// is given.
    /// </summary>
    public static Control Create(Icon icon, double size, IBrush? brush = null)
    {
        var geometry = Geometry.Parse(icon.Data);

        var path = new PathShape
        {
            Data = geometry,
            Stretch = Stretch.Uniform,
            Width = size,
            Height = size,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (icon.Stroked)
        {
            // Scale the stroke with the icon so a 12px glyph is not visually
            // heavier than a 20px one.
            path.StrokeThickness = icon.StrokeThickness * (size / 24d);
            path.StrokeLineCap = PenLineCap.Round;
            path.StrokeJoin = PenLineJoin.Round;

            if (brush is not null)
            {
                path.Stroke = brush;
            }
            else
            {
                path.Bind(Shape.StrokeProperty, path.GetResourceObservable("TextFillColorPrimaryBrush"));
            }
        }
        else if (brush is not null)
        {
            path.Fill = brush;
        }
        else
        {
            path.Bind(Shape.FillProperty, path.GetResourceObservable("TextFillColorPrimaryBrush"));
        }

        return path;
    }
}
