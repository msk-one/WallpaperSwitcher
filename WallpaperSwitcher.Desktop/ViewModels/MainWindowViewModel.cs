using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using WallpaperSwitcher.Desktop.Services;

namespace WallpaperSwitcher.Desktop.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly SettingsStore _settingsStore;
    private readonly IWallpaperService _wallpaperService;
    private readonly WallpaperScheduler _scheduler;
    private readonly IReadOnlyList<ShuffleOption> _shuffleOptions =
    [
        new(ShuffleCadence.Hourly, "Every hour"),
        new(ShuffleCadence.SixHours, "Every 6 hours"),
        new(ShuffleCadence.Daily, "Each day"),
        new(ShuffleCadence.Weekly, "Each week")
    ];

    private string _wallpaperDirectory = string.Empty;
    private string _dayStartText = "06:00";
    private string _nightStartText = "18:00";
    private ShuffleOption _selectedShuffleOption;
    private WallpaperItem? _selectedWallpaper;
    private string _statusMessage = "Choose a folder with wallpapers to get started.";
    private string? _lastAppliedPath;
    private string? _lastAppliedCycleKey;
    private string _loadedWallpaperDirectory = string.Empty;
    private string? _wallpaperFolderBookmark;
    private bool _startAtLogin;

    public MainWindowViewModel(SettingsStore settingsStore, IWallpaperService wallpaperService)
    {
        _settingsStore = settingsStore;
        _wallpaperService = wallpaperService;
        _scheduler = new WallpaperScheduler(
            () => ApplyWallpaperAndReschedule(forceApply: true),
            () => ApplyWallpaperAndReschedule(forceApply: false));

        _selectedShuffleOption = _shuffleOptions.First(option => option.Value == ShuffleCadence.Daily);
        _startAtLogin = LaunchAtLoginService.IsEnabled();
        LoadSettings();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<WallpaperItem> WallpaperItems { get; } = [];

    public IReadOnlyList<ShuffleOption> ShuffleOptions => _shuffleOptions;

    public IReadOnlyList<WallpaperCategory> WallpaperCategories { get; } = Enum.GetValues<WallpaperCategory>();

    public string WallpaperDirectory
    {
        get => _wallpaperDirectory;
        set => SetField(ref _wallpaperDirectory, value);
    }

    public string DayStartText
    {
        get => _dayStartText;
        set => SetField(ref _dayStartText, value);
    }

    public string NightStartText
    {
        get => _nightStartText;
        set => SetField(ref _nightStartText, value);
    }

    public ShuffleOption SelectedShuffleOption
    {
        get => _selectedShuffleOption;
        set => SetField(ref _selectedShuffleOption, value);
    }

    public WallpaperItem? SelectedWallpaper
    {
        get => _selectedWallpaper;
        set => SetField(ref _selectedWallpaper, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string SettingsPath => _settingsStore.SettingsPath;

    public string SupportedFileSummary => "Supported files: .jpg, .jpeg, .png, .bmp, .heic, .heif, .webp, .tif, .tiff. Subfolders are scanned.";

    public string? WallpaperFolderBookmark => _wallpaperFolderBookmark;

    public bool StartAtLogin
    {
        get => _startAtLogin;
        private set => SetField(ref _startAtLogin, value);
    }

    public void Start()
    {
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    public void SetWallpaperFolder(string folderPath)
    {
        _wallpaperFolderBookmark = null;
        WallpaperDirectory = folderPath;
        RefreshWallpapers(BuildAssignmentMap());
        StatusMessage = "Folder updated. Review the Day/Night assignments, then save.";
    }

    public void SetWallpaperFolderFromStorage(
        string folderPath,
        string? bookmark,
        WallpaperLoadResult loadResult,
        string statusPrefix = "Folder updated.")
    {
        _wallpaperFolderBookmark = bookmark;
        WallpaperDirectory = folderPath;
        _loadedWallpaperDirectory = folderPath.Trim();
        ApplyLoadResult(loadResult, statusPrefix);
    }

    public IReadOnlyDictionary<string, WallpaperCategory> BuildAssignmentSnapshot()
    {
        return BuildAssignmentMap();
    }

    public void RefreshFolder()
    {
        RefreshWallpapers(BuildAssignmentMap());
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    public void Save()
    {
        SyncWallpaperListIfFolderChanged();

        if (!TryBuildSettings(out var settings))
        {
            return;
        }

        _settingsStore.Save(settings);
        StatusMessage = "Settings saved. Wallpaper schedule is active.";
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    public void ApplyNow()
    {
        SyncWallpaperListIfFolderChanged();
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    public void CycleNow()
    {
        SyncWallpaperListIfFolderChanged();

        if (!TryValidateRuntimeConfiguration(out var now, out var dayStart, out var nightStart))
        {
            _scheduler.Cancel();
            return;
        }

        EvaluateAndApplyWallpaper(now, dayStart, nightStart, forceApply: true, advanceToNextWallpaper: true);
        _scheduler.Schedule(now, dayStart, nightStart, SelectedShuffleOption.Value);
    }

    public void SwapDayNightHours()
    {
        (DayStartText, NightStartText) = (NightStartText, DayStartText);
        StatusMessage = $"Day now starts at {DayStartText}; night starts at {NightStartText}.";
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    public void SetShuffleCadence(ShuffleCadence cadence)
    {
        SelectedShuffleOption = _shuffleOptions.FirstOrDefault(option => option.Value == cadence)
            ?? _selectedShuffleOption;
        StatusMessage = $"Wallpaper rotation set to {SelectedShuffleOption.Label.ToLowerInvariant()}.";
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    public void SetStartAtLogin(bool enabled)
    {
        if (!LaunchAtLoginService.TrySetEnabled(enabled, out var errorMessage))
        {
            StatusMessage = errorMessage ?? "Unable to update launch-at-login.";
            StartAtLogin = LaunchAtLoginService.IsEnabled();
            return;
        }

        StartAtLogin = enabled;
        StatusMessage = enabled
            ? "Wallpaper Switcher will start when you sign in."
            : "Wallpaper Switcher will no longer start when you sign in.";
    }

    public void SetSelectedCategory(WallpaperCategory category)
    {
        if (SelectedWallpaper is null)
        {
            StatusMessage = "Select an image first.";
            return;
        }

        SelectedWallpaper.Category = category;
        var dayCount = WallpaperItems.Count(item => item.Category == WallpaperCategory.Day);
        var nightCount = WallpaperItems.Count(item => item.Category == WallpaperCategory.Night);
        StatusMessage = $"{SelectedWallpaper.FileName} marked as {category}. Day: {dayCount}, Night: {nightCount}.";
    }

    public void Dispose()
    {
        _scheduler.Dispose();
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load();

        WallpaperDirectory = settings.WallpaperDirectory;
        _wallpaperFolderBookmark = settings.WallpaperFolderBookmark;
        DayStartText = FormatTime(settings.DayStartsAt);
        NightStartText = FormatTime(settings.NightStartsAt);
        SelectedShuffleOption = _shuffleOptions.FirstOrDefault(option => option.Value == settings.ShuffleCadence)
            ?? _shuffleOptions.First(option => option.Value == ShuffleCadence.Daily);

        var savedAssignments = settings.Assignments.ToDictionary(
            assignment => assignment.Path,
            assignment => assignment.Category,
            StringComparer.OrdinalIgnoreCase);

        RefreshWallpapers(savedAssignments);

        if (File.Exists(SettingsPath))
        {
            StatusMessage = "Settings loaded. The app will keep watching your schedule.";
        }
    }

    private void SyncWallpaperListIfFolderChanged()
    {
        var currentFolder = WallpaperDirectory.Trim();
        if (string.Equals(currentFolder, _loadedWallpaperDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        RefreshWallpapers(null);
    }

    private Dictionary<string, WallpaperCategory> BuildAssignmentMap()
    {
        return WallpaperItems.ToDictionary(item => item.FullPath, item => item.Category, StringComparer.OrdinalIgnoreCase);
    }

    private void RefreshWallpapers(IReadOnlyDictionary<string, WallpaperCategory>? preferredAssignments)
    {
        WallpaperItems.Clear();
        SelectedWallpaper = null;

        if (string.IsNullOrWhiteSpace(WallpaperDirectory))
        {
            _loadedWallpaperDirectory = string.Empty;
            StatusMessage = "Choose a folder with wallpapers to get started.";
            return;
        }

        if (!Directory.Exists(WallpaperDirectory))
        {
            _loadedWallpaperDirectory = string.Empty;
            StatusMessage = "The selected wallpaper folder does not exist.";
            return;
        }

        _loadedWallpaperDirectory = WallpaperDirectory.Trim();
        var loadResult = WallpaperSelectionService.LoadWallpapersWithDiagnostics(WallpaperDirectory, preferredAssignments);

        ApplyLoadResult(loadResult);
    }

    private void ApplyLoadResult(WallpaperLoadResult loadResult, string? statusPrefix = null)
    {
        WallpaperItems.Clear();
        SelectedWallpaper = null;

        foreach (var wallpaper in loadResult.Wallpapers)
        {
            WallpaperItems.Add(wallpaper);
        }

        var dayCount = WallpaperItems.Count(item => item.Category == WallpaperCategory.Day);
        var nightCount = WallpaperItems.Count(item => item.Category == WallpaperCategory.Night);
        var summary = $"{WallpaperItems.Count} image(s) loaded. Day: {dayCount}, Night: {nightCount}.";
        if (!string.IsNullOrWhiteSpace(statusPrefix))
        {
            summary = $"{statusPrefix} {summary}";
        }

        StatusMessage = loadResult.WarningMessage is null
            ? summary
            : $"{summary} {loadResult.WarningMessage}";
    }

    private void ApplyWallpaperAndReschedule(bool forceApply = false)
    {
        if (!TryValidateRuntimeConfiguration(out var now, out var dayStart, out var nightStart))
        {
            _scheduler.Cancel();
            return;
        }

        EvaluateAndApplyWallpaper(now, dayStart, nightStart, forceApply, advanceToNextWallpaper: false);
        _scheduler.Schedule(now, dayStart, nightStart, SelectedShuffleOption.Value);
    }

    private void EvaluateAndApplyWallpaper(
        DateTime now,
        TimeSpan dayStart,
        TimeSpan nightStart,
        bool forceApply,
        bool advanceToNextWallpaper)
    {
        var targetCategory = WallpaperScheduleCalculator.GetCurrentCategory(now, dayStart, nightStart);

        var candidates = WallpaperItems
            .Where(item => item.Category == targetCategory && File.Exists(item.FullPath))
            .Select(item => item.FullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            StatusMessage = $"No {targetCategory} wallpapers are assigned.";
            return;
        }

        var cycleKey = WallpaperScheduleCalculator.BuildCycleKey(now, targetCategory, dayStart, SelectedShuffleOption.Value);
        var targetWallpaper = advanceToNextWallpaper
            ? PickNextWallpaper(candidates)
            : string.Equals(cycleKey, _lastAppliedCycleKey, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(_lastAppliedPath)
                && candidates.Contains(_lastAppliedPath, StringComparer.OrdinalIgnoreCase)
                    ? _lastAppliedPath
                    : WallpaperSelectionService.PickWallpaper(candidates, cycleKey, _lastAppliedPath, _lastAppliedCycleKey);

        if (!forceApply
            && string.Equals(targetWallpaper, _lastAppliedPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(cycleKey, _lastAppliedCycleKey, StringComparison.Ordinal))
        {
            return;
        }

        if (!_wallpaperService.TryApply(targetWallpaper, out var errorMessage))
        {
            StatusMessage = errorMessage ?? "Unable to change the wallpaper.";
            return;
        }

        _lastAppliedPath = targetWallpaper;
        _lastAppliedCycleKey = cycleKey;
        StatusMessage = $"{targetCategory} wallpaper active: {Path.GetFileName(targetWallpaper)}";
    }

    private string PickNextWallpaper(IReadOnlyList<string> candidates)
    {
        if (candidates.Count == 1 || string.IsNullOrWhiteSpace(_lastAppliedPath))
        {
            return candidates[0];
        }

        var currentIndex = -1;
        for (var index = 0; index < candidates.Count; index++)
        {
            if (string.Equals(candidates[index], _lastAppliedPath, StringComparison.OrdinalIgnoreCase))
            {
                currentIndex = index;
                break;
            }
        }

        return currentIndex < 0
            ? candidates[0]
            : candidates[(currentIndex + 1) % candidates.Count];
    }

    private bool TryValidateRuntimeConfiguration(out DateTime now, out TimeSpan dayStart, out TimeSpan nightStart)
    {
        now = DateTime.Now;

        if (!TryParseSchedule(out dayStart, out nightStart))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(WallpaperDirectory))
        {
            StatusMessage = "Choose a folder with wallpapers to get started.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(WallpaperDirectory) && !Directory.Exists(WallpaperDirectory))
        {
            StatusMessage = "Choose an existing wallpaper folder before running the schedule.";
            return false;
        }

        return true;
    }

    private bool TryBuildSettings(out AppSettings settings)
    {
        settings = new AppSettings();

        if (!TryParseSchedule(out var dayStart, out var nightStart))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(WallpaperDirectory) && !Directory.Exists(WallpaperDirectory))
        {
            StatusMessage = "Choose an existing wallpaper folder before saving.";
            return false;
        }

        if (WallpaperItems.Count == 0)
        {
            StatusMessage = "No supported wallpaper images were found in that folder or its subfolders.";
            return false;
        }

        settings.WallpaperDirectory = WallpaperDirectory.Trim();
        settings.WallpaperFolderBookmark = _wallpaperFolderBookmark;
        settings.DayStartsAt = dayStart;
        settings.NightStartsAt = nightStart;
        settings.ShuffleCadence = SelectedShuffleOption.Value;
        settings.Assignments = WallpaperItems
            .Select(item => new WallpaperAssignment
            {
                Path = item.FullPath,
                Category = item.Category
            })
            .ToList();

        return true;
    }

    private bool TryParseSchedule(out TimeSpan dayStart, out TimeSpan nightStart)
    {
        dayStart = default;
        nightStart = default;

        if (!TryParseTime(DayStartText, out dayStart))
        {
            StatusMessage = "Day start must look like 06:00 or 6:00 AM.";
            return false;
        }

        if (!TryParseTime(NightStartText, out nightStart))
        {
            StatusMessage = "Night start must look like 18:00 or 6:00 PM.";
            return false;
        }

        if (dayStart == nightStart)
        {
            StatusMessage = "Day start and night start cannot be the same time.";
            return false;
        }

        return true;
    }

    private static bool TryParseTime(string input, out TimeSpan value)
    {
        value = default;
        var trimmed = input.Trim();

        if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out value)
            || TimeSpan.TryParse(trimmed, CultureInfo.CurrentCulture, out value))
        {
            return value >= TimeSpan.Zero && value < TimeSpan.FromDays(1);
        }

        if (DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.NoCurrentDateDefault, out var dateTime))
        {
            value = dateTime.TimeOfDay;
            return true;
        }

        return false;
    }

    private static string FormatTime(TimeSpan value)
    {
        return value.ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName ?? string.Empty);
        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
