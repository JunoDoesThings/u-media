using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using uMedia.Interfaces;
using uMedia.Models;
using uMedia.Services;
using uMedia.ViewModels;

namespace uMedia.Views;

public partial class WidgetWindow : Window
{
    private readonly IAppSettingsProvider settingsProvider;
    private readonly IWidgetLayoutProvider layoutProvider;

    /// <summary>Parameterless constructor for the XAML previewer only.</summary>
    public WidgetWindow() : this(
        new AppSettingsProvider(),
        new WidgetLayoutProvider(),
        new MediaSessionService())
    {
    }

    public WidgetWindow(
        IAppSettingsProvider settingsProvider,
        IWidgetLayoutProvider layoutProvider,
        IMediaService media)
    {
        this.settingsProvider = settingsProvider;
        this.layoutProvider = layoutProvider;

        InitializeComponent();

        DataContext = new MediaViewModel(media, layoutProvider);

        settingsProvider.DataChanged += _ => Apply();
        layoutProvider.DataChanged += _ => Apply();

        PointerPressed += OnPointerPressed;
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        Apply();
        EnsureOnScreen();
    }

    /// <summary>
    /// Pulls the widget back into view if the saved position is off-screen.
    /// This matters most when starting with Windows: at login a second monitor
    /// may not be attached yet, and a widget restored to a screen that is not
    /// there is invisible with no way to get it back.
    ///
    /// Deliberately does not save the clamped position — the file keeps where
    /// the user actually put it, so the widget returns there once the display
    /// comes back.
    /// </summary>
    private void EnsureOnScreen()
    {
        var screen = Screens.ScreenFromPoint(Position)
                     ?? Screens.ScreenFromWindow(this)
                     ?? Screens.Primary;

        if (screen is null) return;

        var area = screen.WorkingArea;
        var width = (int)(Width * RenderScaling);
        var height = (int)(Height * RenderScaling);

        var x = Math.Clamp(Position.X, area.X, Math.Max(area.X, area.X + area.Width - width));
        var y = Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Y + area.Height - height));

        if (x != Position.X || y != Position.Y) Position = new PixelPoint(x, y);
    }

    // -- appearance and geometry -------------------------------------------

    /// <summary>
    /// Pushes both settings files into the window: size from the grid, colours
    /// from the theme, backdrop from DWM.
    /// </summary>
    private void Apply()
    {
        var settings = settingsProvider.Get();
        var layout = layoutProvider.Get();

        var grid = settings.Layout;

        // Size is whatever the user last dragged it to. The exact pixel size is
        // authoritative; the cell count is the fallback for a layout.json
        // written before it existed, or after a size reset.
        MinWidth = ToPixels(1, grid);
        MinHeight = ToPixels(1, grid);

        Width = layout.PixelWidth > 0
            ? layout.PixelWidth
            : ToPixels(Math.Max(1, layout.Width), grid);

        Height = layout.PixelHeight > 0
            ? layout.PixelHeight
            : ToPixels(Math.Max(1, layout.Height), grid);

        FontFamily = string.IsNullOrWhiteSpace(settings.Theme.Font)
            ? FontFamily.Default
            : new FontFamily(settings.Theme.Font);

        // Only move the window if the stored position differs from where it
        // already is. Apply runs on every settings save, and reasserting the
        // position unconditionally is what dragged the widget back to 64,64.
        var target = new PixelPoint(layout.X, layout.Y);

        if (Position != target) Position = target;

        RequestedThemeVariant = settings.Theme.Variant switch
        {
            Models.ThemeVariant.Light => Avalonia.Styling.ThemeVariant.Light,
            Models.ThemeVariant.Dark => Avalonia.Styling.ThemeVariant.Dark,
            _ => Avalonia.Styling.ThemeVariant.Default
        };

        if (this.FindControl<Border>("Root") is { } root)
        {
            root.CornerRadius = new CornerRadius(grid.Radius);
            root.Background = TintBrush(settings.Theme, IsDark(settings.Theme));
        }

        ApplyBackdrop(settings);
        ApplyWindowStyles(settings);
    }

    /// <summary>
    /// The card drawn on top of the backdrop. Mica alone is too low-contrast to
    /// read text against, so a tint is layered over it.
    /// </summary>
    private bool IsDark(Theme theme) => theme.Variant switch
    {
        Models.ThemeVariant.Light => false,
        Models.ThemeVariant.Dark => true,
        _ => ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark
    };

    private static IBrush TintBrush(Theme theme, bool dark)
    {
        if (theme.Backdrop == Backdrop.Solid)
            return dark
                ? new SolidColorBrush(Color.FromRgb(0x1F, 0x1F, 0x1F))
                : new SolidColorBrush(Color.FromRgb(0xF3, 0xF3, 0xF3));

        var value = dark ? (byte)0x00 : (byte)0xFF;
        var alpha = (byte)Math.Clamp(theme.Tint * 255, 0, 255);

        return new SolidColorBrush(Color.FromArgb(alpha, value, value, value));
    }

    private void ApplyBackdrop(AppSettings settings)
    {
        TransparencyLevelHint = settings.Theme.Backdrop switch
        {
            Backdrop.Solid => new[] { WindowTransparencyLevel.None },
            Backdrop.Acrylic => new[] { WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent },
            _ => new[] { WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Transparent }
        };

        if (!OperatingSystem.IsWindows()) return;

        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;

        Win32.SetBackdrop(handle, settings.Theme.Backdrop switch
        {
            Backdrop.Mica => Win32.BackdropType.MainWindow,
            Backdrop.MicaAlt => Win32.BackdropType.TabbedWindow,
            Backdrop.Acrylic => Win32.BackdropType.TransientWindow,
            _ => Win32.BackdropType.None
        });

        // DWM rounds the frame; the Border inside rounds the content. Both are
        // needed or the corners show a square sliver of backdrop.
        Win32.SetCorners(handle, settings.Layout.Radius >= 8
            ? Win32.CornerPreference.Round
            : Win32.CornerPreference.RoundSmall);
    }

    private void ApplyWindowStyles(AppSettings settings)
    {
        Topmost = settings.Topmost;

        if (!OperatingSystem.IsWindows()) return;

        var handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return;

        Win32.SetToolWindow(handle, settings.ToolWindow);
    }

    // -- resizing ----------------------------------------------------------

    private static double ToPixels(int cells, Layout grid) =>
        cells * grid.CellSize + (cells - 1) * grid.Margin;

    private static int ToCells(double pixels, Layout grid)
    {
        var step = grid.CellSize + grid.Margin;
        if (step <= 0) return 1;

        return Math.Max(1, (int)Math.Round((pixels + grid.Margin) / step));
    }

    /// <summary>
    /// BeginResizeDrag hands control to the window manager, which runs a modal
    /// loop and only returns once the drag is over — so the snap and the save
    /// belong on the line after it, not on a timer that fires mid-drag.
    /// </summary>
    private void OnGripPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        e.Handled = true;

        BeginResizeDrag(WindowEdge.SouthEast, e);
        SaveSize();
    }

    private void SaveSize()
    {
        var settings = settingsProvider.Get();
        var layout = layoutProvider.Get();
        var grid = settings.Layout;

        var columns = ToCells(Width, grid);
        var rows = ToCells(Height, grid);

        if (grid.SnapToGrid)
        {
            Width = ToPixels(columns, grid);
            Height = ToPixels(rows, grid);
        }

        layoutProvider.Save(layout with
        {
            Width = columns,
            Height = rows,
            PixelWidth = Width,
            PixelHeight = Height
        });
    }

    private void OnResetSizeClick(object? sender, RoutedEventArgs e)
    {
        var layout = layoutProvider.Get();

        layoutProvider.Save(layout with
        {
            Width = layout.Settings.View == MediaView.Compact ? 2 : 4,
            Height = layout.Settings.View == MediaView.Large ? 4 : 2,
            PixelWidth = 0,
            PixelHeight = 0
        });
    }

    // -- dragging ----------------------------------------------------------

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;

        // Let buttons and the seek bar handle their own clicks.
        if (e.Source is Control { Name: not "Root" } source && IsInteractive(source)) return;

        // Blocks until the move loop ends, same as BeginResizeDrag above.
        BeginMoveDrag(e);
        SavePosition();
    }

    private static bool IsInteractive(Control control)
    {
        for (var current = (Control?)control; current is not null; current = current.Parent as Control)
            if (current is Button or Slider or RangeBase or MenuItem)
                return true;

        return false;
    }

    /// <summary>Snaps the widget to the virtual grid and remembers where it landed.</summary>
    private void SavePosition()
    {
        var settings = settingsProvider.Get();
        var layout = layoutProvider.Get();

        var x = Position.X;
        var y = Position.Y;

        if (settings.Layout.SnapToGrid)
        {
            // Width and Height are device independent pixels; Position is not.
            // Without RenderScaling the grid is the wrong size on any display
            // that is not at 100%.
            var step = (int)Math.Round(
                (settings.Layout.CellSize + settings.Layout.Margin) * RenderScaling);

            if (step > 0)
            {
                x = (int)Math.Round((double)x / step) * step;
                y = (int)Math.Round((double)y / step) * step;

                if (Position.X != x || Position.Y != y) Position = new PixelPoint(x, y);
            }
        }

        if (layout.X == x && layout.Y == y) return;

        layoutProvider.Save(layout with { X = x, Y = y });
    }

    // -- context menu ------------------------------------------------------

    private void OnEditClick(object? sender, RoutedEventArgs e) =>
        (Application.Current as App)?.ShowSettings();

    private void OnCompactClick(object? sender, RoutedEventArgs e) => SetView(MediaView.Compact);
    private void OnWideClick(object? sender, RoutedEventArgs e) => SetView(MediaView.Wide);
    private void OnLargeClick(object? sender, RoutedEventArgs e) => SetView(MediaView.Large);

    private void SetView(MediaView view)
    {
        var layout = layoutProvider.Get();

        layoutProvider.Save(layout with
        {
            Settings = layout.Settings with { View = view },
            Width = view == MediaView.Compact ? 2 : 4,
            Height = view == MediaView.Large ? 4 : 2,
            PixelWidth = 0,
            PixelHeight = 0
        });
    }

    private void OnMicaClick(object? sender, RoutedEventArgs e) => SetBackdrop(Backdrop.Mica);
    private void OnMicaAltClick(object? sender, RoutedEventArgs e) => SetBackdrop(Backdrop.MicaAlt);
    private void OnAcrylicClick(object? sender, RoutedEventArgs e) => SetBackdrop(Backdrop.Acrylic);
    private void OnSolidClick(object? sender, RoutedEventArgs e) => SetBackdrop(Backdrop.Solid);

    private void SetBackdrop(Backdrop backdrop)
    {
        var settings = settingsProvider.Get();

        settingsProvider.Save(settings with
        {
            Theme = settings.Theme with { Backdrop = backdrop }
        });
    }

    private void OnQuitClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current?.ApplicationLifetime is
            Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
