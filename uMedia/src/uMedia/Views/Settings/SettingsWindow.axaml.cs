using Avalonia.Controls;
using Avalonia.Interactivity;
using uMedia.Interfaces;
using uMedia.Services;
using uMedia.ViewModels;

namespace uMedia.Views;

public partial class SettingsWindow : Window
{
    /// <summary>Parameterless constructor for the XAML previewer only.</summary>
    public SettingsWindow() : this(
        new WidgetLayoutProvider(),
        new AppSettingsProvider(),
        new MediaSessionService())
    {
    }

    public SettingsWindow(
        IWidgetLayoutProvider layoutProvider,
        IAppSettingsProvider settingsProvider,
        IMediaService media)
    {
        InitializeComponent();

        DataContext = new SettingsViewModel(layoutProvider, settingsProvider, media);
    }

    private void OnRemoveAppClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { DataContext: AppEntry entry }) return;
        if (DataContext is not SettingsViewModel viewModel) return;

        viewModel.RemoveApp(entry);
    }
}
