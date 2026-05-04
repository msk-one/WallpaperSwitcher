using System.Collections.Concurrent;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace WallpaperSwitcher.Desktop.Services;

public sealed class ThumbnailCache : IValueConverter, IDisposable
{
    public static ThumbnailCache Instance { get; } = new();

    private const int DecodeWidth = 144;
    private readonly ConcurrentDictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);

    private ThumbnailCache()
    {
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return _cache.GetOrAdd(path, LoadThumbnail);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return null;
    }

    public void Dispose()
    {
        foreach (var thumbnail in _cache.Values)
        {
            thumbnail?.Dispose();
        }

        _cache.Clear();
    }

    private static Bitmap? LoadThumbnail(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            return Bitmap.DecodeToWidth(stream, DecodeWidth, BitmapInterpolationMode.LowQuality);
        }
        catch (Exception ex) when (ex is IOException
            or UnauthorizedAccessException
            or NotSupportedException
            or ArgumentException)
        {
            return null;
        }
    }
}
