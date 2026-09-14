using System;
using System.Collections.Generic;

namespace uMedia.Models;

/// <summary>
/// How <see cref="MediaModel.Apps"/> is applied to the sessions reported by Windows.
/// </summary>
public enum FilterMode
{
    /// <summary>Every media app is allowed.</summary>
    Off,

    /// <summary>Only the listed apps are allowed.</summary>
    Whitelist,

    /// <summary>Every app except the listed ones is allowed.</summary>
    Blacklist
}

/// <summary>
/// Which of the three layouts the widget renders.
/// </summary>
public enum MediaView
{
    /// <summary>2x2. Cover art, title, play/pause.</summary>
    Compact,

    /// <summary>4x2. Cover art beside the track info and transport controls.</summary>
    Wide,

    /// <summary>4x4. Large cover art, transport controls and a seek bar.</summary>
    Large
}

/// <summary>
/// The widget's own model. Serialised into <c>layout.json</c>.
/// </summary>
public record MediaModel
{
    public MediaView View { get; set; } = MediaView.Wide;

    public bool ShowCover { get; set; } = true;
    public bool ShowProgress { get; set; } = true;
    public bool ShowSource { get; set; } = true;

    /// <summary>Scroll long titles instead of trimming them with an ellipsis.</summary>
    public bool ScrollTitle { get; set; } = true;

    public FilterMode FilterMode { get; set; } = FilterMode.Off;

    /// <summary>
    /// App user model ids the filter applies to, e.g. <c>Spotify.exe</c> or
    /// <c>Microsoft.ZuneMusic_8wekyb3d8bbwe!Microsoft.ZuneMusic</c>.
    /// </summary>
    public List<string> Apps { get; set; } = new();

    public bool Allows(string? appId)
    {
        if (FilterMode == FilterMode.Off) return true;
        if (string.IsNullOrEmpty(appId)) return FilterMode == FilterMode.Blacklist;

        var listed = Apps.Exists(x => string.Equals(x, appId, StringComparison.OrdinalIgnoreCase));

        return FilterMode == FilterMode.Whitelist ? listed : !listed;
    }
}
