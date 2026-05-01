using WallpaperSwitcher;

namespace WallpaperSwitcher.Tests;

[TestClass]
public class WallpaperScheduleCalculatorTests
{
    [TestMethod]
    public void GetCurrentCategory_UsesNightWindowAcrossMidnight()
    {
        var dayStart = TimeSpan.FromHours(6);
        var nightStart = TimeSpan.FromHours(18);

        var beforeDay = new DateTime(2026, 5, 1, 5, 59, 0);
        var daytime = new DateTime(2026, 5, 1, 12, 0, 0);
        var afterNight = new DateTime(2026, 5, 1, 18, 1, 0);

        Assert.AreEqual(WallpaperCategory.Night, WallpaperScheduleCalculator.GetCurrentCategory(beforeDay, dayStart, nightStart));
        Assert.AreEqual(WallpaperCategory.Day, WallpaperScheduleCalculator.GetCurrentCategory(daytime, dayStart, nightStart));
        Assert.AreEqual(WallpaperCategory.Night, WallpaperScheduleCalculator.GetCurrentCategory(afterNight, dayStart, nightStart));
    }

    [TestMethod]
    public void BuildCycleKey_UsesLogicalDayForEarlyMorningTimes()
    {
        var now = new DateTime(2026, 5, 2, 1, 15, 0);
        var cycleKey = WallpaperScheduleCalculator.BuildCycleKey(
            now,
            WallpaperCategory.Night,
            TimeSpan.FromHours(6),
            ShuffleCadence.Daily);

        Assert.AreEqual("Night:20260501", cycleKey);
    }

    [TestMethod]
    public void GetNextTrigger_ReturnsNextNightStartWhenPhaseChangesFirst()
    {
        var now = new DateTime(2026, 5, 1, 17, 30, 0);

        var nextTrigger = WallpaperScheduleCalculator.GetNextTrigger(
            now,
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(18),
            ShuffleCadence.Daily);

        Assert.AreEqual(new DateTime(2026, 5, 1, 18, 0, 0), nextTrigger);
    }

    [TestMethod]
    public void GetNextTrigger_ReturnsHourlyBoundaryWhenItComesBeforePhaseChange()
    {
        var now = new DateTime(2026, 5, 1, 10, 10, 0);

        var nextTrigger = WallpaperScheduleCalculator.GetNextTrigger(
            now,
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(18),
            ShuffleCadence.Hourly);

        Assert.AreEqual(new DateTime(2026, 5, 1, 11, 0, 0), nextTrigger);
    }

    [TestMethod]
    public void GetNextTrigger_UsesSixHourBoundaryFromLogicalDayAnchor()
    {
        var now = new DateTime(2026, 5, 1, 11, 0, 0);

        var nextTrigger = WallpaperScheduleCalculator.GetNextTrigger(
            now,
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(18),
            ShuffleCadence.SixHours);

        Assert.AreEqual(new DateTime(2026, 5, 1, 12, 0, 0), nextTrigger);
    }

    [TestMethod]
    public void GetNextTrigger_ForWeeklyShuffleStillWakesAtPhaseChange()
    {
        var now = new DateTime(2026, 5, 1, 16, 0, 0);

        var nextTrigger = WallpaperScheduleCalculator.GetNextTrigger(
            now,
            TimeSpan.FromHours(6),
            TimeSpan.FromHours(18),
            ShuffleCadence.Weekly);

        Assert.AreEqual(new DateTime(2026, 5, 1, 18, 0, 0), nextTrigger);
    }
}
