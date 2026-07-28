namespace WallpaperSwitcher;

/// <summary>
/// What clicking the window's close button does.
/// </summary>
/// <remarks>
/// A tray app closing to the tray surprises people who meant to quit, and one
/// that quits surprises people who meant to tuck it away. Asking once and
/// remembering the answer avoids guessing on the user's behalf.
/// </remarks>
public enum WindowCloseAction
{
    /// <summary>Prompt, offering to remember the answer.</summary>
    Ask,

    /// <summary>Hide the window and keep the schedule running in the tray.</summary>
    MinimizeToTray,

    /// <summary>Exit the application.</summary>
    Quit
}
