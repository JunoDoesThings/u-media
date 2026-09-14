using System.Text.Json.Serialization;

namespace uMedia.Models;

/// <summary>
/// Backdrop material of the widget window.
/// </summary>
public enum Backdrop
{
    Mica,
    MicaAlt,
    Acrylic,
    Solid
}

/// <summary>
/// Colour mode of the widget.
/// </summary>
public enum ThemeVariant
{
    Auto,
    Light,
    Dark
}

/// <summary>
/// Colours and materials, shared by every widget.
/// </summary>
public record Theme
{
    public ThemeVariant Variant { get; set; } = ThemeVariant.Auto;
    public Backdrop Backdrop { get; set; } = Backdrop.Mica;

    /// <summary>Accent colour, as #AARRGGBB or #RRGGBB. Empty means "use the system accent".</summary>
    public string Accent { get; set; } = "";

    /// <summary>Font family for the widget text. Empty means the system UI font.</summary>
    public string Font { get; set; } = "";

    /// <summary>Tint drawn on top of the backdrop. 0 = pure backdrop, 1 = opaque card.</summary>
    public double Tint { get; set; } = 0.18;
}

/// <summary>
/// Virtual grid the widget snaps its position and size to.
/// </summary>
public record Layout
{
    /// <summary>Size of a single grid cell, in device independent pixels.</summary>
    public int CellSize { get; set; } = 64;

    /// <summary>Gap between two neighbouring cells.</summary>
    public int Margin { get; set; } = 12;

    /// <summary>Corner radius of the widget.</summary>
    public int Radius { get; set; } = 8;

    /// <summary>Snap position and size to the grid.</summary>
    public bool SnapToGrid { get; set; } = true;
}

/// <summary>
/// Application settings, stored in <c>settings.json</c> next to the executable.
/// </summary>
public record AppSettings
{
    public Theme Theme { get; set; } = new();
    public Layout Layout { get; set; } = new();

    /// <summary>Keep the widget above other windows.</summary>
    public bool Topmost { get; set; }

    /// <summary>Hide the widget from Alt+Tab and the taskbar.</summary>
    public bool ToolWindow { get; set; } = true;

    /// <summary>Launch with Windows.</summary>
    public bool RunOnStartup { get; set; }
}

/// <summary>
/// Position, size and per-widget model, stored in <c>layout.json</c>.
/// Mirrors uWidgets' WidgetLayout so the same file can be moved between the two.
/// </summary>
public record WidgetLayout
{
    public string Type { get; set; } = "uMedia";
    public string SubType { get; set; } = "Media";
    public int X { get; set; } = 64;
    public int Y { get; set; } = 64;

    /// <summary>Width in grid columns.</summary>
    public int Width { get; set; } = 4;

    /// <summary>Height in grid rows.</summary>
    public int Height { get; set; } = 2;

    /// <summary>
    /// Exact size in device independent pixels. Cell counts alone cannot
    /// describe a widget resized with snapping turned off, so the real size is
    /// stored too and wins when set. Zero means "derive it from the cells".
    /// </summary>
    public double PixelWidth { get; set; }

    public double PixelHeight { get; set; }

    [JsonPropertyName("settings")]
    public MediaModel Settings { get; set; } = new();
}
