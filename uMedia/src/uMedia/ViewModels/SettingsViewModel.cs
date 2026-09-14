using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Media;
using uMedia.Interfaces;
using uMedia.Models;
using uMedia.Services;

namespace uMedia.ViewModels;

/// <summary>
/// One row in the app list: an app user model id reported by Windows, or one
/// the user typed in by hand.
/// </summary>
public class AppEntry : ViewModelBase
{
    private bool isChecked;

    public AppEntry(string id, bool isChecked, bool isDetected)
    {
        Id = id;
        DisplayName = AppId.ToDisplayName(id);
        IsDetected = isDetected;
        this.isChecked = isChecked;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public bool IsDetected { get; }

    public bool IsChecked
    {
        get => isChecked;
        set => Set(ref isChecked, value);
    }
}

public class SettingsViewModel : ViewModelBase
{
    private readonly IWidgetLayoutProvider layoutProvider;
    private readonly IAppSettingsProvider settingsProvider;
    private readonly IMediaService media;
    private readonly StartupService startup = new();

    private string newAppId = "";

    public SettingsViewModel(
        IWidgetLayoutProvider layoutProvider,
        IAppSettingsProvider settingsProvider,
        IMediaService media)
    {
        this.layoutProvider = layoutProvider;
        this.settingsProvider = settingsProvider;
        this.media = media;

        var layout = layoutProvider.Get();
        var settings = settingsProvider.Get();

        view = layout.Settings.View;
        showCover = layout.Settings.ShowCover;
        showProgress = layout.Settings.ShowProgress;
        showSource = layout.Settings.ShowSource;
        scrollTitle = layout.Settings.ScrollTitle;
        filterMode = layout.Settings.FilterMode;

        variant = settings.Theme.Variant;
        font = string.IsNullOrWhiteSpace(settings.Theme.Font) ? DefaultFont : settings.Theme.Font;

        // A font saved on another machine may not be installed here; keep it in
        // the list anyway so opening settings does not silently reset it.
        if (!Fonts.Contains(font)) Fonts.Add(font);
        backdrop = settings.Theme.Backdrop;
        tint = settings.Theme.Tint;
        cellSize = settings.Layout.CellSize;
        margin = settings.Layout.Margin;
        radius = settings.Layout.Radius;
        snapToGrid = settings.Layout.SnapToGrid;
        topmost = settings.Topmost;
        toolWindow = settings.ToolWindow;

        // Read the real registry state rather than the settings file: the user
        // may have removed the entry from Task Manager's Startup tab, and the
        // checkbox should agree with Windows, not with our own JSON.
        runOnStartup = OperatingSystem.IsWindows() && startup.IsEnabled();

        AddApp = new Command(OnAddApp, () => NewAppId.Trim().Length > 0);
        Apply = new Command(OnApply);

        media.AppsChanged += OnAppsChanged;

        BuildApps();
    }

    public ObservableCollection<AppEntry> Apps { get; } = new();

    public Command AddApp { get; }
    public Command Apply { get; }

    /// <summary>Label for "no font set", which stores as an empty string.</summary>
    public const string DefaultFont = "System default";

    public ObservableCollection<string> Fonts { get; } = BuildFonts();

    private static ObservableCollection<string> BuildFonts()
    {
        var names = new List<string> { DefaultFont };

        try
        {
            names.AddRange(FontManager.Current.SystemFonts
                .Select(x => x.Name)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase));
        }
        catch (Exception e)
        {
            Services.Log.Error("Could not enumerate system fonts", e);
        }

        return new ObservableCollection<string>(names);
    }

    public IReadOnlyList<MediaView> Views { get; } = Enum.GetValues<MediaView>();
    public IReadOnlyList<FilterMode> FilterModes { get; } = Enum.GetValues<FilterMode>();
    public IReadOnlyList<ThemeVariant> Variants { get; } = Enum.GetValues<ThemeVariant>();
    public IReadOnlyList<Backdrop> Backdrops { get; } = Enum.GetValues<Backdrop>();

    // -- widget ------------------------------------------------------------

    private MediaView view;
    public MediaView View
    {
        get => view;
        set { if (Set(ref view, value)) OnApply(); }
    }

    private bool showCover;
    public bool ShowCover
    {
        get => showCover;
        set { if (Set(ref showCover, value)) OnApply(); }
    }

    private bool showProgress;
    public bool ShowProgress
    {
        get => showProgress;
        set { if (Set(ref showProgress, value)) OnApply(); }
    }

    private bool showSource;
    public bool ShowSource
    {
        get => showSource;
        set { if (Set(ref showSource, value)) OnApply(); }
    }

    private bool scrollTitle;
    public bool ScrollTitle
    {
        get => scrollTitle;
        set { if (Set(ref scrollTitle, value)) OnApply(); }
    }

    // -- apps --------------------------------------------------------------

    private FilterMode filterMode;
    public FilterMode FilterMode
    {
        get => filterMode;
        set
        {
            if (!Set(ref filterMode, value)) return;

            OnPropertyChanged(nameof(IsFilterEnabled), nameof(FilterHint));
            OnApply();
        }
    }

    public bool IsFilterEnabled => FilterMode != FilterMode.Off;

    public string FilterHint => FilterMode switch
    {
        FilterMode.Whitelist => "Only the checked apps can drive the widget.",
        FilterMode.Blacklist => "Checked apps are ignored; everything else is followed.",
        _ => "Every app Windows reports can drive the widget."
    };

    public string NewAppId
    {
        get => newAppId;
        set
        {
            if (Set(ref newAppId, value)) AddApp.RaiseCanExecuteChanged();
        }
    }

    private void OnAddApp()
    {
        var id = NewAppId.Trim();
        if (id.Length == 0 || Apps.Any(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase)))
            return;

        var entry = new AppEntry(id, true, false);
        entry.PropertyChanged += (_, _) => OnApply();

        Apps.Add(entry);
        NewAppId = "";

        OnApply();
    }

    public void RemoveApp(AppEntry entry)
    {
        Apps.Remove(entry);
        OnApply();
    }

    private void OnAppsChanged(object? sender, EventArgs e) => BuildApps();

    /// <summary>
    /// Merges the apps Windows has reported with the ones already saved, so an
    /// app that is currently closed does not silently drop out of the list.
    /// </summary>
    private void BuildApps()
    {
        var saved = layoutProvider.Get().Settings.Apps;
        var detected = media.KnownApps;

        var ids = detected
            .Concat(saved)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(AppId.ToDisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var existing = Apps.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        Apps.Clear();

        foreach (var id in ids)
        {
            var isChecked = existing.TryGetValue(id, out var previous)
                ? previous.IsChecked
                : saved.Contains(id, StringComparer.OrdinalIgnoreCase);

            var entry = new AppEntry(id, isChecked, detected.Contains(id, StringComparer.OrdinalIgnoreCase));
            entry.PropertyChanged += (_, _) => OnApply();

            Apps.Add(entry);
        }
    }

    // -- appearance --------------------------------------------------------

    private ThemeVariant variant;
    public ThemeVariant Variant
    {
        get => variant;
        set { if (Set(ref variant, value)) OnApply(); }
    }

    private string font = DefaultFont;
    public string Font
    {
        get => font;
        set { if (Set(ref font, value)) OnApply(); }
    }

    private Backdrop backdrop;
    public Backdrop Backdrop
    {
        get => backdrop;
        set { if (Set(ref backdrop, value)) OnApply(); }
    }

    private double tint;
    public double Tint
    {
        get => tint;
        set { if (Set(ref tint, Math.Round(value, 2))) OnApply(); }
    }

    private int cellSize;
    public int CellSize
    {
        get => cellSize;
        set { if (Set(ref cellSize, value)) OnApply(); }
    }

    private int margin;
    public int Margin
    {
        get => margin;
        set { if (Set(ref margin, value)) OnApply(); }
    }

    private int radius;
    public int Radius
    {
        get => radius;
        set { if (Set(ref radius, value)) OnApply(); }
    }

    private bool snapToGrid;
    public bool SnapToGrid
    {
        get => snapToGrid;
        set { if (Set(ref snapToGrid, value)) OnApply(); }
    }

    private bool topmost;
    public bool Topmost
    {
        get => topmost;
        set { if (Set(ref topmost, value)) OnApply(); }
    }

    private bool toolWindow;
    public bool ToolWindow
    {
        get => toolWindow;
        set { if (Set(ref toolWindow, value)) OnApply(); }
    }

    private bool runOnStartup;
    public bool RunOnStartup
    {
        get => runOnStartup;
        set
        {
            if (!Set(ref runOnStartup, value)) return;

            if (OperatingSystem.IsWindows()) startup.SetEnabled(value);

            OnApply();
        }
    }

    /// <summary>
    /// Warns when the running process is the dotnet host rather than a published
    /// widget, because registering that would start the SDK on login, not uMedia.
    /// </summary>
    public bool IsStartupReliable =>
        StartupService.ExecutablePath?.EndsWith("uMedia.exe", StringComparison.OrdinalIgnoreCase) == true;

    // -- persistence -------------------------------------------------------

    /// <summary>
    /// Writes both files. Every change applies immediately: the widget listens
    /// for DataChanged and re-renders, so there is no OK/Cancel to get wrong.
    /// </summary>
    private void OnApply()
    {
        var layout = layoutProvider.Get();

        layoutProvider.Save(layout with
        {
            Settings = new MediaModel
            {
                View = View,
                ShowCover = ShowCover,
                ShowProgress = ShowProgress,
                ShowSource = ShowSource,
                ScrollTitle = ScrollTitle,
                FilterMode = FilterMode,
                Apps = Apps.Where(x => x.IsChecked).Select(x => x.Id).ToList()
            }
        });

        var settings = settingsProvider.Get();

        settingsProvider.Save(settings with
        {
            Theme = new Theme
            {
                Variant = Variant,
                Backdrop = Backdrop,
                Accent = settings.Theme.Accent,
                Font = Font == DefaultFont ? "" : Font,
                Tint = Tint
            },
            Layout = new Layout
            {
                CellSize = CellSize,
                Margin = Margin,
                Radius = Radius,
                SnapToGrid = SnapToGrid
            },
            Topmost = Topmost,
            RunOnStartup = RunOnStartup,
            ToolWindow = ToolWindow
        });
    }
}
