using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using WallpaperSwitcher.Desktop.Services;
using WallpaperSwitcher.Desktop.ViewModels;

namespace WallpaperSwitcher.Desktop;

public sealed class MainWindow : Window
{
    private bool _hasRestoredBookmark;
    private ThemePalette _palette;

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        Title = "Wallpaper Switcher";
        Width = 1100;
        Height = 720;
        MinWidth = 940;
        MinHeight = 560;
        Icon = AppIcons.LoadAppIcon();
        _palette = ThemePalette.FromTheme(ActualThemeVariant);
        Background = _palette.Brush(_palette.WindowBackground);
        Content = BuildLayout();

        Opened += async (_, _) =>
        {
            // Restoring a macOS security-scoped bookmark needs a TopLevel, which
            // is why it lives here and not with the rest of startup in App. The
            // window can be reopened from the tray, so only do it once.
            if (_hasRestoredBookmark)
            {
                return;
            }

            _hasRestoredBookmark = true;
            await RestoreBookmarkedFolderAsync();
        };

        Closing += (_, args) =>
        {
            // Only intercept the user clicking the close button. An application
            // or OS shutdown must be allowed through, or the machine cannot log
            // off while the app sits in the tray.
            if (args.CloseReason != WindowCloseReason.WindowClosing)
            {
                return;
            }

            args.Cancel = true;
            Hide();
        };

        ActualThemeVariantChanged += (_, _) =>
        {
            _palette = ThemePalette.FromTheme(ActualThemeVariant);
            Background = _palette.Brush(_palette.WindowBackground);
            Content = BuildLayout();
        };
    }

    private Control BuildLayout()
    {
        var root = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto")
        };

        root.Children.Add(BuildHeader());
        root.Children.Add(BuildControls());
        root.Children.Add(BuildWallpaperTable());
        root.Children.Add(BuildStatusBar());

        return root;
    }

    private Control BuildHeader()
    {
        var panel = Card();
        Grid.SetRow(panel, 0);
        panel.Margin = new Thickness(0, 0, 0, 14);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };

        var copy = new StackPanel();
        copy.Children.Add(new TextBlock
        {
            Text = "Wallpaper Switcher",
            FontSize = 22,
            FontWeight = FontWeight.SemiBold,
            Foreground = _palette.Brush(_palette.Text)
        });
        copy.Children.Add(new TextBlock
        {
            Text = "Point to one folder, mark each image as Day or Night, and let the app handle the rest.",
            Margin = new Thickness(0, 6, 0, 0),
            Foreground = _palette.Brush(_palette.MutedText),
            TextWrapping = TextWrapping.Wrap
        });

        var applyNow = ActionButton("Apply now", () => ViewModel.ApplyNow(), primary: true);
        Grid.SetColumn(applyNow, 1);

        grid.Children.Add(copy);
        grid.Children.Add(applyNow);
        panel.Child = grid;
        return panel;
    }

    private Control BuildControls()
    {
        var outer = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto"),
            Margin = new Thickness(0, 0, 0, 14)
        };
        Grid.SetRow(outer, 1);

        var folderCard = Card();

        var folderGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto"),
            ColumnDefinitions = new ColumnDefinitions("*")
        };

        folderGrid.Children.Add(Label("Wallpaper folder"));

        var folderRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,258"),
            Margin = new Thickness(0, 8, 0, 0)
        };

        var folderText = new TextBox
        {
            MinHeight = 34,
            VerticalContentAlignment = VerticalAlignment.Center,
            PlaceholderText = "Choose or paste a folder path",
            Background = _palette.Brush(_palette.InputBackground),
            Foreground = _palette.Brush(_palette.Text),
            BorderBrush = _palette.Brush(_palette.InputBorder)
        };
        folderText.Bind(TextBox.TextProperty, TwoWay(nameof(MainWindowViewModel.WallpaperDirectory)));
        folderRow.Children.Add(folderText);

        var folderActions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(10, 0, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var browse = ActionButton("Browse", async () => await BrowseFolderAsync());
        folderActions.Children.Add(browse);

        var refresh = ActionButton("Refresh", async () => await RefreshFolderAsync());
        folderActions.Children.Add(refresh);

        var save = ActionButton("Save", () => ViewModel.Save());
        folderActions.Children.Add(save);
        Grid.SetColumn(folderActions, 1);
        folderRow.Children.Add(folderActions);

        Grid.SetRow(folderRow, 1);
        folderGrid.Children.Add(folderRow);

        var supported = new TextBlock
        {
            Margin = new Thickness(0, 8, 0, 0),
            Foreground = _palette.Brush(_palette.MutedText),
            TextWrapping = TextWrapping.Wrap
        };
        supported.Bind(TextBlock.TextProperty, OneWay(nameof(MainWindowViewModel.SupportedFileSummary)));
        Grid.SetRow(supported, 2);
        folderGrid.Children.Add(supported);

        folderCard.Child = folderGrid;
        Grid.SetRow(folderCard, 0);
        outer.Children.Add(folderCard);

        var scheduleCard = Card();
        scheduleCard.Margin = new Thickness(0, 10, 0, 0);

        var schedulePanel = new WrapPanel
        {
            Orientation = Orientation.Horizontal
        };

        schedulePanel.Children.Add(FieldGroup("Day starts", TimeTextBox(nameof(MainWindowViewModel.DayStartText))));

        var shuffle = new ComboBox
        {
            Width = 220,
            MinHeight = 34,
            Margin = new Thickness(10, 0, 0, 0),
            Background = _palette.Brush(_palette.InputBackground),
            Foreground = _palette.Brush(_palette.Text),
            BorderBrush = _palette.Brush(_palette.InputBorder)
        };
        shuffle.Bind(ItemsControl.ItemsSourceProperty, OneWay(nameof(MainWindowViewModel.ShuffleOptions)));
        shuffle.Bind(ComboBox.SelectedItemProperty, TwoWay(nameof(MainWindowViewModel.SelectedShuffleOption)));

        schedulePanel.Children.Add(FieldGroup("Night starts", TimeTextBox(nameof(MainWindowViewModel.NightStartText))));
        schedulePanel.Children.Add(FieldGroup("Shuffle", shuffle));

        if (OperatingSystem.IsWindows())
        {
            // Only Windows honours the fit setting; macOS and Linux desktops
            // decide how to scale the wallpaper themselves.
            var fit = new ComboBox
            {
                Width = 130,
                MinHeight = 34,
                Margin = new Thickness(10, 0, 0, 0),
                Background = _palette.Brush(_palette.InputBackground),
                Foreground = _palette.Brush(_palette.Text),
                BorderBrush = _palette.Brush(_palette.InputBorder)
            };
            fit.Bind(ItemsControl.ItemsSourceProperty, OneWay(nameof(MainWindowViewModel.FitOptions)));
            fit.Bind(ComboBox.SelectedItemProperty, TwoWay(nameof(MainWindowViewModel.SelectedFitOption)));
            fit.SelectionChanged += (_, _) =>
            {
                if (fit.SelectedItem is WallpaperFitOption option)
                {
                    ViewModel.SetWallpaperFit(option.Value);
                }
            };
            schedulePanel.Children.Add(FieldGroup("Fit", fit));
        }

        var startAtLogin = new CheckBox
        {
            Content = "Start at login",
            Margin = new Thickness(18, 4, 0, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _palette.Brush(_palette.Text)
        };
        startAtLogin.Bind(ToggleButton.IsCheckedProperty, OneWay(nameof(MainWindowViewModel.StartAtLogin)));
        startAtLogin.Click += (_, _) => ViewModel.SetStartAtLogin(startAtLogin.IsChecked == true);
        schedulePanel.Children.Add(startAtLogin);

        var startMinimized = new CheckBox
        {
            Content = "Start minimized",
            Margin = new Thickness(18, 4, 0, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _palette.Brush(_palette.Text)
        };
        startMinimized.Bind(ToggleButton.IsCheckedProperty, OneWay(nameof(MainWindowViewModel.StartMinimized)));
        startMinimized.Click += (_, _) => ViewModel.SetStartMinimized(startMinimized.IsChecked == true);
        schedulePanel.Children.Add(startMinimized);

        scheduleCard.Child = schedulePanel;
        Grid.SetRow(scheduleCard, 1);
        outer.Children.Add(scheduleCard);

        return outer;
    }

    private Control BuildWallpaperTable()
    {
        var panel = Card();
        panel.Padding = new Thickness(0);
        Grid.SetRow(panel, 2);

        var grid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*")
        };

        var header = new Grid
        {
            Margin = new Thickness(16, 16, 16, 10),
            ColumnDefinitions = new ColumnDefinitions("*")
        };

        var headerCopy = new StackPanel();
        headerCopy.Children.Add(new TextBlock
        {
            Text = "Image assignments",
            FontSize = 15,
            FontWeight = FontWeight.SemiBold,
            Foreground = _palette.Brush(_palette.Text)
        });
        headerCopy.Children.Add(new TextBlock
        {
            Text = "Images with 'day' or 'night' in the file name are pre-tagged automatically. Everything else starts as Ignore.",
            Margin = new Thickness(0, 4, 0, 0),
            Foreground = _palette.Brush(_palette.MutedText),
            TextWrapping = TextWrapping.Wrap
        });
        header.Children.Add(headerCopy);

        var tableGrid = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*"),
            Margin = new Thickness(16, 0, 16, 16)
        };

        var tableHeader = BuildWallpaperHeaderRow();
        tableGrid.Children.Add(tableHeader);

        var wallpaperItems = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<WallpaperItem>((_, _) => BuildWallpaperItemRow(), false)
        };
        wallpaperItems.Bind(ItemsControl.ItemsSourceProperty, OneWay(nameof(MainWindowViewModel.WallpaperItems)));

        var wallpaperScroller = new ScrollViewer
        {
            Content = wallpaperItems
        };

        var wallpaperList = new Border
        {
            Child = wallpaperScroller,
            BorderThickness = new Thickness(1),
            BorderBrush = _palette.Brush(_palette.Border),
            Background = _palette.Brush(_palette.InputBackground)
        };
        Grid.SetRow(wallpaperList, 1);
        tableGrid.Children.Add(wallpaperList);

        Grid.SetRow(tableGrid, 1);
        grid.Children.Add(header);
        grid.Children.Add(tableGrid);
        panel.Child = grid;
        return panel;
    }

    private Control BuildWallpaperHeaderRow()
    {
        var row = WallpaperRowGrid();
        row.Background = _palette.Brush(_palette.HeaderBackground);
        row.Children.Add(HeaderCell("Preview", 0));
        row.Children.Add(HeaderCell("File", 1));
        row.Children.Add(HeaderCell("Use As", 2));
        row.Children.Add(HeaderCell("Path", 3));
        return row;
    }

    private Control BuildWallpaperItemRow()
    {
        var row = WallpaperRowGrid();
        row.MinHeight = 62;

        var previewFrame = new Border
        {
            Width = 72,
            Height = 46,
            Margin = new Thickness(10, 8),
            Background = _palette.Brush(_palette.HeaderBackground),
            BorderBrush = _palette.Brush(_palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true
        };
        var preview = new Image
        {
            Stretch = Stretch.UniformToFill
        };
        preview.Bind(Image.SourceProperty, new Binding(nameof(WallpaperItem.FullPath))
        {
            Mode = BindingMode.OneWay,
            Converter = ThumbnailCache.Instance
        });
        previewFrame.Child = preview;
        Grid.SetColumn(previewFrame, 0);
        row.Children.Add(previewFrame);

        var file = CellText();
        file.Bind(TextBlock.TextProperty, OneWay(nameof(WallpaperItem.FileName)));
        Grid.SetColumn(file, 1);
        row.Children.Add(file);

        var categoryButtons = CategoryButtonGroup();
        Grid.SetColumn(categoryButtons, 2);
        row.Children.Add(categoryButtons);

        var path = CellText();
        path.Bind(TextBlock.TextProperty, OneWay(nameof(WallpaperItem.FullPath)));
        Grid.SetColumn(path, 3);
        row.Children.Add(path);

        return row;
    }

    private static Grid WallpaperRowGrid()
    {
        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("92,220,250,*")
        };
    }


    private TextBlock HeaderCell(string text, int column)
    {
        var cell = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            Foreground = _palette.Brush(_palette.Text),
            Padding = new Thickness(10, 8),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetColumn(cell, column);
        return cell;
    }

    private TextBlock CellText()
    {
        return new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Padding = new Thickness(10, 7),
            Foreground = _palette.Brush(_palette.Text),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private Control BuildStatusBar()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Margin = new Thickness(2, 12, 2, 0)
        };
        Grid.SetRow(grid, 3);

        var status = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _palette.Brush(_palette.Text),
            TextWrapping = TextWrapping.Wrap
        };
        status.Bind(TextBlock.TextProperty, OneWay(nameof(MainWindowViewModel.StatusMessage)));

        var settings = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _palette.Brush(_palette.MutedText),
            Margin = new Thickness(12, 0, 0, 0)
        };
        settings.Bind(TextBlock.TextProperty, OneWay(nameof(MainWindowViewModel.SettingsPath)));
        Grid.SetColumn(settings, 1);

        grid.Children.Add(status);
        grid.Children.Add(settings);
        return grid;
    }

    private async Task BrowseFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select the folder that contains your wallpaper images",
            AllowMultiple = false
        });

        var selectedFolder = folders.FirstOrDefault();
        if (selectedFolder is null)
        {
            return;
        }

        var folderPath = selectedFolder.TryGetLocalPath() ?? selectedFolder.Path.LocalPath;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            ViewModel.StatusMessage = "The system did not provide a usable local path for that folder.";
            return;
        }

        var bookmark = await TrySaveBookmarkAsync(selectedFolder);
        var loadResult = await StorageFolderWallpaperLoader.LoadAsync(
            selectedFolder,
            ViewModel.BuildAssignmentSnapshot());

        ViewModel.SetWallpaperFolderFromStorage(folderPath, bookmark, loadResult);
    }

    private async Task RefreshFolderAsync()
    {
        var folder = await TryOpenSelectedStorageFolderAsync();
        if (folder is not null)
        {
            var folderPath = folder.TryGetLocalPath() ?? ViewModel.WallpaperDirectory;
            var loadResult = await StorageFolderWallpaperLoader.LoadAsync(
                folder,
                ViewModel.BuildAssignmentSnapshot());

            ViewModel.SetWallpaperFolderFromStorage(
                folderPath,
                ViewModel.WallpaperFolderBookmark,
                loadResult,
                "Folder refreshed.");
            return;
        }

        ViewModel.RefreshFolder();
    }

    private async Task RestoreBookmarkedFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(ViewModel.WallpaperFolderBookmark))
        {
            return;
        }

        var folder = await TryOpenBookmarkAsync(ViewModel.WallpaperFolderBookmark);
        if (folder is null)
        {
            ViewModel.StatusMessage = "Access to the saved wallpaper folder is no longer available. Choose it again with Browse.";
            return;
        }

        var folderPath = folder.TryGetLocalPath() ?? ViewModel.WallpaperDirectory;
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            ViewModel.StatusMessage = "The saved folder reopened but did not provide a usable local path.";
            return;
        }

        var loadResult = await StorageFolderWallpaperLoader.LoadAsync(
            folder,
            ViewModel.BuildAssignmentSnapshot());

        ViewModel.SetWallpaperFolderFromStorage(
            folderPath,
            ViewModel.WallpaperFolderBookmark,
            loadResult,
            "Settings loaded.");
    }

    private async Task<IStorageFolder?> TryOpenSelectedStorageFolderAsync()
    {
        if (!string.IsNullOrWhiteSpace(ViewModel.WallpaperFolderBookmark))
        {
            var bookmarkedFolder = await TryOpenBookmarkAsync(ViewModel.WallpaperFolderBookmark);
            if (bookmarkedFolder is not null)
            {
                return bookmarkedFolder;
            }
        }

        if (string.IsNullOrWhiteSpace(ViewModel.WallpaperDirectory))
        {
            return null;
        }

        try
        {
            return await StorageProvider.TryGetFolderFromPathAsync(ViewModel.WallpaperDirectory);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            ViewModel.StatusMessage = "Access to that folder was denied. Choose it again with Browse.";
            return null;
        }
    }

    private static async Task<string?> TrySaveBookmarkAsync(IStorageFolder folder)
    {
        if (!folder.CanBookmark)
        {
            return null;
        }

        try
        {
            return await folder.SaveBookmarkAsync();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private async Task<IStorageFolder?> TryOpenBookmarkAsync(string bookmark)
    {
        try
        {
            return await StorageProvider.OpenFolderBookmarkAsync(bookmark);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            return null;
        }
    }

    private Border Card()
    {
        return new Border
        {
            Padding = new Thickness(16),
            Background = _palette.Brush(_palette.CardBackground),
            BorderBrush = _palette.Brush(_palette.Border),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8)
        };
    }

    private TextBlock Label(string text)
    {
        return new TextBlock
        {
            Text = text,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _palette.Brush(_palette.Text),
            FontWeight = FontWeight.SemiBold
        };
    }

    private TextBox TimeTextBox(string propertyName)
    {
        var textBox = new TextBox
        {
            Width = 112,
            MinHeight = 34,
            VerticalContentAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 14, 0),
            Background = _palette.Brush(_palette.InputBackground),
            Foreground = _palette.Brush(_palette.Text),
            BorderBrush = _palette.Brush(_palette.InputBorder)
        };
        textBox.Bind(TextBox.TextProperty, TwoWay(propertyName));
        return textBox;
    }

    private StackPanel FieldGroup(string labelText, Control field)
    {
        var group = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 4, 18, 4)
        };

        group.Children.Add(Label(labelText));
        group.Children.Add(field);
        return group;
    }

    private Control ActionButton(string text, Action action, bool primary = false)
    {
        var label = new TextBlock
        {
            Text = text,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = _palette.Brush(primary ? _palette.AccentText : _palette.ButtonText)
        };

        var button = new Border
        {
            MinWidth = primary ? 104 : 72,
            Height = primary ? 36 : 34,
            Margin = primary ? new Thickness(16, 0, 0, 0) : default,
            Padding = new Thickness(12, 4),
            Background = _palette.Brush(primary ? _palette.Accent : _palette.ButtonBackground),
            BorderBrush = _palette.Brush(primary ? _palette.Accent : _palette.InputBorder),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Cursor = new Cursor(StandardCursorType.Hand),
            Child = label
        };

        var normalBackground = primary ? _palette.Accent : _palette.ButtonBackground;
        var hoverBackground = primary ? _palette.AccentHover : _palette.ButtonHover;

        button.PointerEntered += (_, _) => button.Background = _palette.Brush(hoverBackground);
        button.PointerExited += (_, _) => button.Background = _palette.Brush(normalBackground);
        button.PointerPressed += (_, _) => button.Background = _palette.Brush(primary ? _palette.AccentHover : _palette.ButtonPressed);
        button.PointerReleased += (_, _) =>
        {
            button.Background = _palette.Brush(hoverBackground);
            action();
        };

        return button;
    }

    private Control CategoryButtonGroup()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(8, 10),
            VerticalAlignment = VerticalAlignment.Center
        };

        var buttons = new Dictionary<WallpaperCategory, Border>();
        WallpaperItem? currentItem = null;

        void Refresh()
        {
            foreach (var (category, button) in buttons)
            {
                var text = (TextBlock)button.Child!;
                var isActive = currentItem?.Category == category;
                button.Background = _palette.Brush(isActive ? _palette.ActiveButtonBackground : _palette.ButtonBackground);
                button.BorderBrush = _palette.Brush(isActive ? _palette.ActiveButtonBackground : _palette.InputBorder);
                text.Foreground = _palette.Brush(isActive ? _palette.ActiveButtonText : _palette.ButtonText);
            }
        }

        void Attach(WallpaperItem? item)
        {
            if (currentItem is not null)
            {
                currentItem.PropertyChanged -= OnWallpaperItemPropertyChanged;
            }

            currentItem = item;

            if (currentItem is not null)
            {
                currentItem.PropertyChanged += OnWallpaperItemPropertyChanged;
            }

            Refresh();
        }

        void OnWallpaperItemPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(WallpaperItem.Category))
            {
                Refresh();
            }
        }

        foreach (var category in Enum.GetValues<WallpaperCategory>())
        {
            var label = new TextBlock
            {
                Text = category.ToString(),
                FontWeight = FontWeight.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var button = new Border
            {
                MinWidth = 68,
                Height = 32,
                Padding = new Thickness(10, 4),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = label
            };

            button.PointerEntered += (_, _) =>
            {
                if (currentItem?.Category != category)
                {
                    button.Background = _palette.Brush(_palette.ButtonHover);
                }
            };
            button.PointerExited += (_, _) => Refresh();
            button.PointerPressed += (_, _) =>
            {
                button.Background = _palette.Brush(_palette.ButtonPressed);
            };
            button.PointerReleased += (_, _) =>
            {
                if (currentItem is not null)
                {
                    currentItem.Category = category;
                }

                Refresh();
            };

            buttons[category] = button;
            panel.Children.Add(button);
        }

        panel.DataContextChanged += (_, _) => Attach(panel.DataContext as WallpaperItem);
        panel.DetachedFromVisualTree += (_, _) => Attach(null);
        Refresh();

        return panel;
    }

    private static Binding OneWay(string path)
    {
        return new Binding(path)
        {
            Mode = BindingMode.OneWay
        };
    }

    private static Binding TwoWay(string path)
    {
        return new Binding(path)
        {
            Mode = BindingMode.TwoWay
        };
    }

}
