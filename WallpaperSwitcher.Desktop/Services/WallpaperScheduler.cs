using Avalonia.Threading;

namespace WallpaperSwitcher.Desktop.Services;

public sealed class WallpaperScheduler : IDisposable
{
    private static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan WatchdogInterval = TimeSpan.FromMinutes(1);

    private readonly Action _onDue;
    private readonly Action _onWatchdog;
    private readonly Timer _timer;
    private readonly Timer _watchdogTimer;

    public WallpaperScheduler(Action onDue, Action onWatchdog)
    {
        _onDue = onDue;
        _onWatchdog = onWatchdog;
        _timer = new Timer(
            _ => Dispatcher.UIThread.Post(_onDue),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
        _watchdogTimer = new Timer(
            _ => Dispatcher.UIThread.Post(_onWatchdog),
            null,
            Timeout.InfiniteTimeSpan,
            Timeout.InfiniteTimeSpan);
    }

    public void Schedule(DateTime now, TimeSpan dayStart, TimeSpan nightStart, ShuffleCadence cadence)
    {
        var nextTrigger = WallpaperScheduleCalculator.GetNextTrigger(now, dayStart, nightStart, cadence);
        var delay = nextTrigger - now;
        if (delay < MinimumDelay)
        {
            delay = MinimumDelay;
        }

        _timer.Change(delay, Timeout.InfiniteTimeSpan);
        _watchdogTimer.Change(WatchdogInterval, WatchdogInterval);
    }

    public void Cancel()
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _watchdogTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _watchdogTimer.Dispose();
    }
}
