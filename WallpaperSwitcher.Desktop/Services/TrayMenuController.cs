using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using WallpaperSwitcher.Desktop.ViewModels;

namespace WallpaperSwitcher.Desktop.Services;

public sealed class TrayMenuController : IDisposable
{
    private readonly MainWindowViewModel _viewModel;
    private readonly Action _showWindow;
    private readonly Action _quit;
    private readonly TrayIcon _trayIcon;
    private readonly List<(NativeMenuItem Item, ShuffleCadence Cadence)> _shuffleItems = [];
    private readonly NativeMenuItem _startAtLoginItem;

    /// <summary>
    /// Takes an action rather than a <see cref="Window"/> so the tray can outlive
    /// (and predate) the window it opens.
    /// </summary>
    public TrayMenuController(MainWindowViewModel viewModel, WindowIcon icon, Action showWindow, Action quit)
    {
        _viewModel = viewModel;
        _showWindow = showWindow;
        _quit = quit;

        _startAtLoginItem = new NativeMenuItem
        {
            Header = "Start at login",
            ToggleType = MenuItemToggleType.CheckBox,
            IsChecked = _viewModel.StartAtLogin
        };
        _startAtLoginItem.Click += (_, _) =>
        {
            RunOnUiThread(() =>
            {
                _viewModel.SetStartAtLogin(!_viewModel.StartAtLogin);
                RefreshCheckedItems();
            });
        };

        _trayIcon = new TrayIcon
        {
            Icon = icon,
            ToolTipText = "Wallpaper Switcher",
            IsVisible = true,
            Menu = BuildMenu()
        };
        _trayIcon.Clicked += (_, _) => RunOnUiThread(_showWindow);

        if (OperatingSystem.IsMacOS())
        {
            MacOSProperties.SetIsTemplateIcon(_trayIcon, true);
        }

        if (Application.Current is not null)
        {
            TrayIcon.SetIcons(Application.Current, new TrayIcons { _trayIcon });
        }
    }

    public void Dispose()
    {
        if (Application.Current is not null)
        {
            TrayIcon.SetIcons(Application.Current, new TrayIcons());
        }

        _trayIcon.Dispose();
    }

    private NativeMenu BuildMenu()
    {
        var menu = new NativeMenu();
        menu.Items.Add(CreateMenuItem("Open Wallpaper Switcher", _showWindow));
        menu.Items.Add(CreateMenuItem("Cycle wallpaper now", _viewModel.CycleNow));
        menu.Items.Add(CreateMenuItem("Swap day/night hours", () =>
        {
            _viewModel.SwapDayNightHours();
            _viewModel.Save();
        }));
        menu.Items.Add(new NativeMenuItemSeparator());

        foreach (var option in _viewModel.ShuffleOptions)
        {
            var item = new NativeMenuItem
            {
                Header = "Cycle " + option.Label.ToLowerInvariant(),
                ToggleType = MenuItemToggleType.Radio,
                IsChecked = _viewModel.SelectedShuffleOption.Value == option.Value
            };
            item.Click += (_, _) =>
            {
                _viewModel.SetShuffleCadence(option.Value);
                _viewModel.Save();
                RefreshCheckedItems();
            };
            _shuffleItems.Add((item, option.Value));
            menu.Items.Add(item);
        }

        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(_startAtLoginItem);
        menu.Items.Add(CreateMenuItem("Open log folder", OpenLogFolder));
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(CreateMenuItem("Quit", _quit));
        menu.NeedsUpdate += (_, _) => RefreshCheckedItems();

        return menu;
    }

    private static NativeMenuItem CreateMenuItem(string header, Action action)
    {
        var item = new NativeMenuItem
        {
            Header = header
        };
        item.Click += (_, _) => RunOnUiThread(action);
        return item;
    }

    private static void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(AppLog.LogDirectory);
            Process.Start(new ProcessStartInfo(AppLog.LogDirectory) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException)
        {
            AppLog.Warn($"Could not open the log folder: {ex.Message}");
        }
    }

    private void RefreshCheckedItems()
    {
        foreach (var (item, cadence) in _shuffleItems)
        {
            item.IsChecked = _viewModel.SelectedShuffleOption.Value == cadence;
        }

        _startAtLoginItem.IsChecked = _viewModel.StartAtLogin;
    }

    private static void RunOnUiThread(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action);
    }
}
