using System;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using uMedia.Interfaces;
using uMedia.Models;
using uMedia.Services;

namespace uMedia.ViewModels;

public class MediaViewModel : ViewModelBase, IDisposable
{
    private readonly IMediaService media;
    private readonly IWidgetLayoutProvider layoutProvider;
    private readonly DispatcherTimer timer;

    private MediaSnapshot? snapshot;
    private bool suppressSeek;

    public MediaViewModel(IMediaService media, IWidgetLayoutProvider layoutProvider)
    {
        this.media = media;
        this.layoutProvider = layoutProvider;

        PlayPause = new Command(() => _ = media.PlayPauseAsync(), () => CanPlayPause);
        Next = new Command(() => _ = media.NextAsync(), () => CanSkipNext);
        Previous = new Command(() => _ = media.PreviousAsync(), () => CanSkipPrevious);

        media.Changed += OnMediaChanged;
        layoutProvider.DataChanged += OnLayoutChanged;

        // Windows reports the timeline only when it changes, so the elapsed
        // time is interpolated locally between updates.
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, OnTick);
        timer.Start();

        ApplyFilter();
    }

    public MediaModel Model => layoutProvider.Get().Settings;

    public Command PlayPause { get; }
    public Command Next { get; }
    public Command Previous { get; }

    // -- track -------------------------------------------------------------

    public bool HasMedia => snapshot is not null;

    public string Title => snapshot?.Title is { Length: > 0 } title ? title : "Nothing playing";

    public string Artist => snapshot?.Artist ?? "";

    public bool HasArtist => !string.IsNullOrWhiteSpace(snapshot?.Artist);

    public string Source => snapshot is null ? "" : AppId.ToDisplayName(snapshot.AppId);

    public bool ShowSource => Model.ShowSource && HasMedia && Source.Length > 0;

    public Bitmap? Cover => Model.ShowCover ? snapshot?.Cover : null;

    public bool HasCover => Cover is not null;

    public bool IsPlaying => snapshot?.IsPlaying == true;

    public bool IsPaused => !IsPlaying;

    public bool CanPlayPause => snapshot?.CanPlayPause == true;
    public bool CanSkipNext => snapshot?.CanSkipNext == true;
    public bool CanSkipPrevious => snapshot?.CanSkipPrevious == true;
    public bool CanSeek => snapshot?.CanSeek == true;

    // -- timeline ----------------------------------------------------------

    public bool ShowProgress => Model.ShowProgress && HasMedia && Duration > 0;

    public double Duration => snapshot?.Duration.TotalSeconds ?? 0;

    private double position;

    /// <summary>Bound two-way to the seek bar, so it also accepts user input.</summary>
    public double Position
    {
        get => position;
        set
        {
            if (!Set(ref position, value)) return;

            OnPropertyChanged(nameof(PositionText), nameof(RemainingText));

            if (suppressSeek || !CanSeek) return;

            _ = media.SeekAsync(TimeSpan.FromSeconds(value));
        }
    }

    public string PositionText => Format(TimeSpan.FromSeconds(position));

    public string DurationText => Format(snapshot?.Duration ?? TimeSpan.Zero);

    public string RemainingText =>
        "-" + Format(TimeSpan.FromSeconds(Math.Max(0, Duration - position)));

    private static string Format(TimeSpan value) =>
        value.TotalHours >= 1
            ? $"{(int)value.TotalHours}:{value.Minutes:00}:{value.Seconds:00}"
            : $"{value.Minutes}:{value.Seconds:00}";

    // -- layout ------------------------------------------------------------

    public bool IsCompact => Model.View == MediaView.Compact;
    public bool IsWide => Model.View == MediaView.Wide;
    public bool IsLarge => Model.View == MediaView.Large;

    // -- plumbing ----------------------------------------------------------

    private void OnTick(object? sender, EventArgs e)
    {
        if (snapshot is null || !snapshot.IsPlaying) return;

        suppressSeek = true;
        Position = snapshot.PositionAt(DateTimeOffset.Now).TotalSeconds;
        suppressSeek = false;
    }

    private void OnMediaChanged(object? sender, EventArgs e)
    {
        snapshot = media.Current;

        suppressSeek = true;
        position = snapshot?.PositionAt(DateTimeOffset.Now).TotalSeconds ?? 0;
        suppressSeek = false;

        OnPropertyChanged(
            nameof(HasMedia), nameof(Title), nameof(Artist), nameof(HasArtist),
            nameof(Source), nameof(ShowSource), nameof(Cover), nameof(HasCover),
            nameof(IsPlaying), nameof(IsPaused), nameof(CanPlayPause),
            nameof(CanSkipNext), nameof(CanSkipPrevious), nameof(CanSeek),
            nameof(ShowProgress), nameof(Duration), nameof(Position),
            nameof(PositionText), nameof(DurationText), nameof(RemainingText));

        PlayPause.RaiseCanExecuteChanged();
        Next.RaiseCanExecuteChanged();
        Previous.RaiseCanExecuteChanged();
    }

    private void OnLayoutChanged(WidgetLayout layout)
    {
        ApplyFilter();

        OnPropertyChanged(
            nameof(Model), nameof(IsCompact), nameof(IsWide), nameof(IsLarge),
            nameof(ShowProgress), nameof(ShowSource), nameof(Cover), nameof(HasCover));
    }

    private void ApplyFilter()
    {
        var model = Model;
        media.SetFilter(model.Allows);
    }

    public void Dispose()
    {
        timer.Stop();
        media.Changed -= OnMediaChanged;
        layoutProvider.DataChanged -= OnLayoutChanged;

        GC.SuppressFinalize(this);
    }
}
