using Avalonia.Threading;

namespace WallpaperSwitcher.Desktop.Services;

public sealed class WallpaperScheduler : IDisposable
{
    private static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(1);

    private readonly Action _onDue;
    private readonly Timer _timer;

    public WallpaperScheduler(Action onDue)
    {
        _onDue = onDue;
        _timer = new Timer(
            _ => Dispatcher.UIThread.Post(_onDue),
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
    }

    public void Cancel()
    {
        _timer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }
}
