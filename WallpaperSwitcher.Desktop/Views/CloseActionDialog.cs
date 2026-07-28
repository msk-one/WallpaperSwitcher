using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using WallpaperSwitcher.Desktop.Theming;

namespace WallpaperSwitcher.Desktop.Views;

/// <summary>
/// Asks whether closing the window should leave the app running in the tray or
/// quit it, and offers to remember the answer.
/// </summary>
public sealed class CloseActionDialog : Window
{
    private CloseActionDialog()
    {
        Title = "Close Wallpaper Switcher";
        SizeToContent = SizeToContent.Height;
        Width = 420;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        this.Dyn(BackgroundProperty, "SolidBackgroundFillColorBaseBrush");

        // A modal question should not offer minimize, maximize or full screen.
        foreach (var role in new[]
        {
            WindowDecorationsElementRole.MinimizeButton,
            WindowDecorationsElementRole.MaximizeButton,
            WindowDecorationsElementRole.FullScreenButton
        })
        {
            Styles.Add(new Style(x => x.PropertyEquals(WindowDecorationProperties.ElementRoleProperty, role))
            {
                Setters = { new Setter(IsVisibleProperty, false) }
            });
        }
    }

    /// <summary>
    /// Returns the action the user chose, and whether to persist it. A dismissed
    /// dialog (Escape or its own close button) returns null, which cancels the
    /// close entirely.
    /// </summary>
    public static async Task<(WindowCloseAction Action, bool Remember)?> AskAsync(Window owner)
    {
        var dialog = new CloseActionDialog();
        (WindowCloseAction Action, bool Remember)? result = null;

        var remember = new CheckBox
        {
            Content = "Remember my choice",
            FontSize = 13,
            Margin = new Thickness(0, 4, 0, 0)
        };
        AutomationProperties.SetName(remember, "Remember my choice");

        var body = new StackPanel { Spacing = 10, Margin = new Thickness(20, 18, 20, 18) };

        body.Children.Add(new TextBlock
        {
            Text = "Keep Wallpaper Switcher running?",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            TextWrapping = TextWrapping.Wrap
        }.Dyn(ForegroundProperty, "TextFillColorPrimaryBrush"));

        body.Children.Add(new TextBlock
        {
            Text = "Left running, it keeps changing your wallpaper on schedule from the tray. "
                + "Quitting stops the schedule until you open it again.",
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush"));

        body.Children.Add(remember);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var keepRunning = new Button
        {
            Content = "Keep running",
            MinWidth = 120,
            Height = 32,
            IsDefault = true
        };
        keepRunning.Classes.Add("accent");
        keepRunning.Click += (_, _) =>
        {
            result = (WindowCloseAction.MinimizeToTray, remember.IsChecked == true);
            dialog.Close();
        };

        var quit = new Button
        {
            Content = "Quit",
            MinWidth = 120,
            Height = 32
        };
        quit.Click += (_, _) =>
        {
            result = (WindowCloseAction.Quit, remember.IsChecked == true);
            dialog.Close();
        };

        // Keep running is the safer default, so it is both first and the Enter key.
        buttons.Children.Add(keepRunning);
        buttons.Children.Add(quit);
        body.Children.Add(buttons);

        dialog.Content = body;
        await dialog.ShowDialog(owner);
        return result;
    }
}
