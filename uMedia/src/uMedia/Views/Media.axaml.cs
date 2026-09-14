using Avalonia.Controls;

namespace uMedia.Views;

/// <summary>
/// The widget itself. Deliberately free of window concerns, so the same control
/// can be hosted by <see cref="WidgetWindow"/> or dropped into uWidgets as a
/// plugin view (see README).
/// </summary>
public partial class Media : UserControl
{
    public Media()
    {
        InitializeComponent();
    }
}
