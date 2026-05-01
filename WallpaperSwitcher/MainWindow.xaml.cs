using System.Buffers.Binary;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace WallpaperSwitcher;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly string[] SupportedExtensions = [".jpg", ".jpeg", ".png", ".bmp"];

    private readonly SettingsStore _settingsStore = new();
    private readonly WallpaperScheduler _scheduler;
    private readonly WallpaperService _wallpaperService = new();
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
    private string _statusMessage = "Choose a folder with wallpapers to get started.";
    private string? _lastAppliedPath;
    private string? _lastAppliedCycleKey;
    private string _loadedWallpaperDirectory = string.Empty;
    private bool _allowClose;
    private bool _trayHintShown;
    private System.Windows.Forms.NotifyIcon? _notifyIcon;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        _scheduler = new WallpaperScheduler(Dispatcher, () => ApplyWallpaperAndReschedule(forceApply: true));

        _selectedShuffleOption = _shuffleOptions.First(option => option.Value == ShuffleCadence.Daily);
        LoadSettings();

        Loaded += MainWindow_Loaded;
        StateChanged += MainWindow_StateChanged;
        Closing += MainWindow_Closing;
        SystemEvents.PowerModeChanged += SystemEvents_PowerModeChanged;
        SystemEvents.TimeChanged += SystemEvents_TimeChanged;
    }

    public ObservableCollection<WallpaperItem> WallpaperItems { get; } = [];

    public IReadOnlyList<ShuffleOption> ShuffleOptions => _shuffleOptions;

    public string WallpaperDirectory
    {
        get => _wallpaperDirectory;
        set
        {
            if (_wallpaperDirectory == value)
            {
                return;
            }

            _wallpaperDirectory = value;
            OnPropertyChanged(nameof(WallpaperDirectory));
        }
    }

    public string DayStartText
    {
        get => _dayStartText;
        set
        {
            if (_dayStartText == value)
            {
                return;
            }

            _dayStartText = value;
            OnPropertyChanged(nameof(DayStartText));
        }
    }

    public string NightStartText
    {
        get => _nightStartText;
        set
        {
            if (_nightStartText == value)
            {
                return;
            }

            _nightStartText = value;
            OnPropertyChanged(nameof(NightStartText));
        }
    }

    public ShuffleOption SelectedShuffleOption
    {
        get => _selectedShuffleOption;
        set
        {
            if (_selectedShuffleOption == value)
            {
                return;
            }

            _selectedShuffleOption = value;
            OnPropertyChanged(nameof(SelectedShuffleOption));
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    public string SettingsPath => _settingsStore.SettingsPath;

    private void MainWindow_Loaded(object? sender, RoutedEventArgs e)
    {
        EnsureTrayIcon();
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    private void SystemEvents_PowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume)
        {
            return;
        }

        Dispatcher.InvokeAsync(() => ApplyWallpaperAndReschedule(forceApply: true));
    }

    private void SystemEvents_TimeChanged(object? sender, EventArgs e)
    {
        Dispatcher.InvokeAsync(() => ApplyWallpaperAndReschedule(forceApply: true));
    }

    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState != WindowState.Minimized)
        {
            return;
        }

        Hide();
        WindowState = WindowState.Normal;

        if (_notifyIcon is not null && !_trayHintShown)
        {
            _notifyIcon.ShowBalloonTip(
                3000,
                "Wallpaper Switcher",
                "The app is still running in the tray and will keep your wallpaper schedule active.",
                System.Windows.Forms.ToolTipIcon.Info);
            _trayHintShown = true;
        }
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
            SystemEvents.TimeChanged -= SystemEvents_TimeChanged;
            _scheduler.Dispose();
            _notifyIcon?.Dispose();
            _notifyIcon = null;
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select the folder that contains your wallpaper images.",
            ShowNewFolderButton = false,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        WallpaperDirectory = dialog.SelectedPath;
        RefreshWallpapers(BuildAssignmentMap());
        StatusMessage = "Folder updated. Review the Day/Night assignments, then save.";
    }

    private void RefreshFolder_Click(object sender, RoutedEventArgs e)
    {
        RefreshWallpapers(BuildAssignmentMap());
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        SyncWallpaperListIfFolderChanged();

        if (!TryBuildSettings(out var settings))
        {
            System.Windows.MessageBox.Show(
                this,
                StatusMessage,
                "Wallpaper Switcher",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        _settingsStore.Save(settings);
        StatusMessage = "Settings saved. Wallpaper schedule is active.";
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    private void ApplyNow_Click(object sender, RoutedEventArgs e)
    {
        SyncWallpaperListIfFolderChanged();
        ApplyWallpaperAndReschedule(forceApply: true);
    }

    private void EnsureTrayIcon()
    {
        if (_notifyIcon is not null)
        {
            return;
        }

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Wallpaper Switcher",
            Visible = true,
            Icon = System.Drawing.SystemIcons.Application
        };

        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();

        var openItem = new System.Windows.Forms.ToolStripMenuItem("Open");
        openItem.Click += (_, _) => ShowMainWindow();

        var applyItem = new System.Windows.Forms.ToolStripMenuItem("Apply now");
        applyItem.Click += (_, _) => Dispatcher.Invoke(() => ApplyWallpaperAndReschedule(forceApply: true));

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApplication();

        _notifyIcon.ContextMenuStrip = new System.Windows.Forms.ContextMenuStrip();
        _notifyIcon.ContextMenuStrip.Items.Add(openItem);
        _notifyIcon.ContextMenuStrip.Items.Add(applyItem);
        _notifyIcon.ContextMenuStrip.Items.Add(exitItem);
    }

    private void ShowMainWindow()
    {
        Show();
        Activate();
        WindowState = WindowState.Normal;
    }

    private void ExitApplication()
    {
        _allowClose = true;
        SystemEvents.PowerModeChanged -= SystemEvents_PowerModeChanged;
        SystemEvents.TimeChanged -= SystemEvents_TimeChanged;
        _scheduler.Dispose();
        _notifyIcon?.Dispose();
        _notifyIcon = null;
        Close();
        System.Windows.Application.Current.Shutdown();
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load();

        WallpaperDirectory = settings.WallpaperDirectory;
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
            StatusMessage = "Settings loaded. The tray app will keep watching your schedule.";
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

        var files = Directory
            .EnumerateFiles(WallpaperDirectory)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var file in files)
        {
            var category = WallpaperCategory.Ignore;
            if (preferredAssignments is not null && preferredAssignments.TryGetValue(file, out var savedCategory))
            {
                category = savedCategory;
            }
            else
            {
                category = InferCategoryFromName(Path.GetFileNameWithoutExtension(file));
            }

            WallpaperItems.Add(new WallpaperItem(Path.GetFileName(file), file, category));
        }

        var dayCount = WallpaperItems.Count(item => item.Category == WallpaperCategory.Day);
        var nightCount = WallpaperItems.Count(item => item.Category == WallpaperCategory.Night);
        StatusMessage = $"{files.Count} image(s) loaded. Day: {dayCount}, Night: {nightCount}.";
    }

    private void ApplyWallpaperAndReschedule(bool forceApply = false)
    {
        if (!TryValidateRuntimeConfiguration(out var now, out var dayStart, out var nightStart))
        {
            _scheduler.Cancel();
            return;
        }

        EvaluateAndApplyWallpaper(now, dayStart, nightStart, forceApply);
        _scheduler.Schedule(now, dayStart, nightStart, SelectedShuffleOption.Value);
    }

    private void EvaluateAndApplyWallpaper(DateTime now, TimeSpan dayStart, TimeSpan nightStart, bool forceApply = false)
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
        var targetWallpaper = string.Equals(cycleKey, _lastAppliedCycleKey, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(_lastAppliedPath)
            && candidates.Contains(_lastAppliedPath, StringComparer.OrdinalIgnoreCase)
                ? _lastAppliedPath
                : PickWallpaper(candidates, cycleKey);

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

    private bool TryValidateRuntimeConfiguration(out DateTime now, out TimeSpan dayStart, out TimeSpan nightStart)
    {
        now = DateTime.Now;

        if (!TryParseSchedule(out dayStart, out nightStart))
        {
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

        settings.WallpaperDirectory = WallpaperDirectory.Trim();
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

    private static WallpaperCategory InferCategoryFromName(string name)
    {
        if (name.Contains("night", StringComparison.OrdinalIgnoreCase))
        {
            return WallpaperCategory.Night;
        }

        if (name.Contains("day", StringComparison.OrdinalIgnoreCase))
        {
            return WallpaperCategory.Day;
        }

        return WallpaperCategory.Ignore;
    }

    private string PickWallpaper(IReadOnlyList<string> candidates, string cycleKey)
    {
        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(cycleKey));
        var rawIndex = BinaryPrimitives.ReadUInt32LittleEndian(hashBytes);
        var index = (int)(rawIndex % (uint)candidates.Count);

        if (!string.IsNullOrWhiteSpace(_lastAppliedPath)
            && !string.Equals(cycleKey, _lastAppliedCycleKey, StringComparison.Ordinal)
            && string.Equals(candidates[index], _lastAppliedPath, StringComparison.OrdinalIgnoreCase))
        {
            index = (index + 1) % candidates.Count;
        }

        return candidates[index];
    }

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
