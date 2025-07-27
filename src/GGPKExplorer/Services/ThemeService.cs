using System;
using System.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for managing application theme and appearance
    /// </summary>
    public interface IThemeService
    {
        void Initialize();
        void ApplyMicaBackdrop(FluentWindow window);
        void WatchSystemTheme();
    }

    public class ThemeService : IThemeService
    {
        private readonly ISettingsService _settingsService;

        public ThemeService(ISettingsService settingsService)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        }

        public void Initialize()
        {
            // Basic initialization - let WPF-UI handle theme management
            WatchSystemTheme();
        }

        public void ApplyMicaBackdrop(FluentWindow window)
        {
            if (window == null) return;

            try
            {
                // Enable Mica backdrop for Windows 11
                window.WindowBackdropType = WindowBackdropType.Mica;
                window.ExtendsContentIntoTitleBar = true;
            }
            catch (Exception)
            {
                // Mica backdrop is optional - silently fall back to standard window
            }
        }

        public void WatchSystemTheme()
        {
            try
            {
                // SystemThemeWatcher is static in WPF-UI
                if (Application.Current.MainWindow != null)
                {
                    SystemThemeWatcher.Watch(Application.Current.MainWindow);
                }
            }
            catch (Exception)
            {
                // System theme watching is optional - continue without it
            }
        }
    }
}