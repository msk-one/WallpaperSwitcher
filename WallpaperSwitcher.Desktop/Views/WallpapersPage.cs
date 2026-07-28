using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;
using WallpaperSwitcher.Desktop.Services;
using WallpaperSwitcher.Desktop.Theming;
using WallpaperSwitcher.Desktop.ViewModels;

using PathShape = Avalonia.Controls.Shapes.Path;

namespace WallpaperSwitcher.Desktop.Views;

/// <summary>
/// The page anyone actually opens the window for: what is showing now, and the
/// grid of images with their Day/Night/Ignore assignment.
/// </summary>
public sealed class WallpapersPage : UserControl
{
    private readonly MainWindow _window;

    public WallpapersPage(MainWindow window)
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
            Text = "Wallpapers",
            FontSize = 20,
            LineHeight = 26,
            FontWeight = FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        }.Dyn(ForegroundProperty, "TextFillColorPrimaryBrush"));

        root.Children.Add(BuildHero());
        root.Children.Add(BuildImagesHeader());
        root.Children.Add(BuildGrid());
        root.Children.Add(BuildEmptyState());

        return root;
    }

    // ---- Hero --------------------------------------------------------------

    private Control BuildHero()
    {
        var host = new ContentControl
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            ContentTemplate = new FuncDataTemplate<MainWindowViewModel>((_, _) => BuildHeroCard(), true)
        };
        host.Bind(ContentControl.ContentProperty, new Binding("."));

        // NoFolder collapses the strip entirely rather than showing a hero with
        // dead controls; the empty panel below carries the only action.
        host.Bind(IsVisibleProperty, new Binding(nameof(MainWindowViewModel.HeroState))
        {
            Converter = new FuncValueConverter<HeroState, bool>(state => state != HeroState.NoFolder)
        });

        return host;
    }

    private Control BuildHeroCard()
    {
        var card = new Border
        {
            MinHeight = 60,
            Padding = new Thickness(14, 10),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1)
        }
            .Dyn(BackgroundProperty, "CardBackgroundFillColorDefaultBrush")
            .Dyn(BorderBrushProperty, "ControlStrokeColorDefaultBrush");

        // A missing folder is flagged with a 4px bar down the left, inline, never
        // a dialog.
        card.Bind(Border.BorderThicknessProperty, new Binding(nameof(MainWindowViewModel.HeroState))
        {
            Converter = new FuncValueConverter<HeroState, Thickness>(
                state => state == HeroState.FolderMissing ? new Thickness(4, 1, 1, 1) : new Thickness(1))
        });
        card.Bind(Border.BorderBrushProperty, new Binding(nameof(MainWindowViewModel.HeroState))
        {
            Converter = new FuncValueConverter<HeroState, IBrush?>(state => state == HeroState.FolderMissing
                ? Resolve("SystemFillColorCriticalBrush")
                : Resolve("ControlStrokeColorDefaultBrush"))
        });

        var layout = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center
        };

        layout.Children.Add(BuildHeroThumbnail());

        var copy = new StackPanel
        {
            Spacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 14, 0)
        };

        var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var title = new TextBlock
        {
            FontSize = 14,
            LineHeight = 20,
            FontWeight = FontWeight.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        }.Dyn(ForegroundProperty, "TextFillColorPrimaryBrush");
        title.Bind(TextBlock.TextProperty, new Binding(nameof(MainWindowViewModel.HeroTitle)));
        titleRow.Children.Add(title);
        copy.Children.Add(titleRow);

        var subtitle = new TextBlock
        {
            FontSize = 12,
            LineHeight = 16,
            TextWrapping = TextWrapping.Wrap
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush");
        subtitle.Bind(TextBlock.TextProperty, new Binding(nameof(MainWindowViewModel.HeroSubtitle)));
        copy.Children.Add(subtitle);

        Grid.SetColumn(copy, 1);
        layout.Children.Add(copy);

        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center
        };

        var next = Ui.Button("Next", Icons.Next, () => ViewModel.CycleNow());
        ToolTip.SetTip(next, "Cycle to the next wallpaper now");
        next.Bind(IsVisibleProperty, HeroIs(HeroState.Running));
        actions.Children.Add(next);

        var apply = Ui.AccentButton("Apply now", () => ViewModel.ApplyNow());
        apply.Bind(IsVisibleProperty, HeroIs(HeroState.Running));
        actions.Children.Add(apply);

        var fix = Ui.AccentButton("Fix in Settings", () => ViewModel.NavigateToSource());
        fix.Bind(IsVisibleProperty, HeroIs(HeroState.FolderMissing));
        actions.Children.Add(fix);

        Grid.SetColumn(actions, 2);
        layout.Children.Add(actions);

        card.Child = layout;
        return card;
    }

    private Control BuildHeroThumbnail()
    {
        var frame = new Border
        {
            Width = 64,
            Height = 40,
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1),
            ClipToBounds = true,
            VerticalAlignment = VerticalAlignment.Center
        }
            .Dyn(BackgroundProperty, "ControlAltFillColorSecondaryBrush")
            .Dyn(BorderBrushProperty, "ControlStrokeColorDefaultBrush");

        var stack = new Panel();

        var warning = Icons.Create(Icons.Warning, 18);
        if (warning is PathShape warningPath)
        {
            warningPath.Dyn(Shape.FillProperty, "SystemFillColorCriticalBrush");
        }
        warning.Bind(IsVisibleProperty, HeroIs(HeroState.FolderMissing));
        stack.Children.Add(warning);

        var image = new Image { Stretch = Stretch.UniformToFill };
        image.Bind(Image.SourceProperty, new Binding(nameof(MainWindowViewModel.HeroThumbnailPath))
        {
            Converter = ThumbnailCache.Instance
        });
        image.Bind(IsVisibleProperty, HeroIs(HeroState.Running));
        stack.Children.Add(image);

        frame.Child = stack;
        return frame;
    }

    private static Binding HeroIs(HeroState expected) =>
        new(nameof(MainWindowViewModel.HeroState))
        {
            Converter = new FuncValueConverter<HeroState, bool>(state => state == expected)
        };

    // ---- Images header -----------------------------------------------------

    private Control BuildImagesHeader()
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*"),
            Margin = new Thickness(0, 20, 0, 6)
        };

        // With no hero above it, this header is the top of the page.
        row.Bind(Layoutable.MarginProperty, new Binding(nameof(MainWindowViewModel.HeroState))
        {
            Converter = new FuncValueConverter<HeroState, Thickness>(
                state => state == HeroState.NoFolder ? new Thickness(0, 0, 0, 6) : new Thickness(0, 20, 0, 6))
        });

        var label = new TextBlock
        {
            Text = "Images",
            FontSize = 14,
            LineHeight = 20,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Bottom
        }.Dyn(ForegroundProperty, "TextFillColorPrimaryBrush");
        row.Children.Add(label);

        var counts = new TextBlock
        {
            FontSize = 12,
            LineHeight = 16,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Bottom
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush");
        counts.Bind(TextBlock.TextProperty, new Binding(nameof(MainWindowViewModel.CountsSummary)));
        Grid.SetColumn(counts, 1);
        row.Children.Add(counts);

        var hint = new TextBlock
        {
            Text = "Click a tile to cycle day → night → ignore",
            FontSize = 12,
            LineHeight = 16,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush");
        hint.Bind(IsVisibleProperty, new Binding(nameof(MainWindowViewModel.HasImages)));
        Grid.SetColumn(hint, 2);
        row.Children.Add(hint);

        return row;
    }

    // ---- Grid --------------------------------------------------------------

    private Control BuildGrid()
    {
        // ItemsRepeater with UniformGridLayout keeps the virtualization the old
        // list had: a folder of 500 images must not decode every thumbnail up
        // front. Tiles are 16:9 at roughly 155x87 in a four-column pane.
        var repeater = new ItemsRepeater
        {
            Layout = new UniformGridLayout
            {
                MinItemWidth = 155,
                MinItemHeight = 87,
                MinRowSpacing = 8,
                MinColumnSpacing = 8,
                ItemsStretch = UniformGridLayoutItemsStretch.Fill
            },
            ItemTemplate = new FuncDataTemplate<WallpaperItem>((_, _) => BuildTile(), true)
        };
        repeater.Bind(ItemsRepeater.ItemsSourceProperty, new Binding(nameof(MainWindowViewModel.WallpaperItems)));

        // One tab stop into the grid; arrow keys move between tiles from there.
        KeyboardNavigation.SetTabNavigation(repeater, KeyboardNavigationMode.Once);

        var host = new Border { Child = repeater };
        host.Bind(IsVisibleProperty, new Binding(nameof(MainWindowViewModel.HasImages)));
        return host;
    }

    private Control BuildTile()
    {
        // A real Button, so it is focusable, activates on Space and Enter, and
        // reports itself to a screen reader. The old three-Border-per-row
        // pseudo-buttons did none of that.
        var button = new Button
        {
            Padding = new Thickness(0),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Stretch,
            Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
        }
            .Dyn(BackgroundProperty, "ControlAltFillColorSecondaryBrush")
            .Dyn(BorderBrushProperty, "ControlStrokeColorDefaultBrush");

        button.Click += (sender, _) =>
        {
            if (sender is Button { DataContext: WallpaperItem item })
            {
                ViewModel.CycleCategory(item);
                UpdateTileVisuals(sender as Button);
            }
        };

        button.DataContextChanged += (sender, _) => UpdateTileVisuals(sender as Button);

        var layers = new Panel();

        // Only the image is desaturated when ignored, so the badge and the
        // filename keep full contrast.
        var image = new Image { Stretch = Stretch.UniformToFill, Name = "PART_Image" };
        image.Bind(Image.SourceProperty, new Binding(nameof(WallpaperItem.FullPath))
        {
            Converter = ThumbnailCache.Instance
        });
        layers.Children.Add(image);

        layers.Children.Add(BuildBadge());
        layers.Children.Add(BuildWarningChip());
        layers.Children.Add(BuildFilenameBar());

        button.Content = layers;
        return button;
    }

    private static Control BuildBadge()
    {
        var badge = new Border
        {
            Name = "PART_Badge",
            Height = 20,
            Padding = new Thickness(7, 0),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(6)
        };

        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center
        };

        content.Children.Add(new Panel { Name = "PART_BadgeGlyph", Width = 11, Height = 11 });

        // Colour is never the only signal: the badge carries the word too.
        content.Children.Add(new TextBlock
        {
            Name = "PART_BadgeText",
            FontSize = 11,
            LineHeight = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = VerticalAlignment.Center
        });

        badge.Child = content;
        return badge;
    }

    private static Control BuildWarningChip()
    {
        var chip = new Border
        {
            Name = "PART_Warning",
            Width = 20,
            Height = 20,
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(6),
            IsVisible = false,
            Background = new SolidColorBrush(Color.FromArgb(0xEB, 0xC4, 0x2B, 0x1C))
        };

        ToolTip.SetTip(chip, "No preview available — the system has no codec for this format.");

        var glyph = Icons.Create(Icons.Warning, 11, Brushes.White);
        chip.Child = glyph;
        return chip;
    }

    private static Control BuildFilenameBar()
    {
        var bar = new Border
        {
            Height = 22,
            VerticalAlignment = VerticalAlignment.Bottom,
            Padding = new Thickness(7, 0),
            Background = new SolidColorBrush(Color.FromArgb(0x8C, 0, 0, 0))
        };

        var text = new TextBlock
        {
            FontSize = 11,
            LineHeight = 14,
            Foreground = Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center
        };
        text.Bind(TextBlock.TextProperty, new Binding(nameof(WallpaperItem.FileName)));

        bar.Child = text;
        return bar;
    }

    /// <summary>
    /// Applies the badge, desaturation and accessible name for the tile's current
    /// category.
    /// </summary>
    private static void UpdateTileVisuals(Button? button)
    {
        if (button?.DataContext is not WallpaperItem item || button.Content is not Panel layers)
        {
            return;
        }

        var badge = layers.FindDescendant<Border>("PART_Badge");
        var badgeText = layers.FindDescendant<TextBlock>("PART_BadgeText");
        var badgeGlyph = layers.FindDescendant<Panel>("PART_BadgeGlyph");
        var image = layers.FindDescendant<Image>("PART_Image");

        var (word, icon, fill) = item.Category switch
        {
            WallpaperCategory.Day => ("Day", Icons.Sun, Resolve("AccentFillColorDefaultBrush")),
            WallpaperCategory.Night => ("Night", Icons.Moon, (IBrush)new SolidColorBrush(Color.FromArgb(0xB8, 0, 0, 0))),
            _ => ("Ignore", Icons.Block, new SolidColorBrush(Color.FromArgb(0x9E, 0, 0, 0)))
        };

        if (badge is not null)
        {
            badge.Background = fill;
        }

        if (badgeText is not null)
        {
            badgeText.Text = word;
        }

        if (badgeGlyph is not null)
        {
            badgeGlyph.Children.Clear();
            badgeGlyph.Children.Add(Icons.Create(icon, 11, Brushes.White));
        }

        if (image is not null)
        {
            var ignored = item.Category == WallpaperCategory.Ignore;
            image.Opacity = ignored ? 0.5 : 1;
            image.Effect = null;
        }

        AutomationProperties.SetName(button,
            $"{item.FileName}, currently {word.ToLowerInvariant()}. Activate to change.");
    }

    private static IBrush? Resolve(string key) =>
        Application.Current?.TryFindResource(key, Application.Current.ActualThemeVariant, out var value) == true
            ? value as IBrush
            : null;

    // ---- Empty state -------------------------------------------------------

    private Control BuildEmptyState()
    {
        var card = new Border
        {
            Padding = new Thickness(24, 52),
            CornerRadius = new CornerRadius(4),
            BorderThickness = new Thickness(1)
        }
            .Dyn(BackgroundProperty, "CardBackgroundFillColorDefaultBrush")
            .Dyn(BorderBrushProperty, "ControlStrokeColorDefaultBrush");

        var stack = new StackPanel
        {
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Center
        };

        var glyph = Icons.Create(Icons.Image, 32);
        if (glyph is PathShape path)
        {
            path.Dyn(Shape.StrokeProperty, "TextFillColorSecondaryBrush");
        }
        stack.Children.Add(glyph);

        stack.Children.Add(new TextBlock
        {
            Text = "No images yet",
            FontSize = 14,
            LineHeight = 20,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        }.Dyn(ForegroundProperty, "TextFillColorPrimaryBrush"));

        stack.Children.Add(new TextBlock
        {
            Text = "Choose a folder in Settings and the schedule starts straight away. "
                 + "Subfolders are scanned, and files named day or night are tagged for you.",
            FontSize = 12,
            LineHeight = 16,
            MaxWidth = 400,
            TextAlignment = TextAlignment.Center,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush"));

        var cta = Ui.AccentButton("Choose folder", async () =>
        {
            ViewModel.NavigateToSource();
            await _window.BrowseFolderAsync();
        });
        cta.HorizontalAlignment = HorizontalAlignment.Center;
        cta.Margin = new Thickness(0, 6, 0, 0);
        stack.Children.Add(cta);

        card.Child = stack;
        card.Bind(IsVisibleProperty, new Binding(nameof(MainWindowViewModel.HasImages))
        {
            Converter = new FuncValueConverter<bool, bool>(has => !has)
        });
        return card;
    }
}



