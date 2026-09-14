using System;
using System.IO;

namespace uMedia.Services;

/// <summary>
/// Failures here are never fatal: a widget that cannot reach Windows' media
/// controls should keep sitting on the desktop, not take the app down.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    private static readonly string Path =
        System.IO.Path.Combine(AppContext.BaseDirectory, "uMedia.log");

    public static void Error(string message, Exception? exception = null)
    {
        var line = $"{DateTime.Now:s} {message}{(exception is null ? "" : " :: " + exception)}";

        Console.Error.WriteLine(line);

        try
        {
            lock (Gate) File.AppendAllText(Path, line + Environment.NewLine);
        }
        catch
        {
            // Nothing sensible left to do.
        }
    }
}
