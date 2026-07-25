using Avalonia;
using Avalonia.Threading;
using WallpaperSwitcher.Desktop.Services;

namespace WallpaperSwitcher.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        AppLog.Initialize();

        if (!SingleInstanceGuard.TryAcquire(out var singleInstance))
        {
            AppLog.Info("Another instance is already running; asked it to show its window and exiting.");
            return 0;
        }

        App.StartMinimizedRequested = args.Any(arg =>
            string.Equals(arg, LaunchAtLoginService.MinimizedArgument, StringComparison.OrdinalIgnoreCase));

        InstallGlobalExceptionHandlers();

        try
        {
            var builder = BuildAvaloniaApp();

            singleInstance?.ListenForActivation(() =>
                Dispatcher.UIThread.Post(() => (Application.Current as App)?.ActivateFromAnotherInstance()));

            return builder.StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            AppLog.Error("Fatal error; the application is shutting down.", ex);
            throw;
        }
        finally
        {
            singleInstance?.Dispose();
        }
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

    /// <summary>
    /// A tray-resident app has no window to show an error in, so without these an
    /// unhandled exception looks to the user like the app simply vanished.
    /// </summary>
    private static void InstallGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            AppLog.Error("Unhandled exception.", eventArgs.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, eventArgs) =>
        {
            AppLog.Error("Unobserved task exception.", eventArgs.Exception);
            eventArgs.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, eventArgs) =>
        {
            AppLog.Error("Unhandled exception on the UI thread.", eventArgs.Exception);

            // Keep the app alive. Losing a click is recoverable; losing the
            // scheduler because a thumbnail failed to decode is not.
            eventArgs.Handled = true;
        };
    }
}
