using System;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace uMedia.Services;

/// <summary>
/// Launching with Windows, via the per-user Run key. No elevation needed, and
/// removing the app is enough to make the entry harmless — it just stops
/// resolving. Also nothing for an uninstaller to miss, which suits a portable app.
/// </summary>
[SupportedOSPlatform("windows")]
public class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "uMedia";

    /// <summary>
    /// The executable to start. Under <c>dotnet run</c> this is the dotnet host
    /// rather than the widget, which is why startup should only be enabled on a
    /// published build.
    /// </summary>
    public static string? ExecutablePath => Environment.ProcessPath;

    public bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);

            return key?.GetValue(ValueName) is string value && value.Length > 0;
        }
        catch (Exception e)
        {
            Log.Error("Could not read the startup entry", e);

            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (key is null) return;

            if (!enabled)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);

                return;
            }

            var path = ExecutablePath;
            if (string.IsNullOrEmpty(path)) return;

            // Quoted: the path will contain spaces often enough, and an
            // unquoted Run entry is a classic way to break on "Program Files".
            key.SetValue(ValueName, $"\"{path}\"");
        }
        catch (Exception e)
        {
            Log.Error("Could not write the startup entry", e);
        }
    }

    /// <summary>
    /// Rewrites the entry if the app has been moved since it was registered.
    /// A portable app gets dragged to another folder sooner or later.
    /// </summary>
    public void Refresh(bool enabled)
    {
        if (!enabled)
        {
            if (IsEnabled()) SetEnabled(false);

            return;
        }

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            var current = key?.GetValue(ValueName) as string;
            var expected = $"\"{ExecutablePath}\"";

            if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                SetEnabled(true);
        }
        catch (Exception e)
        {
            Log.Error("Could not refresh the startup entry", e);
        }
    }
}
