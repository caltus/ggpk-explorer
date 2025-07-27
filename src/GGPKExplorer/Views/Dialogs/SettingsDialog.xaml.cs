using System;
using System.Windows;
using System.Windows.Controls;
using GGPKExplorer.ViewModels;
using GGPKExplorer.Models;
using GGPKExplorer.Accessibility;
using GGPKExplorer.Services;
using Wpf.Ui.Controls;

namespace GGPKExplorer.Views.Dialogs
{
    /// <summary>
    /// Settings Dialog using WPF-UI NavigationView with Card controls for settings sections
    /// </summary>
    public partial class SettingsDialog : FluentWindow
    {
        public SettingsDialogViewModel ViewModel { get; }

        public SettingsDialog(SettingsDialogViewModel viewModel)
        {
            System.Diagnostics.Debug.WriteLine("SettingsDialog: Constructor starting");
            
            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsDialog: Calling InitializeComponent");
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("SettingsDialog: InitializeComponent completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialog: ERROR in InitializeComponent: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SettingsDialog: StackTrace: {ex.StackTrace}");
                throw;
            }
            
            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsDialog: Setting ViewModel");
                ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                System.Diagnostics.Debug.WriteLine("SettingsDialog: ViewModel set successfully");
                
                System.Diagnostics.Debug.WriteLine("SettingsDialog: Setting DataContext");
                DataContext = ViewModel;
                System.Diagnostics.Debug.WriteLine("SettingsDialog: DataContext set successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialog: ERROR setting ViewModel/DataContext: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SettingsDialog: StackTrace: {ex.StackTrace}");
                throw;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsDialog: Setting up accessibility");
                // Set up accessibility
                SetupAccessibility();
                System.Diagnostics.Debug.WriteLine("SettingsDialog: Accessibility setup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialog: ERROR setting up accessibility: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SettingsDialog: StackTrace: {ex.StackTrace}");
                // Don't throw here, accessibility is not critical
            }

            try
            {
                System.Diagnostics.Debug.WriteLine("SettingsDialog: Showing general settings panel");
                // Show general settings by default
                ShowSettingsPanel("general");
                System.Diagnostics.Debug.WriteLine("SettingsDialog: General settings panel shown");
                
                System.Diagnostics.Debug.WriteLine("SettingsDialog: Setting GeneralButton appearance");
                if (GeneralButton != null)
                {
                    GeneralButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                    System.Diagnostics.Debug.WriteLine("SettingsDialog: GeneralButton appearance set");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("SettingsDialog: WARNING - GeneralButton is null");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialog: ERROR setting up default panel: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SettingsDialog: StackTrace: {ex.StackTrace}");
                throw;
            }
            
            System.Diagnostics.Debug.WriteLine("SettingsDialog: Constructor completed successfully");
        }

        /// <summary>
        /// Sets up accessibility features for the settings dialog
        /// </summary>
        private void SetupAccessibility()
        {
            // Set up keyboard navigation for dialog
            KeyboardNavigationHelper.SetupDialogNavigation(this);
            
            // Set up high contrast support
            AccessibilityHelper.SetupHighContrastSupport(this);
            
            // Announce dialog opening
            Loaded += (s, e) => AccessibilityHelper.AnnounceToScreenReader("Settings dialog opened");
        }

        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Wpf.Ui.Controls.Button button)
            {
                // Reset all button appearances
                GeneralButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                RecentButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                AboutButton.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;

                // Set clicked button as primary
                button.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;

                var tag = button.Tag?.ToString();
                ShowSettingsPanel(tag);
            }
        }

        private void ShowSettingsPanel(string? tag)
        {
            // Hide all panels
            GeneralSettings.Visibility = Visibility.Collapsed;
            RecentFilesSettings.Visibility = Visibility.Collapsed;
            LogsSettings.Visibility = Visibility.Collapsed;
            AboutSettings.Visibility = Visibility.Collapsed;

            // Show the selected panel
            switch (tag)
            {
                case "general":
                    GeneralSettings.Visibility = Visibility.Visible;
                    break;
                case "recent":
                    RecentFilesSettings.Visibility = Visibility.Visible;
                    break;
                case "logs":
                    LogsSettings.Visibility = Visibility.Visible;
                    InitializeLogViewer();
                    break;
                case "about":
                    AboutSettings.Visibility = Visibility.Visible;
                    break;
                default:
                    GeneralSettings.Visibility = Visibility.Visible;
                    break;
            }
        }



        private bool _isClosing = false;
        private bool _isSaving = false;
        private LogViewerViewModel? _logViewerViewModel;

        /// <summary>
        /// Initializes the log viewer control with its ViewModel
        /// </summary>
        private void InitializeLogViewer()
        {
            try
            {
                if (_logViewerViewModel == null)
                {
                    _logViewerViewModel = new LogViewerViewModel();
                    LogViewerControl.DataContext = _logViewerViewModel;
                    System.Diagnostics.Debug.WriteLine("SettingsDialog: Log viewer initialized successfully");
                }
                else
                {
                    // Refresh logs when switching to logs panel
                    _logViewerViewModel.RefreshLogsCommand.Execute(null);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialog: Error initializing log viewer: {ex.Message}");
            }
        }



        private async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Prevent multiple clicks or re-entry
            if (_isClosing || _isSaving)
            {
                System.Diagnostics.Debug.WriteLine("OkButton_Click: Already processing, ignoring click");
                return;
            }

            try
            {
                // Check if there are unsaved changes
                bool hasUnsavedChanges = ViewModel?.HasUnsavedChanges == true;
                
                if (hasUnsavedChanges)
                {
                    // Save settings first
                    _isSaving = true;
                    System.Diagnostics.Debug.WriteLine("OkButton_Click: Saving settings before closing");

                    // Disable the button to prevent multiple clicks
                    if (sender is Wpf.Ui.Controls.Button clickedButton)
                    {
                        clickedButton.IsEnabled = false;
                    }

                    // Save settings
                    if (ViewModel?.SaveSettingsCommand?.CanExecute(null) == true)
                    {
                        await ViewModel.SaveSettingsCommand.ExecuteAsync(null);
                        System.Diagnostics.Debug.WriteLine("OkButton_Click: Settings saved successfully");
                    }

                    // Re-enable the button
                    if (sender is Wpf.Ui.Controls.Button clickedButton2)
                    {
                        clickedButton2.IsEnabled = true;
                    }
                    
                    _isSaving = false;
                    
                    // After saving, check if button is in Apply mode (stays open) or OK mode (closes)
                    // If HasUnsavedChanges is still true after save, it means we're in Apply mode
                    if (ViewModel?.HasUnsavedChanges == true)
                    {
                        // Apply mode - keep dialog open
                        System.Diagnostics.Debug.WriteLine("OkButton_Click: Apply mode - keeping dialog open");
                        return;
                    }
                }
                
                // OK mode or no unsaved changes - close the dialog
                _isClosing = true;
                System.Diagnostics.Debug.WriteLine("OkButton_Click: Closing dialog");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialog.OkButton_Click: Error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"SettingsDialog.OkButton_Click: StackTrace: {ex.StackTrace}");
                
                _isClosing = false;
                _isSaving = false;
                
                // Re-enable the button
                if (sender is Wpf.Ui.Controls.Button button)
                {
                    button.IsEnabled = true;
                }
                
                // Show error message to user
                System.Windows.MessageBox.Show(
                    $"Failed to save settings: {ex.Message}",
                    "Error Saving Settings",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Prevent multiple clicks
            if (_isClosing)
            {
                System.Diagnostics.Debug.WriteLine("CancelButton_Click: Already closing, ignoring click");
                return;
            }

            try
            {
                _isClosing = true;
                System.Diagnostics.Debug.WriteLine("CancelButton_Click: Processing cancel");

                if (ViewModel?.HasUnsavedChanges == true)
                {
                    var result = System.Windows.MessageBox.Show(
                        "You have unsaved changes. Are you sure you want to cancel?",
                        "Unsaved Changes",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.No)
                    {
                        _isClosing = false;
                        return;
                    }
                }

                System.Diagnostics.Debug.WriteLine("CancelButton_Click: Setting DialogResult and closing");
                DialogResult = false;
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialog.CancelButton_Click: Error: {ex.Message}");
                _isClosing = false;
                // Continue with cancel even if there's an error
                DialogResult = false;
                Close();
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                // Only show unsaved changes dialog if we're not already closing from a button click
                // and the dialog result is null (meaning user closed via X button or Alt+F4)
                if (ViewModel?.HasUnsavedChanges == true && DialogResult == null && !_isClosing)
                {
                    var result = System.Windows.MessageBox.Show(
                        "You have unsaved changes. Are you sure you want to close?",
                        "Unsaved Changes",
                        System.Windows.MessageBoxButton.YesNo,
                        System.Windows.MessageBoxImage.Question);

                    if (result == System.Windows.MessageBoxResult.No)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialog.OnClosing: Error checking unsaved changes: {ex.Message}");
                // Continue with closing even if there's an error
            }

            base.OnClosing(e);
        }

        /// <summary>
        /// Handles theme selection changes
        /// </summary>
        private void ThemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (sender is ComboBox comboBox && comboBox.SelectedIndex >= 0 && ViewModel != null)
                {
                    var theme = (ApplicationTheme)comboBox.SelectedIndex;
                    ViewModel.SelectedTheme = theme;
                    
                    // Apply theme immediately for preview
                    ApplyThemePreview(theme);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SettingsDialog.ThemeComboBox_SelectionChanged: Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies theme preview
        /// </summary>
        private void ApplyThemePreview(ApplicationTheme theme)
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying theme preview: {ex.Message}");
            }
        }



        /// <summary>
        /// Finds a visual child of the specified type
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent, string name = "") where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                
                if (child is T result && (string.IsNullOrEmpty(name) || 
                    (child is FrameworkElement element && element.Name == name)))
                {
                    return result;
                }

                var childOfChild = FindVisualChild<T>(child, name);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }


    }
}