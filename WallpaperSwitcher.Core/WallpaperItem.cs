using System.ComponentModel;

namespace WallpaperSwitcher;

public sealed class WallpaperItem : INotifyPropertyChanged
{
    private WallpaperCategory _category;

    public WallpaperItem(string fileName, string fullPath, WallpaperCategory category)
    {
        FileName = fileName;
        FullPath = fullPath;
        _category = category;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FileName { get; }

    public string FullPath { get; }

    public WallpaperCategory Category
    {
        get => _category;
        set
        {
            if (_category == value)
            {
                return;
            }

            _category = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Category)));
        }
    }
}
