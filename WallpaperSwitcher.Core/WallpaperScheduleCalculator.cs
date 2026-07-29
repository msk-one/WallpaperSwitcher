using System.Globalization;

namespace WallpaperSwitcher;

public static class WallpaperScheduleCalculator
{
    public static WallpaperCategory GetCurrentCategory(DateTime now, TimeSpan dayStart, TimeSpan nightStart)
    {
        return IsWithinWindow(now.TimeOfDay, nightStart, dayStart)
            ? WallpaperCategory.Night
            : WallpaperCategory.Day;
    }

    public static string BuildCycleKey(DateTime now, WallpaperCategory category, TimeSpan dayStart, ShuffleCadence cadence)
    {
        var anchor = GetLogicalDayAnchor(now, dayStart);
        var logicalDate = anchor.Date;
        var elapsed = now - anchor;

        return cadence switch
        {
            ShuffleCadence.Hourly => $"{category}:{logicalDate:yyyyMMdd}:h{Math.Floor(elapsed.TotalHours):00}",
            ShuffleCadence.SixHours => $"{category}:{logicalDate:yyyyMMdd}:s{Math.Floor(elapsed.TotalHours / 6d):00}",
            ShuffleCadence.Daily => $"{category}:{logicalDate:yyyyMMdd}",
            ShuffleCadence.Weekly => $"{category}:{ISOWeek.GetYear(logicalDate)}:w{ISOWeek.GetWeekOfYear(logicalDate):00}",
            _ => $"{category}:{logicalDate:yyyyMMdd}"
        };
    }

    public static DateTime GetNextTrigger(DateTime now, TimeSpan dayStart, TimeSpan nightStart, ShuffleCadence cadence)
    {
        var nextPhaseChange = GetNextPhaseChange(now, dayStart, nightStart);
        var nextShuffleBoundary = GetNextShuffleBoundary(now, dayStart, cadence);
        return nextPhaseChange <= nextShuffleBoundary ? nextPhaseChange : nextShuffleBoundary;
    }

    private static DateTime GetNextPhaseChange(DateTime now, TimeSpan dayStart, TimeSpan nightStart)
    {
        var candidates = new[]
        {
            now.Date.Add(dayStart),
            now.Date.Add(nightStart),
            now.Date.AddDays(1).Add(dayStart),
            now.Date.AddDays(1).Add(nightStart)
        };

        return candidates
            .Where(candidate => candidate > now)
            .Min();
    }

    private static DateTime GetNextShuffleBoundary(DateTime now, TimeSpan dayStart, ShuffleCadence cadence)
    {
        return cadence switch
        {
            ShuffleCadence.Hourly => GetNextIntervalBoundary(now, dayStart, TimeSpan.FromHours(1)),
            ShuffleCadence.SixHours => GetNextIntervalBoundary(now, dayStart, TimeSpan.FromHours(6)),
            _ => DateTime.MaxValue
        };
    }

    private static DateTime GetNextIntervalBoundary(DateTime now, TimeSpan dayStart, TimeSpan interval)
    {
        var anchor = GetLogicalDayAnchor(now, dayStart);
        var elapsed = now - anchor;
        var completedWindows = Math.Floor(elapsed.TotalSeconds / interval.TotalSeconds);
        var nextBoundary = anchor.Add(TimeSpan.FromSeconds((completedWindows + 1) * interval.TotalSeconds));

        return nextBoundary > now ? nextBoundary : nextBoundary.Add(interval);
    }

    private static DateTime GetLogicalDayAnchor(DateTime now, TimeSpan dayStart)
    {
        return now.TimeOfDay < dayStart
            ? now.Date.AddDays(-1).Add(dayStart)
            : now.Date.Add(dayStart);
    }

    private static bool IsWithinWindow(TimeSpan currentTime, TimeSpan start, TimeSpan end)
    {
        return start < end
            ? currentTime >= start && currentTime < end
            : currentTime >= start || currentTime < end;
    }
}
