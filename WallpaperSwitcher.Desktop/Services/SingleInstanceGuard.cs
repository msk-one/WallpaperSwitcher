using System.IO.Pipes;
using System.Diagnostics;

namespace WallpaperSwitcher.Desktop.Services;

/// <summary>
/// Ensures only one copy of the app runs per logged-in user, and gives a second
/// launch a way to bring the first one's window to the front.
/// </summary>
/// <remarks>
/// Without this, enabling "Start at login" and then launching from the Start
/// menu produces two processes, each with its own tray icon and its own
/// scheduler, taking turns overwriting the desktop wallpaper.
///
/// Windows only for now. Linux and macOS have their own single-instance
/// conventions and neither has shipped the autostart-plus-manual-launch path
/// that makes this acute.
/// </remarks>
public sealed class SingleInstanceGuard : IDisposable
{
    private const string ActivateMessage = "SHOW";
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromMilliseconds(500);

    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private CancellationTokenSource? _listenerCancellation;

    private SingleInstanceGuard(Mutex mutex, string pipeName)
    {
        _mutex = mutex;
        _pipeName = pipeName;
    }

    /// <summary>
    /// Attempts to become the primary instance. Returns <c>false</c> when another
    /// instance already holds the lock, in which case it has been asked to show
    /// its window and this process should exit.
    /// </summary>
    public static bool TryAcquire(out SingleInstanceGuard? guard)
    {
        guard = null;

        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        // "Local\" scopes the mutex to the logon session so Fast User Switching
        // still gives each signed-in user their own instance. "Global\" would
        // let the first user's app block everyone else's.
        var mutex = new Mutex(initiallyOwned: true, $"Local\\WallpaperSwitcher.SingleInstance.{SessionId()}", out var createdNew);

        if (!createdNew)
        {
            mutex.Dispose();
            SignalExistingInstance();
            return false;
        }

        guard = new SingleInstanceGuard(mutex, PipeName());
        return true;
    }

    /// <summary>
    /// Starts listening for activation requests from later launches.
    /// </summary>
    public void ListenForActivation(Action onActivate)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _listenerCancellation = new CancellationTokenSource();
        _ = Task.Run(() => ListenAsync(onActivate, _listenerCancellation.Token));
    }

    public void Dispose()
    {
        _listenerCancellation?.Cancel();
        _listenerCancellation?.Dispose();

        try
        {
            _mutex.ReleaseMutex();
        }
        catch (ApplicationException)
        {
            // Not the owning thread; the handle close below still frees it.
        }

        _mutex.Dispose();
    }

    private async Task ListenAsync(Action onActivate, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                using var reader = new StreamReader(server);
                var message = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);

                if (string.Equals(message, ActivateMessage, StringComparison.Ordinal))
                {
                    AppLog.Info("Another launch requested activation; showing the window.");
                    onActivate();
                }
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                AppLog.Warn($"Activation listener error: {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(1), CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    private static void SignalExistingInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName(), PipeDirection.Out);
            client.Connect((int)ConnectTimeout.TotalMilliseconds);

            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(ActivateMessage);
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            // The other instance may be mid-shutdown or not listening yet. Exiting
            // quietly is still better than running a second scheduler.
        }
    }

    private static string PipeName()
    {
        return $"WallpaperSwitcher.Activate.{SessionId()}";
    }

    private static int SessionId()
    {
        using var process = Process.GetCurrentProcess();
        return process.SessionId;
    }
}
