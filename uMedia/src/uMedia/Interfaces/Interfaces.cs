using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using uMedia.Models;

namespace uMedia.Interfaces;

public delegate void DataChangedEvent<in T>(T data);

/// <summary>
/// Service for reading and writing a single piece of data.
/// </summary>
public interface IDataProvider<T>
{
    T Get();
    void Save(T data);

    event DataChangedEvent<T>? DataChanged;
}

/// <summary>
/// Reads and writes <c>settings.json</c>.
/// </summary>
public interface IAppSettingsProvider : IDataProvider<AppSettings> { }

/// <summary>
/// Reads and writes <c>layout.json</c>.
/// </summary>
public interface IWidgetLayoutProvider : IDataProvider<WidgetLayout> { }

/// <summary>
/// Wraps the Windows System Media Transport Controls.
/// </summary>
public interface IMediaService
{
    /// <summary>The session the widget is following, or null when nothing is playing.</summary>
    MediaSnapshot? Current { get; }

    /// <summary>Every app that has reported a session since the widget started.</summary>
    IReadOnlyList<string> KnownApps { get; }

    /// <summary>Raised on the UI thread whenever <see cref="Current"/> changes.</summary>
    event EventHandler? Changed;

    /// <summary>Raised on the UI thread whenever <see cref="KnownApps"/> grows.</summary>
    event EventHandler? AppsChanged;

    Task StartAsync();

    /// <summary>Re-applies the widget's whitelist/blacklist and picks a session again.</summary>
    void SetFilter(Func<string, bool> filter);

    Task PlayPauseAsync();
    Task NextAsync();
    Task PreviousAsync();
    Task SeekAsync(TimeSpan position);
}
