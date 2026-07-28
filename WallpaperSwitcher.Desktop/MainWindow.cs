using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using WallpaperSwitcher.Desktop.Services;
using WallpaperSwitcher.Desktop.Theming;
using WallpaperSwitcher.Desktop.ViewModels;
using WallpaperSwitcher.Desktop.Views;

using PathShape = Avalonia.Controls.Shapes.Path;

namespace WallpaperSwitcher.Desktop;

public sealed class MainWindow : Window
{
    private const double TitleBarHeight = 48;
    private const double NavPaneWidth = 200;
    private const double StatusBarHeight = 34;

    private bool _hasRestoredBookmark;

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    public MainWindow()
    {
        Title = "Wallpaper Switcher";

        // The mockup calls for 900x620. That is the width at which the four-column
        // grid exactly reaches the pane's right padding, so the last column sits
        // flush against the edge and any scrollbar overlaps it. A little more room
        // keeps the design's proportions while leaving the grid a real gutter, and
        // the raised minimum stops the hero's buttons from being squeezed out.
        // Height is set by the Settings page, which is the taller of the two: at
        // 700 its last row ("Logs and settings file") sat half under the status
        // bar, and because Fluent's scrollbars are overlays that only appear on
        // hover, it read as clipped rather than scrollable.
        Width = 960;
        Height = 760;
        MinWidth = 780;
        MinHeight = 560;
        Icon = AppIcons.LoadAppIcon();

        this.Dyn(BackgroundProperty, "SolidBackgroundFillColorBaseBrush");

        // Avalonia's drawn chrome offers full screen, minimize, maximize and
        // close. Full screen makes no sense for a window this size, and it is the
        // one caption button Windows itself never shows. Avalonia 12 tags each
        // caption button with an ElementRole rather than a template part name,
        // so the button is matched on that.
        Styles.Add(new Style(x => x.PropertyEquals(
            WindowDecorationProperties.ElementRoleProperty,
            WindowDecorationsElementRole.FullScreenButton))
        {
            Setters = { new Setter(IsVisibleProperty, false) }
        });

        // The 48px bar with the inline app icon is a Windows convention. macOS
        // owns the top-left for its traffic lights and Linux window managers vary
        // too much to hand-roll chrome for, so those keep their native title bar
        // and the content simply starts below it. Everything inside the window is
        // identical on all three.
        if (OperatingSystem.IsWindows())
        {
            // Windows gets the design's 48px bar. Avalonia draws the icon, title
            // and caption buttons into it, and the content is inset below by
            // WindowDecorationMargin. Drawing our own title on top of that is
            // what produced doubled text, so the decorations own the bar
            // entirely. macOS and Linux keep their native chrome, where the
            // margin is zero and this is a no-op.
            ExtendClientAreaToDecorationsHint = true;
            ExtendClientAreaTitleBarHeightHint = TitleBarHeight;
        }

        Content = BuildShell();

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
    }

    private Control BuildShell()
    {
        var root = new DockPanel { LastChildFill = true };

        // Keeps the content clear of whatever the platform reserves for chrome.
        root.Bind(MarginProperty, new Avalonia.Data.Binding(nameof(WindowDecorationMargin)) { Source = this });

        var statusBar = BuildStatusBar();
        DockPanel.SetDock(statusBar, Dock.Bottom);
        root.Children.Add(statusBar);

        root.Children.Add(BuildBody());
        return root;
    }

    private Control BuildBody()
    {
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions($"{NavPaneWidth},*")
        };

        var pages = new TabControl
        {
            TabStripPlacement = Dock.Left,
            Padding = new Thickness(0),
            ItemContainerTheme = NavItemTheme.Create(),
            Template = BuildNavTemplate()
        };
        pages.Bind(SelectingItemsControl.SelectedIndexProperty,
            new Avalonia.Data.Binding(nameof(MainWindowViewModel.SelectedPageIndex))
            {
                Mode = Avalonia.Data.BindingMode.TwoWay
            });

        pages.Items.Add(NavItem("Wallpapers", Icons.Image, new WallpapersPage(this)));
        pages.Items.Add(NavItem("Settings", Icons.Settings, new SettingsPage(this)));

        Grid.SetColumnSpan(pages, 2);
        grid.Children.Add(pages);
        return grid;
    }

    private static TabItem NavItem(string label, Icons.Icon icon, Control page)
    {
        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("16,*"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var glyph = Icons.Create(icon, 16);
        Grid.SetColumn(glyph, 0);
        header.Children.Add(glyph);

        var text = new TextBlock
        {
            Text = label,
            FontSize = 14,
            LineHeight = 20,
            Margin = new Thickness(14, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetColumn(text, 1);
        header.Children.Add(text);

        var item = new TabItem { Header = header, Content = page };
        AutomationProperties.SetName(item, label);
        return item;
    }

    /// <summary>
    /// Lays the tab strip out as a 200-wide rail with the content pane inset to
    /// its right, so the rail reads as chrome and the pane as content.
    /// </summary>
    private static FuncControlTemplate BuildNavTemplate() => new((_, scope) =>
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions($"{NavPaneWidth},*") };

        var strip = new ItemsPresenter
        {
            Margin = new Thickness(8, 4, 4, 12)
        }.Named(scope, "PART_ItemsPresenter");
        Grid.SetColumn(strip, 0);
        grid.Children.Add(strip);

        var pane = new Border
        {
            CornerRadius = new CornerRadius(8, 0, 0, 0),
            BorderThickness = new Thickness(1, 1, 0, 0),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                Padding = new Thickness(20, 14, 24, 20),
                Content = new ContentPresenter().Named(scope, "PART_SelectedContentHost")
            }
        }
            .Dyn(BackgroundProperty, "LayerFillColorDefaultBrush")
            .Dyn(BorderBrushProperty, "DividerStrokeColorDefaultBrush");

        Grid.SetColumn(pane, 1);
        grid.Children.Add(pane);
        return grid;
    });

    private Control BuildStatusBar()
    {
        var bar = new Border
        {
            Height = StatusBarHeight,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(24, 0)
        }
            .Dyn(BackgroundProperty, "FooterFillColorBrush")
            .Dyn(BorderBrushProperty, "DividerStrokeColorDefaultBrush");

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var glyph = Icons.Create(Icons.Check, 12);
        if (glyph is PathShape shape)
        {
            shape.Dyn(Shape.StrokeProperty, "TextFillColorSecondaryBrush");
        }
        glyph.Margin = new Thickness(0, 0, 12, 0);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(glyph, 0);
        layout.Children.Add(glyph);

        // No explicit LineHeight here. Pinning it to the design's 16px is below
        // what Segoe UI Variable Text actually needs at 12px, which pushed the
        // glyph box above the centre of the bar and clipped the descenders. The
        // font's own metrics centre correctly.
        var status = new TextBlock
        {
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush");
        status.Bind(TextBlock.TextProperty, new Avalonia.Data.Binding(nameof(MainWindowViewModel.StatusMessage)));

        // Applying a wallpaper is announced without stealing focus.
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(status, "Status");
        Grid.SetColumn(status, 1);
        layout.Children.Add(status);

        var version = new TextBlock
        {
            Text = $"v{MainWindowViewModel.AppVersion}",
            FontSize = 12,
            Margin = new Thickness(12, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush");
        Grid.SetColumn(version, 2);
        layout.Children.Add(version);

        bar.Child = layout;
        return bar;
    }

    // ---- Folder picking ----------------------------------------------------

    internal async Task BrowseFolderAsync()
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

        // StorageFolderWallpaperLoader walks the tree one async item at a time
        // through the platform storage provider, which is far slower than
        // Directory.GetFiles. It exists only so macOS security-scoped bookmarks
        // keep working; nothing else needs it.
        if (!OperatingSystem.IsMacOS())
        {
            ViewModel.SetWallpaperFolder(folderPath);
            return;
        }

        var bookmark = await TrySaveBookmarkAsync(selectedFolder);
        var loadResult = await StorageFolderWallpaperLoader.LoadAsync(
            selectedFolder,
            ViewModel.BuildAssignmentSnapshot());

        ViewModel.SetWallpaperFolderFromStorage(folderPath, bookmark, loadResult);
    }

    internal async Task RefreshFolderAsync()
    {
        if (!OperatingSystem.IsMacOS())
        {
            ViewModel.RefreshFolder();
            return;
        }

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
            ViewModel.StatusMessage = "Access to the saved wallpaper folder is no longer available. Choose it again.";
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

        ViewModel.SetWallpaperFolderFromStorage(folderPath, ViewModel.WallpaperFolderBookmark, loadResult, "Settings loaded.");
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
            ViewModel.StatusMessage = "Access to that folder was denied. Choose it again.";
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
}


