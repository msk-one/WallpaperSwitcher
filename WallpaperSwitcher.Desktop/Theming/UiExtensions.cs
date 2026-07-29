using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.MarkupExtensions;

namespace WallpaperSwitcher.Desktop.Theming;

/// <summary>
/// Small helpers that keep the code-built UI readable.
/// </summary>
public static class UiExtensions
{
    /// <summary>
    /// Binds a property to a theme resource, so it re-resolves when the OS
    /// switches between light and dark. This is what replaces the old
    /// ThemePalette and its manual rebuild of the entire visual tree.
    /// </summary>
    public static T Dyn<T>(this T target, AvaloniaProperty property, string resourceKey)
        where T : AvaloniaObject
    {
        target[!property] = new DynamicResourceExtension(resourceKey);
        return target;
    }

    /// <summary>
    /// Names a control and registers it in the template's name scope, so
    /// OnApplyTemplate can find it.
    /// </summary>
    public static T Named<T>(this T element, INameScope scope, string name)
        where T : StyledElement
    {
        element.Name = name;
        scope.Register(name, element);
        return element;
    }
}
