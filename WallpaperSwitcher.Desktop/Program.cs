using Avalonia;

namespace WallpaperSwitcher.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder
            .Configure<App>()
            .UsePlatformDetect()
            .With(new MacOSPlatformOptions
            {
                DisableSetProcessName = false
            })
            .LogToTrace();
    }
}
