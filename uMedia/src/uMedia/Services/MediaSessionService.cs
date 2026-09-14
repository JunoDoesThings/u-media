using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using uMedia.Interfaces;
using uMedia.Models;
using Windows.Foundation;
using Windows.Media.Control;
using Windows.Storage.Streams;

namespace uMedia.Services;

/// <summary>
/// Reads the System Media Transport Controls: the same session data that drives
/// the volume flyout, so anything that shows up there shows up here (Spotify,
/// browsers, foobar2000, the Xbox app, ...).
/// </summary>
public class MediaSessionService : IMediaService, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly SemaphoreSlim snapshotGate = new(1, 1);
    private readonly SortedSet<string> knownApps = new(StringComparer.OrdinalIgnoreCase);

    private GlobalSystemMediaTransportControlsSessionManager? manager;
    private GlobalSystemMediaTransportControlsSession? session;

    private Func<string, bool> filter = _ => true;
    private string coverKey = "";
    private Bitmap? cover;
    private int coverAttempts;
    private bool disposed;

    public MediaSnapshot? Current { get; private set; }

    public IReadOnlyList<string> KnownApps
    {
        get { lock (knownApps) return knownApps.ToList(); }
    }

    public event EventHandler? Changed;
    public event EventHandler? AppsChanged;

    public async Task StartAsync()
    {
        try
        {
            manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            manager.SessionsChanged += OnSessionsChanged;
            manager.CurrentSessionChanged += OnSessionsChanged;

            await RefreshSessionAsync();
        }
        catch (Exception e)
        {
            Log.Error("Could not reach the Windows media session manager", e);
        }
    }

    public void SetFilter(Func<string, bool> value)
    {
        filter = value;
        _ = RefreshSessionAsync();
    }

    public Task PlayPauseAsync() => InvokeAsync(x => x.TryTogglePlayPauseAsync());
    public Task NextAsync() => InvokeAsync(x => x.TrySkipNextAsync());
    public Task PreviousAsync() => InvokeAsync(x => x.TrySkipPreviousAsync());

    public Task SeekAsync(TimeSpan position) =>
        InvokeAsync(x => x.TryChangePlaybackPositionAsync(position.Ticks));

    private async Task InvokeAsync(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> action)
    {
        var target = session;
        if (target is null) return;

        try
        {
            await action(target);
            await RefreshSnapshotAsync();
        }
        catch (Exception e)
        {
            Log.Error("Media command failed", e);
        }
    }

    // -- session selection -------------------------------------------------

    private void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, object args) =>
        _ = RefreshSessionAsync();

    /// <summary>
    /// Picks the session the widget should follow: the one Windows considers
    /// current, then any allowed session that is actually playing, then whatever
    /// is left. Sessions the filter rejects are skipped at every step.
    /// </summary>
    private async Task RefreshSessionAsync()
    {
        if (manager is null || disposed) return;

        await gate.WaitAsync();

        try
        {
            var sessions = manager.GetSessions().ToList();
            Remember(sessions);

            var allowed = sessions.Where(x => filter(x.SourceAppUserModelId ?? "")).ToList();
            var current = manager.GetCurrentSession();

            var picked = current is not null && filter(current.SourceAppUserModelId ?? "")
                ? current
                : allowed.FirstOrDefault(IsPlaying) ?? allowed.FirstOrDefault();

            if (!ReferenceEquals(picked, session))
            {
                Unsubscribe(session);
                session = picked;
                Subscribe(session);
            }
        }
        catch (Exception e)
        {
            Log.Error("Could not enumerate media sessions", e);
        }
        finally
        {
            gate.Release();
        }

        await RefreshSnapshotAsync();
    }

    private static bool IsPlaying(GlobalSystemMediaTransportControlsSession session)
    {
        try
        {
            return session.GetPlaybackInfo()?.PlaybackStatus ==
                   GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        }
        catch
        {
            return false;
        }
    }

    private void Remember(IEnumerable<GlobalSystemMediaTransportControlsSession> sessions)
    {
        var added = false;

        lock (knownApps)
        {
            foreach (var id in sessions.Select(x => x.SourceAppUserModelId))
                if (!string.IsNullOrWhiteSpace(id))
                    added |= knownApps.Add(id);
        }

        if (added) Post(() => AppsChanged?.Invoke(this, EventArgs.Empty));
    }

    private void Subscribe(GlobalSystemMediaTransportControlsSession? target)
    {
        if (target is null) return;

        target.MediaPropertiesChanged += OnSessionDataChanged;
        target.PlaybackInfoChanged += OnSessionDataChanged;
        target.TimelinePropertiesChanged += OnSessionDataChanged;
    }

    private void Unsubscribe(GlobalSystemMediaTransportControlsSession? target)
    {
        if (target is null) return;

        try
        {
            target.MediaPropertiesChanged -= OnSessionDataChanged;
            target.PlaybackInfoChanged -= OnSessionDataChanged;
            target.TimelinePropertiesChanged -= OnSessionDataChanged;
        }
        catch (Exception e)
        {
            Log.Error("Could not detach from the previous session", e);
        }
    }

    private void OnSessionDataChanged(GlobalSystemMediaTransportControlsSession sender, object args) =>
        _ = RefreshSnapshotAsync();

    // -- snapshot ----------------------------------------------------------

    /// <summary>
    /// Rebuilds the snapshot from the current session.
    ///
    /// Serialised: a session raises MediaPropertiesChanged, PlaybackInfoChanged
    /// and TimelinePropertiesChanged within milliseconds of each other, and
    /// letting those overlap is what made cover art disappear — a timeline
    /// refresh would finish after the artwork load and publish the older,
    /// coverless result.
    /// </summary>
    private async Task RefreshSnapshotAsync()
    {
        if (disposed) return;

        var target = session;

        if (target is null)
        {
            coverKey = "";
            cover = null;
            Publish(null);

            return;
        }

        await snapshotGate.WaitAsync();

        try
        {
            var properties = await target.TryGetMediaPropertiesAsync();
            var playback = target.GetPlaybackInfo();
            var timeline = target.GetTimelineProperties();

            var title = properties?.Title ?? "";
            var artist = properties?.Artist ?? "";
            var album = properties?.AlbumTitle ?? "";
            var key = $"{target.SourceAppUserModelId}|{title}|{artist}|{album}";

            if (key != coverKey)
            {
                // New track: forget the old artwork before loading, so a failed
                // load never leaves the previous cover on screen.
                coverKey = key;
                coverAttempts = 0;
                cover = await LoadCoverAsync(properties?.Thumbnail);
            }
            else if (cover is null)
            {
                // Same track, still no artwork. Several apps publish the
                // thumbnail a beat after the metadata, so this is a normal
                // second (third, fourth) look rather than an error.
                cover = await LoadCoverAsync(properties?.Thumbnail);
            }

            // EndTime is the track length for most apps; a few report a window
            // that starts somewhere other than zero, so subtract StartTime.
            var duration = timeline is null ? TimeSpan.Zero : timeline.EndTime - timeline.StartTime;
            var position = timeline is null ? TimeSpan.Zero : timeline.Position - timeline.StartTime;

            if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
            if (position < TimeSpan.Zero) position = TimeSpan.Zero;

            var updatedAt = timeline is null || timeline.LastUpdatedTime == default
                ? DateTimeOffset.Now
                : timeline.LastUpdatedTime;

            Publish(new MediaSnapshot
            {
                AppId = target.SourceAppUserModelId ?? "",
                Title = title,
                Artist = artist,
                Album = album,
                IsPlaying = playback?.PlaybackStatus ==
                            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing,
                CanPlayPause = playback?.Controls.IsPlayEnabled == true ||
                               playback?.Controls.IsPauseEnabled == true,
                CanSkipNext = playback?.Controls.IsNextEnabled == true,
                CanSkipPrevious = playback?.Controls.IsPreviousEnabled == true,
                CanSeek = playback?.Controls.IsPlaybackPositionEnabled == true && duration > TimeSpan.Zero,
                Position = position,
                Duration = duration,
                UpdatedAt = updatedAt,
                Cover = cover,
                CoverKey = key
            });

            if (cover is null && coverAttempts < CoverAttemptLimit)
            {
                coverAttempts++;
                _ = RetryCoverAsync(key);
            }
        }
        catch (Exception e)
        {
            Log.Error("Could not read the current session", e);
        }
        finally
        {
            snapshotGate.Release();
        }
    }

    private const int CoverAttemptLimit = 5;

    /// <summary>
    /// Looks again for artwork that was not published with the metadata.
    /// Gives up once the track changes or the limit is reached, so a source
    /// with genuinely no cover does not poll forever.
    /// </summary>
    private async Task RetryCoverAsync(string key)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(700));

        if (disposed || coverKey != key || cover is not null) return;

        await RefreshSnapshotAsync();
    }

    private static async Task<Bitmap?> LoadCoverAsync(IRandomAccessStreamReference? reference)
    {
        if (reference is null) return null;

        try
        {
            using var stream = await reference.OpenReadAsync();
            if (stream.Size == 0) return null;

            var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);

            var bytes = new byte[stream.Size];
            reader.ReadBytes(bytes);

            using var memory = new MemoryStream(bytes);

            return new Bitmap(memory);
        }
        catch (Exception e)
        {
            Log.Error("Could not decode cover art", e);

            return null;
        }
    }

    private void Publish(MediaSnapshot? snapshot)
    {
        Post(() =>
        {
            Current = snapshot;
            Changed?.Invoke(this, EventArgs.Empty);
        });
    }

    private static void Post(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess()) action();
        else Dispatcher.UIThread.Post(action, DispatcherPriority.Background);
    }

    public void Dispose()
    {
        disposed = true;

        Unsubscribe(session);

        if (manager is not null)
        {
            manager.SessionsChanged -= OnSessionsChanged;
            manager.CurrentSessionChanged -= OnSessionsChanged;
        }

        gate.Dispose();
        snapshotGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
