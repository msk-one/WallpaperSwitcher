using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace WallpaperSwitcher.Desktop.Services;

/// <summary>
/// Decodes preview thumbnails, keeping the most recently used ones.
/// </summary>
/// <remarks>
/// The cache is bounded because it used to grow without limit: a folder with a
/// few thousand images held every decoded bitmap for the lifetime of the process
/// and nothing ever disposed them. With the list virtualized only a screenful is
/// live at a time, so a small cap covers scrolling without the footprint.
/// </remarks>
public sealed class ThumbnailCache : IValueConverter, IDisposable
{
    public static ThumbnailCache Instance { get; } = new();

    private const int DecodeWidth = 144;
    private const int MaximumEntries = 300;

    private readonly object _gate = new();
    private readonly Dictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _recency = new();

    private ThumbnailCache()
    {
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        lock (_gate)
        {
            if (_cache.TryGetValue(path, out var cached))
            {
                Touch(path);
                return cached;
            }
        }

        var thumbnail = LoadThumbnail(path);

        lock (_gate)
        {
            // Another thread may have populated it while we decoded.
            if (_cache.TryGetValue(path, out var existing))
            {
                thumbnail?.Dispose();
                Touch(path);
                return existing;
            }

            _cache[path] = thumbnail;
            _recency.AddFirst(path);
            EvictWhileOverCapacity();
            return thumbnail;
        }
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            foreach (var thumbnail in _cache.Values)
            {
                thumbnail?.Dispose();
            }

            _cache.Clear();
            _recency.Clear();
        }
    }

    private void Touch(string path)
    {
        if (_recency.Remove(path))
        {
            _recency.AddFirst(path);
        }
    }

    private void EvictWhileOverCapacity()
    {
        while (_recency.Count > MaximumEntries && _recency.Last is { } oldest)
        {
            _recency.RemoveLast();

            if (_cache.Remove(oldest.Value, out var evicted))
            {
                evicted?.Dispose();
            }
        }
    }

    private static Bitmap? LoadThumbnail(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, DecodeWidth, BitmapInterpolationMode.LowQuality);
        }
        catch (Exception ex)
        {
            // Deliberately broad. This runs inside an IValueConverter on the UI
            // thread, so anything that escapes takes the whole app down, and Skia
            // does not document which exception types it raises for a format it
            // cannot decode (HEIC/HEIF and TIFF are unsupported) or for a
            // truncated file. A missing preview is not worth a crash.
            AppLog.Warn($"No preview for '{path}': {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
