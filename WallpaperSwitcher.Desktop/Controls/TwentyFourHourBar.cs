using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using WallpaperSwitcher.Desktop.Theming;

using PathShape = Avalonia.Controls.Shapes.Path;

namespace WallpaperSwitcher.Desktop.Controls;

/// <summary>
/// A 24-hour track showing when day and night begin, with two draggable handles.
/// </summary>
/// <remarks>
/// Replaces the two time text boxes. A night window that crosses midnight was the
/// confusing case those boxes made worst, and it also made three validation
/// errors possible ("must look like 06:00", "cannot be the same time") that this
/// control makes structurally impossible: the handles snap to 15 minutes and
/// cannot come within an hour of each other.
///
/// Because there is no text entry left, the handles must be fully keyboard
/// operable — arrows, Page keys and Home/End all move them.
/// </remarks>
public sealed class TwentyFourHourBar : TemplatedControl
{
    private const int MinutesPerDay = 24 * 60;
    private const int SnapMinutes = 15;
    private const int MinimumGapMinutes = 60;
    private const double TrackHeight = 38;
    private const double ThumbWidth = 14;

    public static readonly StyledProperty<TimeSpan> DayStartProperty =
        AvaloniaProperty.Register<TwentyFourHourBar, TimeSpan>(nameof(DayStart), TimeSpan.FromHours(6),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<TimeSpan> NightStartProperty =
        AvaloniaProperty.Register<TwentyFourHourBar, TimeSpan>(nameof(NightStart), TimeSpan.FromHours(18),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    private Canvas? _track;
    private Rectangle? _leadingNight;
    private Border? _dayBand;
    private Border? _trailingNight;
    private Thumb? _dayThumb;
    private Thumb? _nightThumb;
    private Canvas? _overlay;
    private StackPanel? _caret;
    private Border? _pill;
    private TextBlock? _pillText;
    private TextBlock? _dayRange;
    private TextBlock? _nightRange;
    private DispatcherTimer? _nowTimer;
    private TimeSpan _now = DateTime.Now.TimeOfDay;

    static TwentyFourHourBar()
    {
        FocusableProperty.OverrideDefaultValue<TwentyFourHourBar>(false);
        DayStartProperty.Changed.AddClassHandler<TwentyFourHourBar>((bar, _) => bar.UpdateVisual());
        NightStartProperty.Changed.AddClassHandler<TwentyFourHourBar>((bar, _) => bar.UpdateVisual());
    }

    public TwentyFourHourBar()
    {
        Template = BuildTemplate();
    }

    public TimeSpan DayStart
    {
        get => GetValue(DayStartProperty);
        set => SetValue(DayStartProperty, value);
    }

    public TimeSpan NightStart
    {
        get => GetValue(NightStartProperty);
        set => SetValue(NightStartProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _track = e.NameScope.Find<Canvas>("PART_Track");
        _leadingNight = e.NameScope.Find<Rectangle>("PART_LeadingNight");
        _dayBand = e.NameScope.Find<Border>("PART_DayBand");
        _trailingNight = e.NameScope.Find<Border>("PART_TrailingNight");
        _dayThumb = e.NameScope.Find<Thumb>("PART_DayThumb");
        _nightThumb = e.NameScope.Find<Thumb>("PART_NightThumb");
        _overlay = e.NameScope.Find<Canvas>("PART_Overlay");
        _caret = e.NameScope.Find<StackPanel>("PART_Caret");
        _pill = e.NameScope.Find<Border>("PART_Pill");
        _pillText = e.NameScope.Find<TextBlock>("PART_PillText");
        _dayRange = e.NameScope.Find<TextBlock>("PART_DayRange");
        _nightRange = e.NameScope.Find<TextBlock>("PART_NightRange");

        HookThumb(_dayThumb, isDay: true, "Day starts");
        HookThumb(_nightThumb, isDay: false, "Night starts");

        if (_track is not null)
        {
            _track.SizeChanged += (_, _) => UpdateVisual();
            _track.PointerPressed += OnTrackPressed;
        }

        UpdateVisual();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        // Purely cosmetic: keeps the "now" caret honest without involving the
        // wallpaper scheduler, which owns its own timing.
        _nowTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _nowTimer.Tick += (_, _) =>
        {
            _now = DateTime.Now.TimeOfDay;
            UpdateVisual();
        };
        _nowTimer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _nowTimer?.Stop();
        _nowTimer = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void HookThumb(Thumb? thumb, bool isDay, string label)
    {
        if (thumb is null)
        {
            return;
        }

        AutomationProperties.SetName(thumb, label);
        thumb.DragDelta += (_, args) => OnThumbDrag(isDay, args.Vector.X);
        thumb.DragStarted += (_, _) => ShowPill(isDay, visible: true);
        thumb.DragCompleted += (_, _) => ShowPill(isDay, visible: false);
        thumb.KeyDown += (_, args) => OnThumbKey(isDay, args);
        thumb.GotFocus += (_, _) => ShowPill(isDay, visible: true);
        thumb.LostFocus += (_, _) => ShowPill(isDay, visible: false);
    }

    private void OnThumbDrag(bool isDay, double deltaX)
    {
        if (_track is null || _track.Bounds.Width <= 0)
        {
            return;
        }

        var current = isDay ? DayStart : NightStart;
        var minutes = (int)Math.Round(current.TotalMinutes + (deltaX / _track.Bounds.Width * MinutesPerDay));
        Commit(isDay, minutes);
        ShowPill(isDay, visible: true);
    }

    private void OnThumbKey(bool isDay, KeyEventArgs args)
    {
        var current = (int)Math.Round((isDay ? DayStart : NightStart).TotalMinutes);
        var handled = true;

        switch (args.Key)
        {
            case Key.Left or Key.Down:
                Commit(isDay, current - SnapMinutes);
                break;
            case Key.Right or Key.Up:
                Commit(isDay, current + SnapMinutes);
                break;
            case Key.PageDown:
                Commit(isDay, current - (SnapMinutes * 4));
                break;
            case Key.PageUp:
                Commit(isDay, current + (SnapMinutes * 4));
                break;
            case Key.Home:
                // Clamp against the other handle rather than to midnight, so Home
                // and End mean "as far as this handle is allowed to go".
                Commit(isDay, isDay ? 0 : (int)DayStart.TotalMinutes + MinimumGapMinutes);
                break;
            case Key.End:
                Commit(isDay, isDay ? (int)NightStart.TotalMinutes - MinimumGapMinutes : MinutesPerDay);
                break;
            default:
                handled = false;
                break;
        }

        if (handled)
        {
            ShowPill(isDay, visible: true);
            args.Handled = true;
        }
    }

    private void OnTrackPressed(object? sender, PointerPressedEventArgs args)
    {
        if (_track is null || _track.Bounds.Width <= 0)
        {
            return;
        }

        var minutes = (int)Math.Round(args.GetPosition(_track).X / _track.Bounds.Width * MinutesPerDay);

        // Move whichever handle is nearer, so clicking the track is useful rather
        // than ambiguous.
        var toDay = Math.Abs(minutes - DayStart.TotalMinutes);
        var toNight = Math.Abs(minutes - NightStart.TotalMinutes);
        var isDay = toDay <= toNight;

        Commit(isDay, minutes);
        (isDay ? _dayThumb : _nightThumb)?.Focus();
    }

    private void Commit(bool isDay, int minutes)
    {
        var snapped = (int)Math.Round(minutes / (double)SnapMinutes) * SnapMinutes;

        if (isDay)
        {
            var limit = (int)NightStart.TotalMinutes - MinimumGapMinutes;
            DayStart = TimeSpan.FromMinutes(Math.Clamp(snapped, 0, Math.Max(0, limit)));
        }
        else
        {
            var limit = (int)DayStart.TotalMinutes + MinimumGapMinutes;
            NightStart = TimeSpan.FromMinutes(Math.Clamp(snapped, Math.Min(MinutesPerDay, limit), MinutesPerDay));
        }
    }

    private void ShowPill(bool isDay, bool visible)
    {
        if (_pill is null || _pillText is null || _overlay is null)
        {
            return;
        }

        _pill.IsVisible = visible;
        if (!visible)
        {
            return;
        }

        var value = isDay ? DayStart : NightStart;
        _pillText.Text = Format(value);

        var width = _track?.Bounds.Width ?? 0;
        Canvas.SetLeft(_pill, Math.Max(0, (value.TotalMinutes / MinutesPerDay * width) - 22));
    }

    private void UpdateVisual()
    {
        if (_track is null)
        {
            return;
        }

        var width = _track.Bounds.Width;
        if (width <= 0)
        {
            return;
        }

        double X(TimeSpan value) => value.TotalMinutes / MinutesPerDay * width;

        var dayX = X(DayStart);
        var nightX = X(NightStart);

        if (_leadingNight is not null)
        {
            _leadingNight.Width = Math.Max(0, dayX);
            _leadingNight.Height = TrackHeight;
        }

        if (_dayBand is not null)
        {
            Canvas.SetLeft(_dayBand, dayX);
            _dayBand.Width = Math.Max(0, nightX - dayX);
            _dayBand.Height = TrackHeight;
        }

        if (_trailingNight is not null)
        {
            Canvas.SetLeft(_trailingNight, nightX);
            _trailingNight.Width = Math.Max(0, width - nightX);
            _trailingNight.Height = TrackHeight;
        }

        if (_dayThumb is not null)
        {
            Canvas.SetLeft(_dayThumb, dayX - (ThumbWidth / 2));
            AutomationProperties.SetHelpText(_dayThumb, Format(DayStart));
            ToolTip.SetTip(_dayThumb, $"Day starts at {Format(DayStart)}. Drag, or use the arrow keys.");
        }

        if (_nightThumb is not null)
        {
            Canvas.SetLeft(_nightThumb, nightX - (ThumbWidth / 2));
            AutomationProperties.SetHelpText(_nightThumb, Format(NightStart));
            ToolTip.SetTip(_nightThumb, $"Night starts at {Format(NightStart)}. Drag, or use the arrow keys.");
        }

        if (_dayRange is not null)
        {
            _dayRange.Text = $"{Format(DayStart)} – {Format(NightStart)}";
        }

        if (_nightRange is not null)
        {
            _nightRange.Text = $"{Format(NightStart)} – {Format(DayStart)}";
        }

        if (_caret is not null)
        {
            Canvas.SetLeft(_caret, X(_now) - 12);
        }
    }

    private static string Format(TimeSpan value) => $"{(int)value.TotalHours:00}:{value.Minutes:00}";

    private FuncControlTemplate BuildTemplate() => new((_, scope) =>
    {
        var root = new Grid
        {
            RowDefinitions = new RowDefinitions("19,38,Auto")
        };

        // Caret layer. Sits above the track so it never crosses a band label.
        var overlay = new Canvas { Height = 19, ClipToBounds = false }.Named(scope, "PART_Overlay");

        var caret = new StackPanel { Width = 24 }.Named(scope, "PART_Caret");
        caret.Children.Add(new TextBlock
        {
            Text = "now",
            FontSize = 10,
            LineHeight = 12,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        }.Dyn(ForegroundProperty, "TextFillColorPrimaryBrush"));
        caret.Children.Add(new PathShape
        {
            Data = Geometry.Parse("M0,0 L8,0 L4,5 Z"),
            Width = 8,
            Height = 5,
            HorizontalAlignment = HorizontalAlignment.Center
        }.Dyn(Shape.FillProperty, "TextFillColorPrimaryBrush"));
        overlay.Children.Add(caret);

        var pill = new Border
        {
            IsVisible = false,
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(7, 2)
        }.Dyn(BackgroundProperty, "AccentFillColorDefaultBrush").Named(scope, "PART_Pill");

        pill.Child = new TextBlock
        {
            FontSize = 11,
            LineHeight = 15,
            FontWeight = FontWeight.SemiBold
        }.Dyn(ForegroundProperty, "TextOnAccentFillColorPrimaryBrush").Named(scope, "PART_PillText");
        overlay.Children.Add(pill);

        Grid.SetRow(overlay, 0);
        root.Children.Add(overlay);

        // Track.
        var track = new Canvas { Height = TrackHeight, ClipToBounds = false }.Named(scope, "PART_Track");

        var leadingNight = new Rectangle { Height = TrackHeight }
            .Dyn(Shape.FillProperty, "ScheduleNightFillBrush")
            .Named(scope, "PART_LeadingNight");
        Canvas.SetLeft(leadingNight, 0);
        track.Children.Add(leadingNight);

        track.Children.Add(BuildBand(scope, "PART_DayBand", "PART_DayRange", Icons.Sun,
            "AccentFillColorDefaultBrush", "TextOnAccentFillColorPrimaryBrush"));
        track.Children.Add(BuildBand(scope, "PART_TrailingNight", "PART_NightRange", Icons.Moon,
            "ScheduleNightFillBrush", "ScheduleNightForegroundBrush"));

        track.Children.Add(BuildThumb(scope, "PART_DayThumb"));
        track.Children.Add(BuildThumb(scope, "PART_NightThumb"));

        var trackFrame = new Border
        {
            Height = TrackHeight,
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            BorderThickness = new Thickness(1),
            Child = track
        }
            .Dyn(BackgroundProperty, "ControlAltFillColorSecondaryBrush")
            .Dyn(BorderBrushProperty, "ControlStrokeColorDefaultBrush");

        Grid.SetRow(trackFrame, 1);
        root.Children.Add(trackFrame);

        // Ticks.
        var ticks = new Grid
        {
            Margin = new Thickness(0, 5, 0, 0),
            ColumnDefinitions = new ColumnDefinitions("*,*,*,*,*,*,*,*,*")
        };
        var labels = new[] { "00", "03", "06", "09", "12", "15", "18", "21", "24" };
        for (var i = 0; i < labels.Length; i++)
        {
            var tick = new TextBlock
            {
                Text = labels[i],
                FontSize = 11,
                LineHeight = 14,
                HorizontalAlignment = i == 0 ? HorizontalAlignment.Left
                    : i == labels.Length - 1 ? HorizontalAlignment.Right
                    : HorizontalAlignment.Center
            }.Dyn(ForegroundProperty, "TextFillColorSecondaryBrush");

            Grid.SetColumn(tick, i);
            ticks.Children.Add(tick);
        }

        Grid.SetRow(ticks, 2);
        root.Children.Add(ticks);

        return root;
    });

    private static Border BuildBand(INameScope scope, string bandName, string textName, Icons.Icon icon,
        string fillKey, string foregroundKey)
    {
        var content = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            Margin = new Thickness(10, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        var glyph = Icons.Create(icon, 13);
        if (glyph is PathShape shape)
        {
            shape.Dyn(icon.Stroked ? Shape.StrokeProperty : Shape.FillProperty, foregroundKey);
        }

        content.Children.Add(glyph);
        content.Children.Add(new TextBlock
        {
            FontSize = 12,
            LineHeight = 16,
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center
        }.Dyn(ForegroundProperty, foregroundKey).Named(scope, textName));

        return new Border
        {
            Height = TrackHeight,
            ClipToBounds = true,
            Child = content
        }.Dyn(BackgroundProperty, fillKey).Named(scope, bandName);
    }

    private static Thumb BuildThumb(INameScope scope, string name)
    {
        var thumb = new Thumb
        {
            Width = ThumbWidth,
            Height = TrackHeight + 6,
            Focusable = true,
            Cursor = new Cursor(StandardCursorType.SizeWestEast),
            Template = new FuncControlTemplate((_, _) =>
            {
                var grip = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 2,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                for (var i = 0; i < 2; i++)
                {
                    grip.Children.Add(new Rectangle { Width = 1, Height = 12 }
                        .Dyn(Shape.FillProperty, "TextFillColorSecondaryBrush"));
                }

                return new Border
                {
                    CornerRadius = new CornerRadius(4),
                    BorderThickness = new Thickness(1),
                    Child = grip
                }
                    .Dyn(BackgroundProperty, "CardBackgroundFillColorSecondaryBrush")
                    .Dyn(BorderBrushProperty, "TextFillColorPrimaryBrush");
            })
        }.Named(scope, name);

        Canvas.SetTop(thumb, -3);
        return thumb;
    }
}
