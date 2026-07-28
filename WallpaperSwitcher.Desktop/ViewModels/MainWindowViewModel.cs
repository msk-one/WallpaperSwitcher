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
    private readonly WindowsSystemEventsBridge? _systemEvents;
    private readonly IReadOnlyList<ShuffleOption> _shuffleOptions =
    [
        new(ShuffleCadence.Hourly, "Every hour"),
        new(ShuffleCadence.SixHours, "Every 6 hours"),
        new(ShuffleCadence.Daily, "Each day"),
        new(ShuffleCadence.Weekly, "Each week")
    ];

    private readonly IReadOnlyList<WallpaperFitOption> _fitOptions =
    [
        new(WallpaperFit.Fill, "Fill"),
        new(WallpaperFit.Fit, "Fit"),
        new(WallpaperFit.Stretch, "Stretch"),
        new(WallpaperFit.Center, "Center"),
        new(WallpaperFit.Tile, "Tile"),
        new(WallpaperFit.Span, "Span")
    ];

    private readonly IReadOnlyList<WindowCloseActionOption> _closeActionOptions =
    [
        new(WindowCloseAction.Ask, "Ask every time"),
        new(WindowCloseAction.MinimizeToTray, "Keep running in the tray"),
        new(WindowCloseAction.Quit, "Quit the app")
    ];

    /// <summary>
    /// Files that failed to apply during this run. The cycle key is deterministic,
    /// so without this the watchdog would recompute the same unusable image every
    /// minute and never change the wallpaper for the rest of the cycle.
    /// </summary>
    private readonly HashSet<string> _failedWallpapers = new(StringComparer.OrdinalIgnoreCase);

    private string _wallpaperDirectory = string.Empty;
    private TimeSpan _dayStart = TimeSpan.FromHours(6);
    private TimeSpan _nightStart = TimeSpan.FromHours(18);
    private ShuffleOption _selectedShuffleOption;
    private HeroState _heroState = HeroState.NoFolder;
    private string _heroTitle = string.Empty;
    private string _heroSubtitle = string.Empty;
    private string? _heroThumbnailPath;
    private bool _highlightSource;
    private int _selectedPageIndex;
    private bool _isLoadingSettings;
    private WallpaperFitOption _selectedFitOption;
    private WindowCloseActionOption _selectedCloseActionOption;
    private string _statusMessage = "Choose a folder with wallpapers to get started.";
    private string? _lastAppliedPath;
    private string? _lastAppliedCycleKey;
    private string _loadedWallpaperDirectory = string.Empty;
    private string? _wallpaperFolderBookmark;
    private bool _startAtLogin;
    private bool _startMinimized;
    private bool _applyInFlight;
    private bool _applyRequestedWhileBusy;

    public MainWindowViewModel(SettingsStore settingsStore, IWallpaperService wallpaperService)
    {
        _settingsStore = settingsStore;
        _wallpaperService = wallpaperService;
        _scheduler = new WallpaperScheduler(
            () => ApplyWallpaperAndReschedule(forceApply: true),
            () => ApplyWallpaperAndReschedule(forceApply: false));

        _selectedShuffleOption = _shuffleOptions.First(option => option.Value == ShuffleCadence.Daily);
        _selectedFitOption = _fitOptions.First(option => option.Value == WallpaperFit.Fill);
        _selectedCloseActionOption = _closeActionOptions.First(option => option.Value == WindowCloseAction.Ask);
        _startAtLogin = LaunchAtLoginService.IsEnabled();
        LoadSettings();

        _systemEvents = WindowsSystemEventsBridge.CreateIfSupported(
            () => Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyWallpaperAndReschedule(forceApply: true)));
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

    /// <summary>
    /// Bound to the schedule bar. The bar snaps to 15 minutes and enforces an
    /// hour between the two, so the old "must look like 06:00" and "cannot be the
    /// same time" errors are no longer reachable and have been removed.
    /// </summary>
    public TimeSpan DayStart
    {
        get => _dayStart;
        set
        {
            if (SetField(ref _dayStart, value))
            {
                OnScheduleEdited();
            }
        }
    }

    public TimeSpan NightStart
    {
        get => _nightStart;
        set
        {
            if (SetField(ref _nightStart, value))
            {
                OnScheduleEdited();
            }
        }
    }

    public ShuffleOption SelectedShuffleOption
    {
        get => _selectedShuffleOption;
        set => SetField(ref _selectedShuffleOption, value);
    }

    public IReadOnlyList<WallpaperFitOption> FitOptions => _fitOptions;

    public WallpaperFitOption SelectedFitOption
    {
        get => _selectedFitOption;
        set => SetField(ref _selectedFitOption, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public string SettingsPath => _settingsStore.SettingsPath;

    /// <summary>
    /// Shown in the status bar so a bug report can quote it without the reporter
    /// having to find a log file.
    /// </summary>
    public static string AppVersion { get; } = ResolveAppVersion();

    public string VersionAndSettingsPath => $"v{AppVersion}  ·  {SettingsPath}";

    private static string ResolveAppVersion()
    {
        var informational = typeof(MainWindowViewModel).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .OfType<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return "unknown";
        }

        // Strip the "+<commit sha>" suffix; the full string is in the log.
        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }

    // Split deliberately: the first group is verified to preview and apply. The
    // second is accepted but depends on image codecs that are not present on
    // every machine, so promising it outright would be misleading.
    public string SupportedFileSummary =>
        "Supported files: .jpg, .jpeg, .png, .bmp, .gif, .tif, .tiff. "
        + ".heic, .heif and .webp also work when Windows has the matching codec installed, "
        + "and are skipped with a note in the log when it does not. Subfolders are scanned.";

    public string? WallpaperFolderBookmark => _wallpaperFolderBookmark;

    public bool StartAtLogin
    {
        get => _startAtLogin;
        private set => SetField(ref _startAtLogin, value);
    }

    public bool StartMinimized
    {
        get => _startMinimized;
        private set => SetField(ref _startMinimized, value);
    }

    public void SetStartMinimized(bool enabled)
    {
        StartMinimized = enabled;
        StatusMessage = enabled
            ? "Starts in the tray."
            : "Opens the window on start.";
        Save();
    }

    public IReadOnlyList<WindowCloseActionOption> CloseActionOptions => _closeActionOptions;

    /// <summary>
    /// What the window's close button does. Read by MainWindow on close.
    /// </summary>
    public WindowCloseAction CloseAction => _selectedCloseActionOption.Value;

    public WindowCloseActionOption SelectedCloseActionOption
    {
        get => _selectedCloseActionOption;
        set => SetField(ref _selectedCloseActionOption, value);
    }

    /// <summary>
    /// Records the answer to the close prompt, or a change made on the Settings
    /// page. Persists without touching the wallpaper.
    /// </summary>
    public void SetCloseAction(WindowCloseAction action)
    {
        SelectedCloseActionOption = _closeActionOptions.FirstOrDefault(option => option.Value == action)
            ?? _selectedCloseActionOption;

        StatusMessage = action switch
        {
            WindowCloseAction.MinimizeToTray => "Closing the window will keep the app running.",
            WindowCloseAction.Quit => "Closing the window will quit the app.",
            _ => "Closing the window will ask what to do."
        };

        PersistSettings();
    }

    // ---- Shell state -------------------------------------------------------

    /// <summary>Which of the two pages the nav is on. 0 = Wallpapers, 1 = Settings.</summary>
    public int SelectedPageIndex
    {
        get => _selectedPageIndex;
        set => SetField(ref _selectedPageIndex, value);
    }

    /// <summary>
    /// Set for a moment after the hero or the empty state sends the user to
    /// Settings, so the Source card is ringed and the jump is legible without a
    /// dialog.
    /// </summary>
    public bool HighlightSource
    {
        get => _highlightSource;
        private set => SetField(ref _highlightSource, value);
    }

    public HeroState HeroState
    {
        get => _heroState;
        private set => SetField(ref _heroState, value);
    }

    public string HeroTitle
    {
        get => _heroTitle;
        private set => SetField(ref _heroTitle, value);
    }

    public string HeroSubtitle
    {
        get => _heroSubtitle;
        private set => SetField(ref _heroSubtitle, value);
    }

    public string? HeroThumbnailPath
    {
        get => _heroThumbnailPath;
        private set => SetField(ref _heroThumbnailPath, value);
    }

    public int DayCount => WallpaperItems.Count(item => item.Category == WallpaperCategory.Day);

    public int NightCount => WallpaperItems.Count(item => item.Category == WallpaperCategory.Night);

    public int IgnoredCount => WallpaperItems.Count(item => item.Category == WallpaperCategory.Ignore);

    public bool HasImages => WallpaperItems.Count > 0;

    public string CountsSummary => HasImages
        ? $"{DayCount} day · {NightCount} night · {IgnoredCount} ignored"
        : "none yet";

    public string FolderSummary
    {
        get
        {
            if (!HasFolder)
            {
                return "Subfolders are scanned. jpg, png, bmp, gif, tif.";
            }

            var folders = CountScannedSubfolders();
            var subfolders = folders == 1 ? "1 subfolder" : $"{folders} subfolders";
            return $"{WallpaperItems.Count} images · {subfolders} scanned · jpg, png, bmp, gif, tif";
        }
    }

    public bool HasFolder => !string.IsNullOrWhiteSpace(WallpaperDirectory);

    public string FolderDisplayPath => HasFolder ? WallpaperDirectory : "No folder selected";

    /// <summary>Sends the user to Settings and rings the Source card.</summary>
    public void NavigateToSource()
    {
        SelectedPageIndex = 1;
        HighlightSource = true;
    }

    public void ClearSourceHighlight()
    {
        HighlightSource = false;
    }

    /// <summary>
    /// Advances one image between Ignore, Day and Night. Bound to the tile
    /// button's command, so it is reachable by keyboard and screen reader.
    /// </summary>
    public void CycleCategory(WallpaperItem item)
    {
        item.Category = item.Category switch
        {
            WallpaperCategory.Ignore => WallpaperCategory.Day,
            WallpaperCategory.Day => WallpaperCategory.Night,
            _ => WallpaperCategory.Ignore
        };

        RaiseCountsChanged();
        StatusMessage = $"{item.FileName} is now {Describe(item.Category)}.";
        Save();
    }

    private static string Describe(WallpaperCategory category) => category switch
    {
        WallpaperCategory.Day => "a day wallpaper",
        WallpaperCategory.Night => "a night wallpaper",
        _ => "ignored"
    };

    private void OnScheduleEdited()
    {
        if (_isLoadingSettings)
        {
            return;
        }

        StatusMessage = $"Day starts {Format(DayStart)}, night starts {Format(NightStart)}.";
        Save();
    }

    private void RaiseCountsChanged()
    {
        OnPropertyChanged(nameof(DayCount));
        OnPropertyChanged(nameof(NightCount));
        OnPropertyChanged(nameof(IgnoredCount));
        OnPropertyChanged(nameof(CountsSummary));
        OnPropertyChanged(nameof(HasImages));
        OnPropertyChanged(nameof(FolderSummary));
        OnPropertyChanged(nameof(HasFolder));
        OnPropertyChanged(nameof(FolderDisplayPath));
    }

    private int CountScannedSubfolders()
    {
        var root = WallpaperDirectory.Trim();
        if (string.IsNullOrWhiteSpace(root))
        {
            return 0;
        }

        return WallpaperItems
            .Select(item => Path.GetDirectoryName(item.FullPath))
            .Where(directory => !string.IsNullOrEmpty(directory)
                && !string.Equals(directory, root, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
    }

    private static string Format(TimeSpan value) => $"{(int)value.TotalHours:00}:{value.Minutes:00}";

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
        if (!PersistSettings())
        {
            return;
        }

        // Autosave has no Save button to acknowledge it, so the status bar is the
        // only confirmation. A change that does move the wallpaper overwrites this
        // with the more specific "... wallpaper active" line.
        StatusMessage = "Settings saved. Wallpaper schedule is active.";

        // Not a forced apply. Autosave runs on every toggle, every schedule drag
        // and every cadence change, and forcing here meant each of those paid for
        // a full SystemParametersInfo broadcast even when the correct wallpaper
        // was already on screen. The schedule is still rearmed, and the wallpaper
        // is reapplied only when the target actually changed.
        ApplyWallpaperAndReschedule(forceApply: false);
    }

    /// <summary>
    /// Writes settings to disk without touching the wallpaper, so callers choose
    /// whether the change warrants reapplying.
    /// </summary>
    private bool PersistSettings()
    {
        SyncWallpaperListIfFolderChanged();

        if (!TryBuildSettings(out var settings))
        {
            return false;
        }

        if (!_settingsStore.TrySave(settings, out var saveError))
        {
            StatusMessage = saveError ?? "Could not save settings.";
            AppLog.Error($"Saving settings failed: {saveError}");
            return false;
        }

        return true;
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
        _isLoadingSettings = true;
        (DayStart, NightStart) = (NightStart, DayStart);
        _isLoadingSettings = false;

        StatusMessage = $"Day starts {Format(DayStart)}, night starts {Format(NightStart)}.";
        Save();
    }

    public void SetShuffleCadence(ShuffleCadence cadence)
    {
        SelectedShuffleOption = _shuffleOptions.FirstOrDefault(option => option.Value == cadence)
            ?? _selectedShuffleOption;

        // Cadence says how often the wallpaper changes, not that it should change
        // now. Without this the new cadence produces a new cycle key, the key no
        // longer matches the one the current image was picked under, and simply
        // choosing "every week" swapped the wallpaper on the spot.
        RebaseCycleKeyToCurrentWallpaper();

        StatusMessage = $"Shuffles {SelectedShuffleOption.Label.ToLowerInvariant()}.";
        Save();
    }

    /// <summary>
    /// Re-anchors the current image to the cycle it would belong to under the
    /// settings now in force, so a settings change does not read as a new cycle.
    /// </summary>
    private void RebaseCycleKeyToCurrentWallpaper()
    {
        if (string.IsNullOrWhiteSpace(_lastAppliedPath))
        {
            return;
        }

        var now = DateTime.Now;
        var category = WallpaperScheduleCalculator.GetCurrentCategory(now, DayStart, NightStart);
        _lastAppliedCycleKey = WallpaperScheduleCalculator.BuildCycleKey(
            now,
            category,
            DayStart,
            SelectedShuffleOption.Value);
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
        StatusMessage = enabled ? "Starts when you sign in." : "No longer starts when you sign in.";
    }

    public void SetWallpaperFit(WallpaperFit fit)
    {
        SelectedFitOption = _fitOptions.FirstOrDefault(option => option.Value == fit) ?? _selectedFitOption;
        StatusMessage = $"Fit set to {SelectedFitOption.Label.ToLowerInvariant()}.";

        // Fit is written to the registry as part of applying, so unlike every
        // other setting it has no visible effect until the wallpaper is set
        // again. Hence a forced apply where Save would use an unforced one.
        PersistSettings();
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    public void Dispose()
    {
        _systemEvents?.Dispose();
        _scheduler.Dispose();
    }

    private void LoadSettings()
    {
        // Suppress the autosave that every schedule edit would otherwise trigger
        // while we are populating from disk.
        _isLoadingSettings = true;

        var settings = _settingsStore.Load(out var loadStatus);

        WallpaperDirectory = settings.WallpaperDirectory;
        _wallpaperFolderBookmark = settings.WallpaperFolderBookmark;
        DayStart = settings.DayStartsAt;
        NightStart = settings.NightStartsAt;
        StartMinimized = settings.StartMinimized;
        SelectedCloseActionOption = _closeActionOptions.FirstOrDefault(option => option.Value == settings.CloseAction)
            ?? _closeActionOptions.First(option => option.Value == WindowCloseAction.Ask);
        SelectedShuffleOption = _shuffleOptions.FirstOrDefault(option => option.Value == settings.ShuffleCadence)
            ?? _shuffleOptions.First(option => option.Value == ShuffleCadence.Daily);
        SelectedFitOption = _fitOptions.FirstOrDefault(option => option.Value == settings.WallpaperFit)
            ?? _fitOptions.First(option => option.Value == WallpaperFit.Fill);

        var savedAssignments = settings.Assignments.ToDictionary(
            assignment => assignment.Path,
            assignment => assignment.Category,
            StringComparer.OrdinalIgnoreCase);

        RefreshWallpapers(savedAssignments);

        switch (loadStatus)
        {
            case SettingsLoadStatus.Loaded:
                StatusMessage = "Settings loaded. The app will keep watching your schedule.";
                break;
            case SettingsLoadStatus.Corrupt:
                StatusMessage = "Your settings file was unreadable and has been set aside. Choose your folder again to start over.";
                AppLog.Warn("Settings file was corrupt and has been quarantined.");
                break;
            case SettingsLoadStatus.Unreadable:
                StatusMessage = "The settings file could not be opened. Starting with defaults; your saved settings were left untouched.";
                AppLog.Warn("Settings file exists but could not be read.");
                break;
        }

        _isLoadingSettings = false;
        RaiseCountsChanged();
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
        // Keyed relative to the wallpaper folder so assignments still match after
        // the folder is renamed or moved.
        var baseDirectory = WallpaperDirectory.Trim();

        return WallpaperItems.ToDictionary(
            item => WallpaperSelectionService.BuildAssignmentKey(baseDirectory, item.FullPath),
            item => item.Category,
            StringComparer.OrdinalIgnoreCase);
    }

    private void RefreshWallpapers(IReadOnlyDictionary<string, WallpaperCategory>? preferredAssignments)
    {
        WallpaperItems.Clear();
        _failedWallpapers.Clear();

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
        _failedWallpapers.Clear();

        foreach (var wallpaper in loadResult.Wallpapers)
        {
            WallpaperItems.Add(wallpaper);
        }

        RaiseCountsChanged();

        var summary = $"{WallpaperItems.Count} images loaded · {DayCount} day · {NightCount} night.";
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
            .Where(item => item.Category == targetCategory
                && File.Exists(item.FullPath)
                && !_failedWallpapers.Contains(item.FullPath))
            .Select(item => item.FullPath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (candidates.Count == 0)
        {
            StatusMessage = _failedWallpapers.Count == 0
                ? $"No {targetCategory} wallpapers are assigned."
                : $"None of the assigned {targetCategory} wallpapers could be applied. Check the log for details.";
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

        var startIndex = Math.Max(0, candidates.IndexOf(targetWallpaper));
        BeginApply(candidates, startIndex, cycleKey, targetCategory, now, dayStart, nightStart);
    }

    /// <summary>
    /// Runs the actual wallpaper change on a background thread and applies the
    /// result back on the UI thread.
    /// </summary>
    /// <remarks>
    /// On Windows the apply ends in SystemParametersInfo with SPIF_SENDWININICHANGE,
    /// which broadcasts WM_SETTINGCHANGE to every top-level window and blocks
    /// until they answer or time out. Doing that inline froze the whole app for
    /// seconds at a time — the tray menu would not open, the window would not
    /// paint, and startup stalled before the first frame.
    /// </remarks>
    private async void BeginApply(
        IReadOnlyList<string> candidates,
        int startIndex,
        string cycleKey,
        WallpaperCategory targetCategory,
        DateTime now,
        TimeSpan dayStart,
        TimeSpan nightStart)
    {
        if (_applyInFlight)
        {
            // Coalesce: the watchdog, a settings change and a manual cycle can all
            // land at once. Re-evaluating once at the end reaches the same result
            // as queueing each of them, without a backlog of broadcasts.
            _applyRequestedWhileBusy = true;
            return;
        }

        _applyInFlight = true;
        var fit = SelectedFitOption.Value;

        try
        {
            var outcome = await Task.Run(() => ApplyFirstUsable(candidates, startIndex, fit));

            foreach (var failure in outcome.Failed)
            {
                _failedWallpapers.Add(failure.Path);
                AppLog.Warn($"Could not apply '{failure.Path}': {failure.Error}");
            }

            if (outcome.Applied is { } applied)
            {
                _lastAppliedPath = applied;
                _lastAppliedCycleKey = cycleKey;
                StatusMessage = $"{targetCategory} wallpaper active: {Path.GetFileName(applied)}";
                UpdateHero(now, targetCategory, applied, dayStart, nightStart);
            }
            else
            {
                StatusMessage = outcome.LastError ?? "Unable to change the wallpaper.";
            }
        }
        catch (Exception ex)
        {
            // This is an async void continuation: anything that escapes would go
            // to the unhandled-exception handler and take the app down.
            AppLog.Error($"Applying the wallpaper failed: {ex}");
            StatusMessage = "Unable to change the wallpaper. See the log for details.";
        }
        finally
        {
            _applyInFlight = false;

            if (_applyRequestedWhileBusy)
            {
                _applyRequestedWhileBusy = false;
                ApplyWallpaperAndReschedule(forceApply: false);
            }
        }
    }

    /// <summary>
    /// Walks forward through the candidates until one applies. Runs off the UI
    /// thread, so it touches no view-model state.
    /// </summary>
    /// <remarks>
    /// An image the OS cannot decode must not stall the whole cycle: with a
    /// deterministic cycle key, giving up on the first failure would make the
    /// watchdog retry the same file every minute until the next boundary.
    /// </remarks>
    private ApplyOutcome ApplyFirstUsable(IReadOnlyList<string> candidates, int startIndex, WallpaperFit fit)
    {
        var failed = new List<(string Path, string? Error)>();
        string? lastError = null;

        for (var attempt = 0; attempt < candidates.Count; attempt++)
        {
            var candidate = candidates[(startIndex + attempt) % candidates.Count];

            if (_wallpaperService.TryApply(candidate, fit, out var errorMessage))
            {
                return new ApplyOutcome(candidate, null, failed);
            }

            lastError = errorMessage;
            failed.Add((candidate, errorMessage));
        }

        return new ApplyOutcome(null, lastError, failed);
    }

    private sealed record ApplyOutcome(
        string? Applied,
        string? LastError,
        IReadOnlyList<(string Path, string? Error)> Failed);

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
        dayStart = DayStart;
        nightStart = NightStart;

        if (string.IsNullOrWhiteSpace(WallpaperDirectory))
        {
            SetHero(HeroState.NoFolder, string.Empty, string.Empty);
            StatusMessage = "Waiting for a folder.";
            return false;
        }

        if (!Directory.Exists(WallpaperDirectory))
        {
            SetHero(
                HeroState.FolderMissing,
                "Wallpaper unchanged",
                $"{WallpaperDirectory} is unavailable. Choose a folder again in Settings.");
            StatusMessage = "Wallpaper unchanged — the source folder is missing.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Fills the hero strip from the wallpaper that was just applied.
    /// </summary>
    private void UpdateHero(DateTime now, WallpaperCategory category, string appliedPath, TimeSpan dayStart, TimeSpan nightStart)
    {
        var isNight = category == WallpaperCategory.Night;
        var nextChange = isNight ? dayStart : nightStart;
        var inSet = isNight ? NightCount : DayCount;

        HeroThumbnailPath = appliedPath;
        SetHero(
            HeroState.Running,
            Path.GetFileName(appliedPath),
            $"{(isNight ? "Night" : "Day")} set · {inSet} images · next change {Format(nextChange)}");
    }

    private void SetHero(HeroState state, string title, string subtitle)
    {
        HeroState = state;
        HeroTitle = title;
        HeroSubtitle = subtitle;

        if (state != HeroState.Running)
        {
            HeroThumbnailPath = null;
        }
    }

    private bool TryBuildSettings(out AppSettings settings)
    {
        settings = new AppSettings();

        if (!string.IsNullOrWhiteSpace(WallpaperDirectory) && !Directory.Exists(WallpaperDirectory))
        {
            StatusMessage = "That folder is no longer available.";
            return false;
        }

        settings.WallpaperDirectory = WallpaperDirectory.Trim();
        settings.WallpaperFolderBookmark = _wallpaperFolderBookmark;
        settings.DayStartsAt = DayStart;
        settings.NightStartsAt = NightStart;
        settings.ShuffleCadence = SelectedShuffleOption.Value;
        settings.WallpaperFit = SelectedFitOption.Value;
        settings.StartMinimized = StartMinimized;
        settings.CloseAction = SelectedCloseActionOption.Value;

        // An empty image list must not block the save. The tray's cadence radios
        // and "Swap day/night hours" both call Save(), so refusing here made those
        // silently revert on restart for anyone who had not picked a folder yet.
        // It must also not overwrite a good assignment list with an empty one.
        if (WallpaperItems.Count > 0)
        {
            settings.Assignments = WallpaperItems
                .Select(item => new WallpaperAssignment
                {
                    Path = item.FullPath,
                    Category = item.Category
                })
                .ToList();
        }
        else
        {
            settings.Assignments = _settingsStore.Load().Assignments;
            StatusMessage = "No supported wallpaper images were found in that folder or its subfolders.";
        }

        return true;
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
