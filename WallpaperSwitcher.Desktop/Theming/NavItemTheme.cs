using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;

namespace WallpaperSwitcher.Desktop.Theming;

/// <summary>
/// Styles the tab strip as a Fluent left navigation rail.
/// </summary>
/// <remarks>
/// A TabControl rather than a bespoke list, because tab semantics are what a
/// screen reader should hear for two mutually exclusive destinations, and they
/// come for free. The visuals are the design's: 36-high items, 4px radius, 2px
/// apart, a 16px icon column, and a 3px accent pill down the left of the
/// selected item, inset 8px top and bottom.
/// </remarks>
public static class NavItemTheme
{
    public static ControlTheme Create()
    {
        var theme = new ControlTheme(typeof(TabItem))
        {
            Setters =
            {
                new Setter(Layoutable.HeightProperty, 36d),
                new Setter(Layoutable.MarginProperty, new Thickness(0, 1)),
                new Setter(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Stretch),
                new Setter(TemplatedControl.PaddingProperty, new Thickness(12, 0)),
                new Setter(TemplatedControl.FontSizeProperty, 14d),
                new Setter(TemplatedControl.TemplateProperty, BuildTemplate())
            }
        };

        // Unselected is expressed as a style, not as a local value in the
        // template. A property assigned in the template factory is a LocalValue,
        // which outranks every Style setter, so setting Background/IsVisible
        // there made the :selected setters below silently unreachable and the
        // rail never showed which page you were on.
        var normal = new Style(x => x.Nesting().Template().Name("PART_Background"))
        {
            Setters = { new Setter(Border.BackgroundProperty, Brushes.Transparent) }
        };
        theme.Add(normal);

        var pillHidden = new Style(x => x.Nesting().Template().Name("PART_Pill"))
        {
            Setters = { new Setter(Visual.IsVisibleProperty, false) }
        };
        theme.Add(pillHidden);

        // Selected: subtle fill plus the accent pill.
        var selected = new Style(x => x.Nesting().Class(":selected"));
        selected.Children.Add(new Style(x => x.Nesting().Template().Name("PART_Background"))
        {
            Setters = { new Setter(Border.BackgroundProperty, Dynamic("SubtleFillColorSecondaryBrush")) }
        });
        selected.Children.Add(new Style(x => x.Nesting().Template().Name("PART_Pill"))
        {
            Setters = { new Setter(Visual.IsVisibleProperty, true) }
        });
        theme.Add(selected);

        // Pointer over, when not already selected.
        var hover = new Style(x => x.Nesting().Class(":pointerover").Not(y => y.Nesting().Class(":selected")));
        hover.Children.Add(new Style(x => x.Nesting().Template().Name("PART_Background"))
        {
            Setters = { new Setter(Border.BackgroundProperty, Dynamic("SubtleFillColorTertiaryBrush")) }
        });
        theme.Add(hover);

        return theme;
    }

    private static Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension Dynamic(string key) => new(key);

    private static FuncControlTemplate<TabItem> BuildTemplate() => new((item, scope) =>
    {
        var background = new Border
        {
            CornerRadius = new CornerRadius(4)
        }.Named(scope, "PART_Background");

        var layout = new Panel();

        var pill = new Border
        {
            Width = 3,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 8)
        }.Dyn(Border.BackgroundProperty, "AccentFillColorDefaultBrush").Named(scope, "PART_Pill");
        layout.Children.Add(pill);

        var content = new ContentPresenter
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            [!ContentPresenter.ContentProperty] = item[~HeaderedContentControl.HeaderProperty],
            [!ContentPresenter.PaddingProperty] = item[~TemplatedControl.PaddingProperty],
            [!ContentPresenter.ForegroundProperty] = item[~TemplatedControl.ForegroundProperty]
        }.Named(scope, "PART_ContentPresenter");
        layout.Children.Add(content);

        background.Child = layout;
        return background;
    });
}
