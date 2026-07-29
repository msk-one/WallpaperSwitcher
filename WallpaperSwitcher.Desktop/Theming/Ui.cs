using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;

namespace WallpaperSwitcher.Desktop.Theming;

/// <summary>
/// Builders for the repeated pieces of the window.
/// </summary>
/// <remarks>
/// Everything interactive here is a real <see cref="Button"/>. Nothing is a
/// Border with pointer handlers, which is what closes the accessibility gap the
/// design notes recorded: these are focusable, activate on Space and Enter, keep
/// the Fluent focus adorner, and report themselves to a screen reader.
/// </remarks>
public static class Ui
{
    public const double ControlHeight = 32;

    public static Button Button(string text, Icons.Icon? icon, Action onClick)
    {
        var button = new Button
        {
            Height = ControlHeight,
            Padding = new Thickness(12, 0),
            CornerRadius = new CornerRadius(4),
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = BuildLabel(text, icon)
        };

        button.Click += (_, _) => onClick();
        return button;
    }

    public static Button AccentButton(string text, Action onClick)
    {
        var button = Button(text, null, onClick);
        button.Classes.Add("accent");
        return button;
    }

    public static Button AccentButton(string text, Func<Task> onClick)
    {
        var button = new Button
        {
            Height = ControlHeight,
            Padding = new Thickness(12, 0),
            CornerRadius = new CornerRadius(4),
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = BuildLabel(text, null)
        };
        button.Classes.Add("accent");
        button.Click += async (_, _) => await onClick();
        return button;
    }

    /// <summary>A 32x32 square button holding a single icon.</summary>
    public static Button IconButton(Icons.Icon icon, string accessibleName, Action onClick)
    {
        var button = new Button
        {
            Width = ControlHeight,
            Height = ControlHeight,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(4),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = Icons.Create(icon, 14)
        };

        button.Click += (_, _) => onClick();
        Avalonia.Automation.AutomationProperties.SetName(button, accessibleName);
        ToolTip.SetTip(button, accessibleName);
        return button;
    }

    private static Control BuildLabel(string text, Icons.Icon? icon)
    {
        var label = new TextBlock
        {
            Text = text,
            FontSize = 14,
            LineHeight = 20,
            VerticalAlignment = VerticalAlignment.Center
        };

        if (icon is null)
        {
            return label;
        }

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(Icons.Create(icon, 14));
        row.Children.Add(label);
        return row;
    }

    /// <summary>Section heading, as used above Source, Schedule and App.</summary>
    public static Control SectionHeading(string text, double topMargin) =>
        new TextBlock
        {
            Text = text,
            FontSize = 14,
            LineHeight = 20,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, topMargin, 0, 6)
        }.Dyn(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");

    /// <summary>A card surface: 4px radius, hairline stroke, card fill.</summary>
    public static Border Card(double minHeight = 0, Thickness? padding = null) =>
        new Border
        {
            MinHeight = minHeight,
            Padding = padding ?? new Thickness(14, 10),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1)
        }
            .Dyn(Border.BackgroundProperty, "CardBackgroundFillColorDefaultBrush")
            .Dyn(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");

    /// <summary>
    /// Finds a named element inside a built (not templated) subtree. The tiles
    /// build their own content, so the usual template name scope does not apply.
    /// </summary>
    public static T? FindDescendant<T>(this Control root, string name) where T : Control
    {
        if (root is T match && root.Name == name)
        {
            return match;
        }

        foreach (var child in root.GetLogicalChildren())
        {
            if (child is Control control && control.FindDescendant<T>(name) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
