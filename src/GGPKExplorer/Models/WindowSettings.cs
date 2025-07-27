using CommunityToolkit.Mvvm.ComponentModel;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Window settings for the application
    /// </summary>
    public partial class WindowSettings : ObservableObject
    {
        [ObservableProperty]
        private double width = 1200;

        [ObservableProperty]
        private double height = 800;

        [ObservableProperty]
        private double left = 100;

        [ObservableProperty]
        private double top = 100;

        [ObservableProperty]
        private bool isMaximized = false;

        [ObservableProperty]
        private double splitterPosition = 300;

        [ObservableProperty]
        private bool showStatusBar = true;

        [ObservableProperty]
        private bool showToolbar = true;

        [ObservableProperty]
        private bool rememberWindowPosition = true;
    }
}