using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Media;
using GGPKExplorer.ViewModels;
using GGPKExplorer.Services;
using GGPKExplorer.Models;
using GGPKExplorer.Accessibility;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace GGPKExplorer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly Wpf.Ui.IThemeService _themeService;
    private readonly ITaskBarService _taskBarService;
    private readonly IContentDialogService _contentDialogService;
    private readonly ISettingsService _settingsService;
    private readonly Services.IThemeService _customThemeService;
    private readonly Services.IDragDropService _dragDropService;
    public MainWindow(
        MainViewModel viewModel,
        Wpf.Ui.IThemeService themeService,
        ITaskBarService taskBarService,
        IContentDialogService contentDialogService,
        Services.ISettingsService settingsService,
        Services.IThemeService customThemeService,
        Services.IDragDropService dragDropService)
    {
        try
        {
            InitializeComponent();
            
            // Subscribe to window state changes to update maximize button
            StateChanged += MainWindow_StateChanged;
            
            // Ensure STA threading model for this window
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
            }

            _themeService = themeService ?? throw new ArgumentNullException(nameof(themeService));
            _taskBarService = taskBarService ?? throw new ArgumentNullException(nameof(taskBarService));
            _contentDialogService = contentDialogService ?? throw new ArgumentNullException(nameof(contentDialogService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _customThemeService = customThemeService ?? throw new ArgumentNullException(nameof(customThemeService));
            _dragDropService = dragDropService ?? throw new ArgumentNullException(nameof(dragDropService));

            // Set the DataContext to the injected ViewModel
            DataContext = viewModel;

            ConfigureServices();
            LoadWindowSettings();
            ConfigureThemeWatcher();
            ConfigureAccessibility();
            InitializeDragDrop();
            SetupSettingsHandling();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Critical error in MainWindow constructor: {ex.Message}", "MainWindow Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            throw; // Re-throw the exception
        }
    }

    /// <summary>
    /// Configures WPF-UI services for this window
    /// </summary>
    private void ConfigureServices()
    {
        try
        {
            // FloatingToastService doesn't need SnackbarPresenter configuration
            // It uses its own Popup-based system that truly floats
        }
        catch
        {
            // Continue without service configuration if it fails
        }
    }

    /// <summary>
    /// Configures automatic theme switching based on system theme
    /// </summary>
    private void ConfigureThemeWatcher()
    {
        // Initialize custom theme service
        _customThemeService.Initialize();
        
        // Apply Mica backdrop effect
        _customThemeService.ApplyMicaBackdrop(this);
        
        // Enable theme watching
        _customThemeService.WatchSystemTheme();
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
    }

    /// <summary>
    /// Configures accessibility features
    /// </summary>
    private void ConfigureAccessibility()
    {
        // Set up keyboard navigation for the main window
        KeyboardNavigationHelper.SetupMainWindowNavigation(this);
        
        // Set up high contrast theme support
        AccessibilityHelper.SetupHighContrastSupport(this);
        
        // Initialize high contrast theme manager
        var themeManager = HighContrastThemeManager.Instance;
        themeManager.ThemeChanged += OnThemeChanged;
        
        // Set focus to the main content area when window loads
        Loaded += (s, e) => MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
        
        // Announce window opening to screen readers
        Loaded += (s, e) => AccessibilityHelper.AnnounceToScreenReader("GGPK Explorer opened");
    }

    /// <summary>
    /// Handles theme changes for accessibility
    /// </summary>
    private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
    {
        if (e.IsHighContrastMode)
        {
            AccessibilityHelper.AnnounceToScreenReader("High contrast mode enabled");
        }
        else
        {
            AccessibilityHelper.AnnounceToScreenReader("High contrast mode disabled");
        }
    }

    /// <summary>
    /// Loads window settings from the settings service
    /// </summary>
    private async void LoadWindowSettings()
    {
        try
        {
            await _settingsService.LoadSettingsAsync();
            
            var windowSettings = _settingsService.WindowSettings;
            
            if (windowSettings.RememberWindowPosition)
            {
                // Apply window position and size
                Width = windowSettings.Width;
                Height = windowSettings.Height;
                Left = windowSettings.Left;
                Top = windowSettings.Top;
                
                if (windowSettings.IsMaximized)
                {
                    WindowState = WindowState.Maximized;
                }
            }
        }
        catch
        {
            // Ignore window settings loading errors
        }
    }

    /// <summary>
    /// Saves window settings to the settings service
    /// </summary>
    private async void SaveWindowSettings()
    {
        try
        {
            var windowSettings = _settingsService.WindowSettings;
            
            // Update window settings
            windowSettings.Width = Width;
            windowSettings.Height = Height;
            windowSettings.Left = Left;
            windowSettings.Top = Top;
            windowSettings.IsMaximized = WindowState == WindowState.Maximized;
            
            _settingsService.UpdateWindowSettings(windowSettings);
            await _settingsService.SaveSettingsAsync();
        }
        catch
        {
            // Ignore window settings saving errors
        }
    }

    /// <summary>
    /// Handles window closing to ensure proper cleanup
    /// </summary>
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        try
        {
            // Save window settings
            SaveWindowSettings();
            
            // Stop theme watching only if window is loaded
            if (IsLoaded)
            {
                Wpf.Ui.Appearance.SystemThemeWatcher.UnWatch(this);
            }
            
            // Clean up accessibility resources
            var themeManager = HighContrastThemeManager.Instance;
            themeManager.ThemeChanged -= OnThemeChanged;
            
            // Clean up settings subscription
            _settingsService.SettingsChanged -= OnSettingsChanged;
        }
        catch
        {
            // Continue with base cleanup even if our cleanup fails
        }
        
        base.OnClosing(e);
    }

    /// <summary>
    /// Handles drag and drop functionality for GGPK files
    /// </summary>
    protected override void OnDragEnter(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0 && Path.GetExtension(files[0]).Equals(".ggpk", StringComparison.OrdinalIgnoreCase))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        
        base.OnDragEnter(e);
    }

    /// <summary>
    /// Handles dropping GGPK files onto the window
    /// </summary>
    protected override void OnDrop(DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files?.Length > 0 && Path.GetExtension(files[0]).Equals(".ggpk", StringComparison.OrdinalIgnoreCase))
            {
                // Open the dropped GGPK file
                if (DataContext is MainViewModel viewModel)
                {
                    _ = viewModel.OpenRecentFileCommand.ExecuteAsync(files[0]);
                }
            }
        }
        
        base.OnDrop(e);
    }

    /// <summary>
    /// Initializes drag and drop functionality
    /// </summary>
    private void InitializeDragDrop()
    {
        _dragDropService.InitializeDragDrop(this);
    }

    /// <summary>
    /// Sets up settings handling and applies initial settings
    /// </summary>
    private void SetupSettingsHandling()
    {
        try
        {
            // Subscribe to settings changes
            _settingsService.SettingsChanged += OnSettingsChanged;
            
            // Apply initial settings
            ApplySettings();
        }
        catch
        {
            // Ignore settings handling setup errors
        }
    }

    /// <summary>
    /// Handles settings changes
    /// </summary>
    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        try
        {
            // Apply settings when they change
            Dispatcher.BeginInvoke(() => ApplySettings());
        }
        catch
        {
            // Ignore settings change handling errors
        }
    }

    /// <summary>
    /// Applies current settings to the UI
    /// </summary>
    private void ApplySettings()
    {
        try
        {
            var windowSettings = _settingsService.WindowSettings;
            
            // Apply status bar visibility
            if (StatusBar != null)
            {
                StatusBar.Visibility = windowSettings.ShowStatusBar ? Visibility.Visible : Visibility.Collapsed;
            }
            
            // Apply menu bar visibility
            if (MenuBar != null)
            {
                MenuBar.Visibility = windowSettings.ShowToolbar ? Visibility.Visible : Visibility.Collapsed;
            }
            
            // Apply theme
            ApplyTheme(_settingsService.Theme);
        }
        catch
        {
            // Ignore settings application errors
        }
    }

    /// <summary>
    /// Applies the specified theme
    /// </summary>
    private void ApplyTheme(ApplicationTheme theme)
    {
        try
        {
            switch (theme)
            {
                case ApplicationTheme.Light:
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
                    break;
                case ApplicationTheme.Dark:
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
                    break;
                case ApplicationTheme.System:
                default:
                    var systemTheme = Wpf.Ui.Appearance.ApplicationThemeManager.GetSystemTheme();
                    var appTheme = systemTheme == Wpf.Ui.Appearance.SystemTheme.Light 
                        ? Wpf.Ui.Appearance.ApplicationTheme.Light 
                        : Wpf.Ui.Appearance.ApplicationTheme.Dark;
                    Wpf.Ui.Appearance.ApplicationThemeManager.Apply(appTheme);
                    break;
            }
        }
        catch
        {
            // Ignore theme application errors
        }
    }



    #region Window Control Event Handlers

    /// <summary>
    /// Handles title bar mouse down for window dragging and double-click for maximize/restore
    /// </summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            // Don't handle if clicking directly on buttons or menu items
            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource != null)
            {
                // Check if we're clicking on a button or within a menu
                if (IsChildOfType<Button>(originalSource) || IsChildOfType<MenuItem>(originalSource))
                {
                    return; // Don't handle drag/double-click for buttons and menu items
                }
            }

            // Check for double-click
            if (e.ClickCount == 2)
            {
                // Double-click: toggle maximize/restore
                if (WindowState == WindowState.Maximized)
                {
                    WindowState = WindowState.Normal;
                }
                else
                {
                    WindowState = WindowState.Maximized;
                }
                e.Handled = true;
            }
            else if (e.ClickCount == 1)
            {
                // Single click: drag window (with a small delay to allow for double-click detection)
                try
                {
                    DragMove();
                }
                catch
                {
                    // Ignore drag move exceptions (can happen if window state changes during drag)
                }
            }
        }
    }

    /// <summary>
    /// Helper method to check if an element is a child of a specific type
    /// </summary>
    private bool IsChildOfType<T>(DependencyObject element) where T : DependencyObject
    {
        if (element is T)
            return true;
            
        var parent = VisualTreeHelper.GetParent(element);
        while (parent != null)
        {
            if (parent is T)
                return true;
            parent = VisualTreeHelper.GetParent(parent);
        }
        return false;
    }

    /// <summary>
    /// Handles minimize button click
    /// </summary>
    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    /// <summary>
    /// Handles maximize/restore button click
    /// </summary>
    private void MaximizeButton_Click(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            MaximizeButton.Content = "🗖";
            MaximizeButton.ToolTip = "Maximize";
        }
        else
        {
            WindowState = WindowState.Maximized;
            MaximizeButton.Content = "🗗";
            MaximizeButton.ToolTip = "Restore";
        }
    }

    /// <summary>
    /// Handles close button click
    /// </summary>
    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Handles window state changes to update maximize button
    /// </summary>
    private void MainWindow_StateChanged(object? sender, EventArgs e)
    {
        if (MaximizeButton != null)
        {
            if (WindowState == WindowState.Maximized)
            {
                MaximizeButton.Content = "🗗";
                MaximizeButton.ToolTip = "Restore";
            }
            else
            {
                MaximizeButton.Content = "🗖";
                MaximizeButton.ToolTip = "Maximize";
            }
        }
    }

    #endregion

    #region Search Event Handlers



    #endregion
}