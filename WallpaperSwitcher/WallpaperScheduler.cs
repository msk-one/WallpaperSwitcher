using System.Windows.Threading;

namespace WallpaperSwitcher;

public sealed class WallpaperScheduler : IDisposable
{
    private static readonly TimeSpan MinimumDelay = TimeSpan.FromSeconds(1);

    private readonly Dispatcher _dispatcher;
    private readonly Action _onDue;
    private readonly System.Threading.Timer _timer;

    public WallpaperScheduler(Dispatcher dispatcher, Action onDue)
    {
        _dispatcher = dispatcher;
        _onDue = onDue;
        _timer = new System.Threading.Timer(
            _ => _dispatcher.InvokeAsync(_onDue),
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
