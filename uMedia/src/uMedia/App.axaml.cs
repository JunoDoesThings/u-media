using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using uMedia.Interfaces;
using uMedia.Services;
using uMedia.Views;

namespace uMedia;

public partial class App : Application
{
    private IAppSettingsProvider? settings;
    private IWidgetLayoutProvider? layout;
    private MediaSessionService? media;

    private WidgetWindow? widget;
    private SettingsWindow? settingsWindow;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            settings = new AppSettingsProvider();
            layout = new WidgetLayoutProvider();
            media = new MediaSessionService();

            // A portable app gets moved around; make sure the Run entry still
            // points at where the executable actually is before anything else.
            if (OperatingSystem.IsWindows())
                new StartupService().Refresh(settings.Get().RunOnStartup);

            widget = new WidgetWindow(settings, layout, media);

            // Closing the widget should not leave a headless process behind.
            desktop.MainWindow = widget;
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Exit += (_, _) => media?.Dispose();

            _ = media.StartAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Opens the settings window, or brings the existing one forward. Shared by
    /// the widget's context menu and the tray icon so only one can ever be open.
    /// </summary>
    public void ShowSettings()
    {
        if (settings is null || layout is null || media is null) return;

        if (settingsWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;

            existing.Activate();

            return;
        }

        settingsWindow = new SettingsWindow(layout, settings, media);
        settingsWindow.Closed += (_, _) => settingsWindow = null;
        settingsWindow.Show();
    }

    private void OnTrayClicked(object? sender, EventArgs e) => ShowSettings();

    private void OnTraySettingsClick(object? sender, EventArgs e) => ShowSettings();

    /// <summary>
    /// A widget pinned behind other windows can be hard to find; this raises it
    /// and puts it back at its saved position.
    /// </summary>
    private void OnTrayShowClick(object? sender, EventArgs e)
    {
        if (widget is null) return;

        widget.Show();
        widget.Activate();

        // Avalonia has no BringToFront; flipping Topmost is the usual way to
        // pull a window out from under everything else.
        widget.Topmost = true;
        widget.Topmost = false;
    }

    private void OnTrayQuitClick(object? sender, EventArgs e)
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }
}
