using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using WallpaperSwitcher.Desktop.Services;
using WallpaperSwitcher.Desktop.Theming;
using WallpaperSwitcher.Desktop.ViewModels;

namespace WallpaperSwitcher.Desktop;

public sealed class App : Application
{
    private MainWindowViewModel? _viewModel;
    private TrayMenuController? _trayMenuController;
    private MainWindow? _mainWindow;

    /// <summary>
    /// Set from the command line before the app starts. Suppresses the initial
    /// window so signing in lands straight in the tray.
    /// </summary>
    public static bool StartMinimizedRequested { get; set; }

    public override void Initialize()
    {
        Name = "Wallpaper Switcher";

        // Default means "follow the OS". Every colour in the window resolves
        // through a theme resource, so light and dark switch on their own and
        // there is no palette to rebuild by hand.
        RequestedThemeVariant = ThemeVariant.Default;

        Styles.Add(new StyleInclude(new Uri("avares://WallpaperSwitcher"))
        {
            Source = new Uri("avares://Avalonia.Themes.Fluent/FluentTheme.xaml")
        });

        Resources.MergedDictionaries.Add(FluentTokens.Create());
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settingsStore = new SettingsStore();
            var wallpaperService = new PlatformWallpaperService();
            _viewModel = new MainWindowViewModel(settingsStore, wallpaperService);

            // The tray and the scheduler are owned by the application, not by the
            // window. They used to be created in MainWindow.Opened, which meant
            // the wallpaper schedule never armed unless a window was shown.
            _trayMenuController = new TrayMenuController(
                _viewModel,
                AppIcons.LoadTrayIcon(),
                ShowMainWindow,
                QuitApplication);

            _viewModel.Start();

            if (!ShouldStartMinimized(_viewModel))
            {
                // Assigning MainWindow is what makes the classic lifetime show it,
                // so a minimized start simply never assigns one.
                ShowMainWindow();
            }
            else
            {
                AppLog.Info("Started minimized to the tray.");
            }

            desktop.Exit += (_, _) =>
            {
                _trayMenuController?.Dispose();
                _viewModel?.Dispose();
                ThumbnailCache.Instance.Dispose();
                AppLog.Info("Wallpaper Switcher exiting.");
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Called when a second launch asks this instance to surface itself.
    /// </summary>
    public void ActivateFromAnotherInstance()
    {
        ShowMainWindow();
    }

    private static bool ShouldStartMinimized(MainWindowViewModel viewModel)
    {
        if (!StartMinimizedRequested && !viewModel.StartMinimized)
        {
            return false;
        }

        // macOS reopens the wallpaper folder through a security-scoped bookmark,
        // and that needs a TopLevel to resolve. Until the macOS startup path has
        // been re-verified, a saved bookmark forces the window to be created.
        if (OperatingSystem.IsMacOS() && !string.IsNullOrWhiteSpace(viewModel.WallpaperFolderBookmark))
        {
            AppLog.Info("Ignoring minimized start: a macOS folder bookmark needs a window to restore.");
            return false;
        }

        return true;
    }

    private void ShowMainWindow()
    {
        if (_viewModel is null || ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return;
        }

        if (_mainWindow is null)
        {
            _mainWindow = new MainWindow { DataContext = _viewModel };
            desktop.MainWindow = _mainWindow;
        }

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Show();

        // See ForegroundWindow: Activate() alone cannot take the foreground from
        // another process, which is why a click on the tray icon left the window
        // behind whatever had focus.
        ForegroundWindow.Raise(_mainWindow);
    }

    /// <summary>
    /// Quit chosen from the window's close prompt.
    /// </summary>
    public void QuitFromWindow()
    {
        QuitApplication();
    }

    private void QuitApplication()
    {
        _trayMenuController?.Dispose();
        _trayMenuController = null;

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
