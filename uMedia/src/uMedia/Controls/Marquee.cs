using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;

namespace uMedia.Controls;

/// <summary>
/// Draws a single line of text and slides it sideways when it does not fit.
/// Cheaper than a templated control and it never reflows the layout around it,
/// which matters inside a widget only a couple of grid cells wide.
/// </summary>
public class Marquee : Avalonia.Controls.Control
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<Marquee, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsScrollingProperty =
        AvaloniaProperty.Register<Marquee, bool>(nameof(IsScrolling), true);

    /// <summary>Pixels per second.</summary>
    public static readonly StyledProperty<double> SpeedProperty =
        AvaloniaProperty.Register<Marquee, double>(nameof(Speed), 28d);

    /// <summary>Gap between the end of the text and the start of its repeat.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<Marquee, double>(nameof(Gap), 40d);

    public static readonly StyledProperty<double> FontSizeProperty =
        TextElement.FontSizeProperty.AddOwner<Marquee>();

    public static readonly StyledProperty<FontWeight> FontWeightProperty =
        TextElement.FontWeightProperty.AddOwner<Marquee>();

    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextElement.FontFamilyProperty.AddOwner<Marquee>();

    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextElement.ForegroundProperty.AddOwner<Marquee>();

    private readonly DispatcherTimer timer;
    private DateTime lastTick = DateTime.UtcNow;
    private double offset;
    private double textWidth;

    static Marquee()
    {
        AffectsRender<Marquee>(TextProperty, ForegroundProperty, FontSizeProperty,
            FontWeightProperty, FontFamilyProperty, IsScrollingProperty);

        AffectsMeasure<Marquee>(TextProperty, FontSizeProperty, FontWeightProperty, FontFamilyProperty);
    }

    public Marquee()
    {
        ClipToBounds = true;

        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(33), DispatcherPriority.Render, OnTick);
    }

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsScrolling
    {
        get => GetValue(IsScrollingProperty);
        set => SetValue(IsScrollingProperty, value);
    }

    public double Speed
    {
        get => GetValue(SpeedProperty);
        set => SetValue(SpeedProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public FontWeight FontWeight
    {
        get => GetValue(FontWeightProperty);
        set => SetValue(FontWeightProperty, value);
    }

    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var formatted = Build();
        textWidth = formatted.Width;

        // Never ask for more width than the parent offers: the point of the
        // control is to overflow horizontally, not to stretch its container.
        var width = double.IsInfinity(availableSize.Width) ? formatted.Width : availableSize.Width;

        return new Size(width, formatted.Height);
    }

    public override void Render(DrawingContext context)
    {
        var formatted = Build();
        textWidth = formatted.Width;

        var y = (Bounds.Height - formatted.Height) / 2;

        if (!ShouldScroll())
        {
            context.DrawText(formatted, new Point(0, y));
            return;
        }

        var step = textWidth + Gap;

        context.DrawText(formatted, new Point(-offset, y));
        context.DrawText(formatted, new Point(-offset + step, y));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        lastTick = DateTime.UtcNow;
        timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        timer.Stop();
    }

    private bool ShouldScroll() => IsScrolling && textWidth > Bounds.Width + 0.5;

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var delta = (now - lastTick).TotalSeconds;
        lastTick = now;

        if (!ShouldScroll())
        {
            if (offset == 0) return;

            offset = 0;
            InvalidateVisual();

            return;
        }

        var step = textWidth + Gap;

        offset += Speed * delta;
        if (offset > step) offset -= step;

        InvalidateVisual();
    }

    private FormattedText Build()
    {
        var typeface = new Typeface(FontFamily, FontStyle.Normal, FontWeight);

        return new FormattedText(
            Text ?? "",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            FontSize <= 0 ? 12 : FontSize,
            Foreground ?? Brushes.White);
    }
}
