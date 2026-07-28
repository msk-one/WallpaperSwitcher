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
public sealed class ThumbnailCache : IDisposable
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

    /// <summary>
    /// Returns an already-decoded thumbnail without touching the disk.
    /// </summary>
    public bool TryGetCached(string path, out Bitmap? thumbnail)
    {
        lock (_gate)
        {
            if (_cache.TryGetValue(path, out thumbnail))
            {
                Touch(path);
                return true;
            }
        }

        thumbnail = null;
        return false;
    }

    /// <summary>
    /// Decodes off the UI thread, so opening the window does not stall while a
    /// screenful of images is read from disk.
    /// </summary>
    /// <remarks>
    /// Decoding used to happen synchronously inside the value converter. Every
    /// realized tile therefore blocked the UI thread for as long as one JPEG took
    /// to read and downscale, which is what made showing the window take seconds
    /// on a folder of large images.
    /// </remarks>
    public async Task<Bitmap?> GetAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (TryGetCached(path, out var cached))
        {
            return cached;
        }

        var thumbnail = await Task.Run(() => LoadThumbnail(path)).ConfigureAwait(true);

        lock (_gate)
        {
            // Another decode may have populated it while this one ran.
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
            // Deliberately broad. A decode failure must never propagate out of
            // the background task and reach the unhandled-exception handler, and Skia
            // does not document which exception types it raises for a format it
            // cannot decode (HEIC/HEIF and TIFF are unsupported) or for a
            // truncated file. A missing preview is not worth a crash.
            AppLog.Warn($"No preview for '{path}': {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }
}
