using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using WallpaperSwitcher.Desktop.Controls;
using WallpaperSwitcher.Desktop.Services;
using WallpaperSwitcher.Desktop.Theming;
using WallpaperSwitcher.Desktop.ViewModels;

using PathShape = Avalonia.Controls.Shapes.Path;

namespace WallpaperSwitcher.Desktop.Views;

/// <summary>
/// Source, Schedule and App in one scroll. Everything here saves as you change
/// it — there is no Save button.
/// </summary>
public sealed class SettingsPage : UserControl
{
    private readonly MainWindow _window;
    private Border? _sourceCard;
    private DispatcherTimer? _highlightTimer;

    public SettingsPage(MainWindow window)
    {
        _window = window;
        Content = Build();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private Control Build()
    {
        var root = new StackPanel { Spacing = 0 };

        root.Children.Add(new TextBlock
        {
            Text = "Settings",
            FontSize = 20,
            LineHeight = 26,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        }.Dyn(ForegroundProperty, "TextFillColorPrimaryBrush"));

        root.Children.Add(Ui.SectionHeading("Source", 0));
        root.Children.Add(BuildSource());

        root.Children.Add(Ui.SectionHeading("Schedule", 20));
        root.Children.Add(BuildSchedule());

        root.Children.Add(Ui.SectionHeading("App", 20));
        root.Children.Add(BuildApp());

        return root;
    }

    // ---- Source ------------------------------------------------------------

    private Control BuildSource()
    {
        var card = Ui.Card(minHeight: 56);
        _sourceCard = card;

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("20,*,Auto,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var folderGlyph = Icons.Create(Icons.Folder, 16);
        folderGlyph.VerticalAlignment = VerticalAlignment.Center;
        layout.Children.Add(folderGlyph);

        var copy = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(14, 0, 14, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        // No path text box: the picker is the only way in, so there is nothing to
        // mistype and nothing to validate.
        var path = new TextBlock
        {
            FontSize = 14,
            LineHeight = 20,
            TextTrimming = TextTrimming.CharacterEllipsis
        }.Dyn(ForegroundProperty, "TextFillColorPrimaryBrush");
        path.Bind(TextBlock.TextProperty, new Binding(nameof(MainWindowViewModel.FolderDisplayPath)));
        copy.Children.Add(path);

        var summary = new TextBlock
        {
            FontSize = 12,
            LineHeight = 16,
            TextTrimming = TextTrimming.CharacterEllipsis
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush");
        summary.Bind(TextBlock.TextProperty, new Binding(nameof(MainWindowViewModel.FolderSummary)));
        copy.Children.Add(summary);

        Grid.SetColumn(copy, 1);
        layout.Children.Add(copy);

        var refresh = Ui.IconButton(Icons.Refresh, "Rescan this folder", async () => await _window.RefreshFolderAsync());
        refresh.Bind(IsVisibleProperty, new Binding(nameof(MainWindowViewModel.HasFolder)));
        refresh.Margin = new Thickness(0, 0, 8, 0);
        Grid.SetColumn(refresh, 2);
        layout.Children.Add(refresh);

        var change = Ui.AccentButton("Choose folder", async () => await _window.BrowseFolderAsync());
        change.Bind(ContentControl.ContentProperty, new Binding(nameof(MainWindowViewModel.HasFolder))
        {
            Converter = new FuncValueConverter<bool, string>(has => has ? "Change folder" : "Choose folder")
        });
        Grid.SetColumn(change, 3);
        layout.Children.Add(change);

        card.Child = layout;

        // Arriving from the hero or the empty state rings the card, so the jump is
        // legible without a dialog.
        DataContextChanged += (_, _) => HookHighlight();
        return card;
    }

    private void HookHighlight()
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(MainWindowViewModel.HighlightSource) || !viewModel.HighlightSource)
            {
                return;
            }

            ApplyHighlight(true);

            _highlightTimer?.Stop();
            _highlightTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(2600) };
            _highlightTimer.Tick += (_, _) =>
            {
                _highlightTimer?.Stop();
                _highlightTimer = null;
                ApplyHighlight(false);
                viewModel.ClearSourceHighlight();
            };
            _highlightTimer.Start();
        };
    }

    private void ApplyHighlight(bool on)
    {
        if (_sourceCard is null)
        {
            return;
        }

        if (on)
        {
            _sourceCard.BorderThickness = new Thickness(2);
            _sourceCard.Dyn(Border.BorderBrushProperty, "AccentFillColorDefaultBrush");
            _sourceCard.Margin = new Thickness(2);
        }
        else
        {
            _sourceCard.BorderThickness = new Thickness(1);
            _sourceCard.Dyn(Border.BorderBrushProperty, "ControlStrokeColorDefaultBrush");
            _sourceCard.Margin = new Thickness(0);
        }
    }

    // ---- Schedule ----------------------------------------------------------

    private Control BuildSchedule()
    {
        var card = Ui.Card(padding: new Thickness(14));
        var stack = new StackPanel { Spacing = 0 };

        var bar = new TwentyFourHourBar();
        bar.Bind(TwentyFourHourBar.DayStartProperty, new Binding(nameof(MainWindowViewModel.DayStart))
        {
            Mode = BindingMode.TwoWay
        });
        bar.Bind(TwentyFourHourBar.NightStartProperty, new Binding(nameof(MainWindowViewModel.NightStart))
        {
            Mode = BindingMode.TwoWay
        });
        stack.Children.Add(bar);

        stack.Children.Add(new TextBlock
        {
            Text = "Drag a handle to set when day and night begin. Arrow keys move it in 15-minute steps.",
            FontSize = 12,
            LineHeight = 16,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 0)
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush"));

        var shuffleRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            Margin = new Thickness(0, 14, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        shuffleRow.Children.Add(new TextBlock
        {
            Text = "Shuffle",
            FontSize = 14,
            LineHeight = 20,
            VerticalAlignment = VerticalAlignment.Center
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush"));

        var shuffle = new ComboBox
        {
            Width = 170,
            Height = Ui.ControlHeight,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(shuffle, "Shuffle");
        shuffle.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(MainWindowViewModel.ShuffleOptions)));
        shuffle.Bind(SelectingItemsControl.SelectedItemProperty,
            new Binding(nameof(MainWindowViewModel.SelectedShuffleOption)) { Mode = BindingMode.TwoWay });
        shuffle.SelectionChanged += (_, _) =>
        {
            if (shuffle.SelectedItem is ShuffleOption option && DataContext is MainWindowViewModel vm)
            {
                vm.SetShuffleCadence(option.Value);
            }
        };
        shuffleRow.Children.Add(shuffle);

        shuffleRow.Children.Add(new TextBlock
        {
            Text = "Picks another image from the current set.",
            FontSize = 12,
            LineHeight = 16,
            VerticalAlignment = VerticalAlignment.Center
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush"));

        stack.Children.Add(shuffleRow);
        card.Child = stack;
        return card;
    }

    // ---- App ---------------------------------------------------------------

    private Control BuildApp()
    {
        var rows = new StackPanel { Spacing = 2 };

        if (OperatingSystem.IsWindows())
        {
            rows.Children.Add(BuildFitRow());
        }

        rows.Children.Add(BuildToggleRow(
            Icons.Clock,
            "Start when I sign in",
            "Keeps the schedule running after a restart.",
            nameof(MainWindowViewModel.StartAtLogin),
            enabled => ViewModel.SetStartAtLogin(enabled)));

        rows.Children.Add(BuildToggleRow(
            Icons.Image,
            "Start in the tray",
            "No window on startup. Closing the window always leaves the app running.",
            nameof(MainWindowViewModel.StartMinimized),
            enabled => ViewModel.SetStartMinimized(enabled)));

        rows.Children.Add(BuildLogRow());
        return rows;
    }

    private Control BuildFitRow()
    {
        var card = Ui.Card(minHeight: 48, padding: new Thickness(14, 8));
        var layout = BuildRowLayout(Icons.Sun, "Fit", "How the image is scaled. Windows only.");

        var fit = new ComboBox
        {
            Width = 140,
            Height = Ui.ControlHeight,
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(fit, "Fit");
        fit.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(MainWindowViewModel.FitOptions)));
        fit.Bind(SelectingItemsControl.SelectedItemProperty,
            new Binding(nameof(MainWindowViewModel.SelectedFitOption)) { Mode = BindingMode.TwoWay });
        fit.SelectionChanged += (_, _) =>
        {
            if (fit.SelectedItem is WallpaperFitOption option && DataContext is MainWindowViewModel vm)
            {
                vm.SetWallpaperFit(option.Value);
            }
        };

        Grid.SetColumn(fit, 2);
        layout.Children.Add(fit);
        card.Child = layout;
        return card;
    }

    private Control BuildToggleRow(Icons.Icon icon, string title, string subtitle, string bindingPath, Action<bool> onChanged)
    {
        var card = Ui.Card(minHeight: 48, padding: new Thickness(14, 8));
        var layout = BuildRowLayout(icon, title, subtitle);

        var toggle = new ToggleSwitch
        {
            OnContent = "On",
            OffContent = "Off",
            VerticalAlignment = VerticalAlignment.Center
        };
        AutomationProperties.SetName(toggle, title);
        toggle.Bind(ToggleSwitch.IsCheckedProperty, new Binding(bindingPath));

        // The binding is one-way from the view model, because the setters may
        // refuse (launch-at-login can fail) and re-report the real state.
        toggle.IsCheckedChanged += (_, _) =>
        {
            if (DataContext is MainWindowViewModel vm
                && toggle.IsChecked is { } isChecked
                && isChecked != GetCurrent(vm, bindingPath))
            {
                onChanged(isChecked);
            }
        };

        Grid.SetColumn(toggle, 2);
        layout.Children.Add(toggle);
        card.Child = layout;
        return card;
    }

    private static bool GetCurrent(MainWindowViewModel viewModel, string bindingPath) => bindingPath switch
    {
        nameof(MainWindowViewModel.StartAtLogin) => viewModel.StartAtLogin,
        nameof(MainWindowViewModel.StartMinimized) => viewModel.StartMinimized,
        _ => false
    };

    private Control BuildLogRow()
    {
        var card = Ui.Card(minHeight: 48, padding: new Thickness(0));

        // A whole-row button rather than a Border that happens to respond to
        // clicks, so it is reachable by keyboard like everything else.
        var button = new Button
        {
            MinHeight = 48,
            Padding = new Thickness(14, 8),
            CornerRadius = new CornerRadius(4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };

        var layout = BuildRowLayout(Icons.Folder, "Logs and settings file", SettingsStore.AppDataDirectory);

        var openGlyph = Icons.Create(Icons.OpenExternal, 12);
        if (openGlyph is PathShape path)
        {
            path.Dyn(Shape.StrokeProperty, "TextFillColorSecondaryBrush");
        }
        openGlyph.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(openGlyph, 2);
        layout.Children.Add(openGlyph);

        button.Content = layout;
        button.Click += (_, _) => OpenDataFolder();
        AutomationProperties.SetName(button, "Open the logs and settings folder");

        card.Child = button;
        return card;
    }

    private void OpenDataFolder()
    {
        try
        {
            Directory.CreateDirectory(SettingsStore.AppDataDirectory);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(SettingsStore.AppDataDirectory)
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception
            or InvalidOperationException
            or IOException
            or UnauthorizedAccessException)
        {
            // Inline, never a dialog.
            ViewModel.StatusMessage = $"Could not open the folder: {ex.Message}";
            AppLog.Warn($"Could not open the data folder: {ex.Message}");
        }
    }

    private static Grid BuildRowLayout(Icons.Icon icon, string title, string subtitle)
    {
        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("20,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        var glyph = Icons.Create(icon, 16);
        glyph.VerticalAlignment = VerticalAlignment.Center;
        layout.Children.Add(glyph);

        var copy = new StackPanel
        {
            Spacing = 2,
            Margin = new Thickness(16, 0, 16, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        copy.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            LineHeight = 20
        }.Dyn(ForegroundProperty, "TextFillColorPrimaryBrush"));

        copy.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 12,
            LineHeight = 16,
            TextTrimming = TextTrimming.CharacterEllipsis
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush"));

        Grid.SetColumn(copy, 1);
        layout.Children.Add(copy);
        return layout;
    }
}



