using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using WallpaperSwitcher.Desktop.Services;
using WallpaperSwitcher.Desktop.ViewModels;

namespace WallpaperSwitcher.Desktop;

public sealed class App : Application
{
    public override void Initialize()
    {
        Name = "Wallpaper Switcher";
        RequestedThemeVariant = ThemeVariant.Default;
        Styles.Add(new StyleInclude(new Uri("avares://WallpaperSwitcher"))
        {
            Source = new Uri("avares://Avalonia.Themes.Fluent/FluentTheme.xaml")
        });
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var settingsStore = new SettingsStore();
            var wallpaperService = new PlatformWallpaperService();
            var viewModel = new MainWindowViewModel(settingsStore, wallpaperService);

            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel
            };

            desktop.Exit += (_, _) => viewModel.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
