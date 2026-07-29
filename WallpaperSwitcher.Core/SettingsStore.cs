using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WallpaperSwitcher;

public enum SettingsLoadStatus
{
    /// <summary>No settings file exists yet; defaults are in use.</summary>
    NotFound,

    /// <summary>The settings file was read successfully.</summary>
    Loaded,

    /// <summary>The settings file was unreadable and has been moved aside.</summary>
    Corrupt,

    /// <summary>The settings file exists but could not be opened.</summary>
    Unreadable
}

public sealed class SettingsStore
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,

        // Enum names rather than ordinals, so the file stays readable and stays
        // valid if a value is ever inserted into the middle of an enum. The
        // converter still reads the numbers written by earlier versions.
        Converters = { new JsonStringEnumConverter() }
    };

    public SettingsStore()
        : this(AppDataDirectory)
    {
    }

    /// <param name="appDataDirectory">
    /// Overrides the per-user location. Exists so tests can work against a scratch
    /// directory: <see cref="Environment.SpecialFolder"/> resolves through the
    /// Win32 known-folder API on Windows and cannot be redirected with an
    /// environment variable.
    /// </param>
    public SettingsStore(string appDataDirectory)
    {
        SettingsPath = Path.Combine(appDataDirectory, "settings.json");
    }

    /// <summary>
    /// Per-user data directory shared by the settings file and the log folder.
    /// </summary>
    public static string AppDataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WallpaperSwitcher");

    public string SettingsPath { get; }

    public AppSettings Load()
    {
        return Load(out _);
    }

    public AppSettings Load(out SettingsLoadStatus status)
    {
        if (!File.Exists(SettingsPath))
        {
            status = SettingsLoadStatus.NotFound;
            return new AppSettings();
        }

        string json;
        try
        {
            json = File.ReadAllText(SettingsPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            status = SettingsLoadStatus.Unreadable;
            return new AppSettings();
        }

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions);
            if (settings is not null)
            {
                status = SettingsLoadStatus.Loaded;
                return settings;
            }
        }
        catch (JsonException)
        {
            // Fall through to the quarantine path below.
        }

        // Never silently discard a file we could not parse. Overwriting it on the
        // next save would destroy the user's folder and every Day/Night assignment
        // with no way to recover them.
        QuarantineCorruptFile();
        status = SettingsLoadStatus.Corrupt;
        return new AppSettings();
    }

    public bool TrySave(AppSettings settings, out string? errorMessage)
    {
        errorMessage = null;

        var directory = Path.GetDirectoryName(SettingsPath);
        var temporaryPath = SettingsPath + ".tmp";

        try
        {
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Write-then-rename. A direct write truncates first, so an interrupted
            // save (crash, full disk, antivirus holding the handle) would leave a
            // half-written file that the next load has to quarantine.
            var json = JsonSerializer.Serialize(ToPersistedForm(settings), _jsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, SettingsPath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            errorMessage = $"Could not save settings: {ex.Message}";
            TryDelete(temporaryPath);
            return false;
        }
    }

    /// <summary>
    /// Rewrites assignment paths relative to the wallpaper folder and drops
    /// Ignore entries.
    /// </summary>
    /// <remarks>
    /// Absolute paths meant that renaming or moving the wallpaper folder made
    /// every stored assignment stop matching, so the user's Day/Night choices
    /// were silently replaced by filename guesses. Dropping Ignore entries also
    /// lets newly added images pick up name inference instead of being pinned to
    /// a default someone never chose.
    /// </remarks>
    private static AppSettings ToPersistedForm(AppSettings settings)
    {
        var baseDirectory = settings.WallpaperDirectory.Trim();

        return new AppSettings
        {
            SchemaVersion = AppSettings.CurrentSchemaVersion,
            WallpaperDirectory = settings.WallpaperDirectory,
            WallpaperFolderBookmark = settings.WallpaperFolderBookmark,
            DayStartsAt = settings.DayStartsAt,
            NightStartsAt = settings.NightStartsAt,
            ShuffleCadence = settings.ShuffleCadence,
            WallpaperFit = settings.WallpaperFit,
            StartMinimized = settings.StartMinimized,
            CloseAction = settings.CloseAction,
            Assignments = settings.Assignments
                .Where(assignment => assignment.Category != WallpaperCategory.Ignore)
                .Select(assignment => new WallpaperAssignment
                {
                    Path = WallpaperSelectionService.BuildAssignmentKey(baseDirectory, assignment.Path),
                    Category = assignment.Category
                })
                .ToList()
        };
    }

    private void QuarantineCorruptFile()
    {
        var stamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var quarantinePath = Path.Combine(
            Path.GetDirectoryName(SettingsPath) ?? AppDataDirectory,
            $"settings.corrupt-{stamp}.json");

        try
        {
            File.Move(SettingsPath, quarantinePath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Best effort. A failure here only means the bad file stays put.
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing useful to do about a leftover temp file.
        }
    }
}
