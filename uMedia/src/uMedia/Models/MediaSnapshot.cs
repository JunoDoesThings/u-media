using System;
using Avalonia.Media.Imaging;

namespace uMedia.Models;

/// <summary>
/// Everything the widget knows about the session it is currently following.
/// Immutable: the service replaces it wholesale on every change.
/// </summary>
public record MediaSnapshot
{
    public string AppId { get; init; } = "";
    public string Title { get; init; } = "";
    public string Artist { get; init; } = "";
    public string Album { get; init; } = "";

    public bool IsPlaying { get; init; }
    public bool CanPlayPause { get; init; }
    public bool CanSkipNext { get; init; }
    public bool CanSkipPrevious { get; init; }
    public bool CanSeek { get; init; }

    public TimeSpan Position { get; init; }
    public TimeSpan Duration { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

    public Bitmap? Cover { get; init; }

    /// <summary>Identity of the artwork, used to avoid decoding the same cover twice.</summary>
    public string CoverKey { get; init; } = "";

    /// <summary>
    /// Position interpolated up to <paramref name="now"/>, because Windows only
    /// reports the timeline when it changes, not while it runs.
    /// </summary>
    public TimeSpan PositionAt(DateTimeOffset now)
    {
        if (!IsPlaying) return Position;

        var elapsed = now - UpdatedAt;
        if (elapsed < TimeSpan.Zero) elapsed = TimeSpan.Zero;

        var position = Position + elapsed;

        return Duration > TimeSpan.Zero && position > Duration ? Duration : position;
    }
}
