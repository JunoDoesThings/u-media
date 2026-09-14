using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace uMedia.Services;

/// <summary>
/// The handful of window tweaks Avalonia does not expose: rounded corners on a
/// borderless window, hiding from Alt+Tab, and pinning the widget to the desktop.
/// </summary>
[SupportedOSPlatform("windows")]
public static class Win32
{
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    public enum CornerPreference
    {
        Default = 0,
        DoNotRound = 1,
        Round = 2,
        RoundSmall = 3
    }

    /// <summary>DWM backdrop values, matching DWM_SYSTEMBACKDROP_TYPE.</summary>
    public enum BackdropType
    {
        Auto = 0,
        None = 1,

        /// <summary>Mica.</summary>
        MainWindow = 2,

        /// <summary>Acrylic.</summary>
        TransientWindow = 3,

        /// <summary>Mica Alt, the tabbed variant.</summary>
        TabbedWindow = 4
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    public static void SetCorners(IntPtr handle, CornerPreference preference)
    {
        if (handle == IntPtr.Zero) return;

        var value = (int)preference;
        Try(() => DwmSetWindowAttribute(handle, DwmwaWindowCornerPreference, ref value, sizeof(int)));
    }

    /// <summary>
    /// Asks DWM for a backdrop directly. Avalonia's TransparencyLevelHint covers
    /// this on Windows 11 22H2+, but setting it here too keeps Mica Alt available
    /// and makes the behaviour predictable on earlier builds.
    /// </summary>
    public static void SetBackdrop(IntPtr handle, BackdropType type)
    {
        if (handle == IntPtr.Zero) return;

        var value = (int)type;
        Try(() => DwmSetWindowAttribute(handle, DwmwaSystemBackdropType, ref value, sizeof(int)));
    }

    /// <summary>Keeps the widget out of Alt+Tab and off the taskbar.</summary>
    public static void SetToolWindow(IntPtr handle, bool enabled)
    {
        if (handle == IntPtr.Zero) return;

        var style = GetWindowLong(handle, GwlExStyle);
        style = enabled ? style | WsExToolWindow : style & ~WsExToolWindow;

        SetWindowLong(handle, GwlExStyle, style);
    }

    public static void SetNoActivate(IntPtr handle, bool enabled)
    {
        if (handle == IntPtr.Zero) return;

        var style = GetWindowLong(handle, GwlExStyle);
        style = enabled ? style | WsExNoActivate : style & ~WsExNoActivate;

        SetWindowLong(handle, GwlExStyle, style);
    }

    private static void Try(Func<int> action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            Log.Error("Win32 call failed", e);
        }
    }
}

/// <summary>
/// Turns an app user model id into something worth showing in a settings list.
/// Windows hands out two shapes: <c>Spotify.exe</c> for desktop apps, and
/// <c>Family_hash!AppId</c> for packaged ones.
/// </summary>
public static class AppId
{
    public static string ToDisplayName(string appId)
    {
        if (string.IsNullOrWhiteSpace(appId)) return "Unknown app";

        var name = appId;

        var bang = name.IndexOf('!');
        if (bang > 0) name = name[..bang];

        var underscore = name.IndexOf('_');
        if (underscore > 0) name = name[..underscore];

        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            name = name[..^4];

        var dot = name.LastIndexOf('.');
        if (dot > 0 && dot < name.Length - 1) name = name[(dot + 1)..];

        return name.Length == 0
            ? appId
            : CultureInfo.CurrentCulture.TextInfo.ToTitleCase(name);
    }
}
