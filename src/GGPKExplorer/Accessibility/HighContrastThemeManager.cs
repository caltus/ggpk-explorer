using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui.Appearance;

namespace GGPKExplorer.Accessibility
{
    /// <summary>
    /// Manages high contrast theme support and system theme integration
    /// </summary>
    public class HighContrastThemeManager
    {
        private static HighContrastThemeManager _instance = null!;
        private static readonly object _lock = new object();

        public static HighContrastThemeManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new HighContrastThemeManager();
                        }
                    }
                }
                return _instance;
            }
        }

        private bool _isHighContrastMode;
        private ResourceDictionary _highContrastResources = null!;

        public event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

        private HighContrastThemeManager()
        {
            Initialize();
        }

        private void Initialize()
        {
            // Check initial high contrast state
            _isHighContrastMode = SystemParameters.HighContrast;

            // Create high contrast resource dictionary
            CreateHighContrastResources();

            // Listen for system theme changes
            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;

            // Apply initial theme
            ApplyTheme();
        }

        private void CreateHighContrastResources()
        {
            _highContrastResources = new ResourceDictionary();

            // Define high contrast colors
            _highContrastResources["HighContrastBackgroundBrush"] = new SolidColorBrush(System.Windows.SystemColors.WindowColor);
            _highContrastResources["HighContrastForegroundBrush"] = new SolidColorBrush(System.Windows.SystemColors.WindowTextColor);
            _highContrastResources["HighContrastBorderBrush"] = new SolidColorBrush(System.Windows.SystemColors.WindowFrameColor);
            _highContrastResources["HighContrastSelectionBrush"] = new SolidColorBrush(System.Windows.SystemColors.HighlightColor);
            _highContrastResources["HighContrastSelectionTextBrush"] = new SolidColorBrush(System.Windows.SystemColors.HighlightTextColor);
            _highContrastResources["HighContrastDisabledBrush"] = new SolidColorBrush(System.Windows.SystemColors.GrayTextColor);
            _highContrastResources["HighContrastButtonBrush"] = new SolidColorBrush(System.Windows.SystemColors.ControlColor);
            _highContrastResources["HighContrastButtonTextBrush"] = new SolidColorBrush(System.Windows.SystemColors.ControlTextColor);

            // Define high contrast styles
            CreateHighContrastStyles();
        }

        private void CreateHighContrastStyles()
        {
            // Button style for high contrast
            var buttonStyle = new Style(typeof(Button));
            buttonStyle.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("HighContrastButtonBrush")));
            buttonStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("HighContrastButtonTextBrush")));
            buttonStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("HighContrastBorderBrush")));
            buttonStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
            _highContrastResources[typeof(Button)] = buttonStyle;

            // TextBlock style for high contrast
            var textBlockStyle = new Style(typeof(TextBlock));
            textBlockStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("HighContrastForegroundBrush")));
            _highContrastResources[typeof(TextBlock)] = textBlockStyle;

            // TreeView style for high contrast
            var treeViewStyle = new Style(typeof(TreeView));
            treeViewStyle.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("HighContrastBackgroundBrush")));
            treeViewStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("HighContrastForegroundBrush")));
            treeViewStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("HighContrastBorderBrush")));
            treeViewStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
            _highContrastResources[typeof(TreeView)] = treeViewStyle;

            // ListView style for high contrast
            var listViewStyle = new Style(typeof(ListView));
            listViewStyle.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("HighContrastBackgroundBrush")));
            listViewStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("HighContrastForegroundBrush")));
            listViewStyle.Setters.Add(new Setter(Control.BorderBrushProperty, new DynamicResourceExtension("HighContrastBorderBrush")));
            listViewStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(2)));
            _highContrastResources[typeof(ListView)] = listViewStyle;

            // TreeViewItem style for high contrast
            var treeViewItemStyle = new Style(typeof(TreeViewItem));
            treeViewItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("HighContrastForegroundBrush")));
            
            // Selection trigger
            var selectionTrigger = new Trigger { Property = TreeViewItem.IsSelectedProperty, Value = true };
            selectionTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("HighContrastSelectionBrush")));
            selectionTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("HighContrastSelectionTextBrush")));
            treeViewItemStyle.Triggers.Add(selectionTrigger);
            
            _highContrastResources[typeof(TreeViewItem)] = treeViewItemStyle;

            // ListViewItem style for high contrast
            var listViewItemStyle = new Style(typeof(ListViewItem));
            listViewItemStyle.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("HighContrastForegroundBrush")));
            
            // Selection trigger
            var listSelectionTrigger = new Trigger { Property = ListViewItem.IsSelectedProperty, Value = true };
            listSelectionTrigger.Setters.Add(new Setter(Control.BackgroundProperty, new DynamicResourceExtension("HighContrastSelectionBrush")));
            listSelectionTrigger.Setters.Add(new Setter(Control.ForegroundProperty, new DynamicResourceExtension("HighContrastSelectionTextBrush")));
            listViewItemStyle.Triggers.Add(listSelectionTrigger);
            
            _highContrastResources[typeof(ListViewItem)] = listViewItemStyle;
        }

        private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
        {
            if (e.Category == UserPreferenceCategory.Accessibility)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CheckAndUpdateTheme();
                }));
            }
        }

        private void OnSystemParametersChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast))
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    CheckAndUpdateTheme();
                }));
            }
        }

        private void CheckAndUpdateTheme()
        {
            var newHighContrastState = SystemParameters.HighContrast;
            if (newHighContrastState != _isHighContrastMode)
            {
                _isHighContrastMode = newHighContrastState;
                ApplyTheme();
                
                ThemeChanged?.Invoke(this, new ThemeChangedEventArgs(_isHighContrastMode));
            }
        }

        private void ApplyTheme()
        {
            if (Application.Current?.Resources == null) return;

            try
            {
                if (_isHighContrastMode)
                {
                    // Apply high contrast theme
                    if (!Application.Current.Resources.MergedDictionaries.Contains(_highContrastResources))
                    {
                        Application.Current.Resources.MergedDictionaries.Add(_highContrastResources);
                    }

                    // Override WPF-UI theme with high contrast
                    ApplicationThemeManager.Apply(ApplicationTheme.Light); // Use light as base for high contrast
                }
                else
                {
                    // Remove high contrast theme
                    if (Application.Current.Resources.MergedDictionaries.Contains(_highContrastResources))
                    {
                        Application.Current.Resources.MergedDictionaries.Remove(_highContrastResources);
                    }

                    // Apply system theme
                    var systemTheme = ApplicationThemeManager.GetSystemTheme();
                    var appTheme = systemTheme == Wpf.Ui.Appearance.SystemTheme.Dark 
                        ? Wpf.Ui.Appearance.ApplicationTheme.Dark 
                        : Wpf.Ui.Appearance.ApplicationTheme.Light;
                    ApplicationThemeManager.Apply(appTheme);
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                System.Diagnostics.Debug.WriteLine($"Error applying theme: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets whether high contrast mode is currently active
        /// </summary>
        public bool IsHighContrastMode => _isHighContrastMode;

        /// <summary>
        /// Applies high contrast styling to a specific element
        /// </summary>
        /// <param name="element">Element to style</param>
        public void ApplyHighContrastStyling(FrameworkElement element)
        {
            if (element == null || !_isHighContrastMode) return;

            try
            {
                switch (element)
                {
                    case Button button:
                        button.Background = (Brush)_highContrastResources["HighContrastButtonBrush"];
                        button.Foreground = (Brush)_highContrastResources["HighContrastButtonTextBrush"];
                        button.BorderBrush = (Brush)_highContrastResources["HighContrastBorderBrush"];
                        button.BorderThickness = new Thickness(2);
                        break;

                    case TextBlock textBlock:
                        textBlock.Foreground = (Brush)_highContrastResources["HighContrastForegroundBrush"];
                        break;

                    case Control control:
                        control.Background = (Brush)_highContrastResources["HighContrastBackgroundBrush"];
                        control.Foreground = (Brush)_highContrastResources["HighContrastForegroundBrush"];
                        control.BorderBrush = (Brush)_highContrastResources["HighContrastBorderBrush"];
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying high contrast styling: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the appropriate brush for high contrast mode
        /// </summary>
        /// <param name="brushType">Type of brush needed</param>
        /// <returns>High contrast brush or null if not in high contrast mode</returns>
        public Brush? GetHighContrastBrush(HighContrastBrushType brushType)
        {
            if (!_isHighContrastMode) return null;

            var resourceKey = brushType switch
            {
                HighContrastBrushType.Background => "HighContrastBackgroundBrush",
                HighContrastBrushType.Foreground => "HighContrastForegroundBrush",
                HighContrastBrushType.Border => "HighContrastBorderBrush",
                HighContrastBrushType.Selection => "HighContrastSelectionBrush",
                HighContrastBrushType.SelectionText => "HighContrastSelectionTextBrush",
                HighContrastBrushType.Disabled => "HighContrastDisabledBrush",
                HighContrastBrushType.Button => "HighContrastButtonBrush",
                HighContrastBrushType.ButtonText => "HighContrastButtonTextBrush",
                _ => "HighContrastForegroundBrush"
            };

            return _highContrastResources[resourceKey] as Brush ?? null;
        }

        public void Dispose()
        {
            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
            SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
        }
    }

    /// <summary>
    /// Event arguments for theme change events
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public bool IsHighContrastMode { get; }

        public ThemeChangedEventArgs(bool isHighContrastMode)
        {
            IsHighContrastMode = isHighContrastMode;
        }
    }

    /// <summary>
    /// Types of brushes available for high contrast mode
    /// </summary>
    public enum HighContrastBrushType
    {
        Background,
        Foreground,
        Border,
        Selection,
        SelectionText,
        Disabled,
        Button,
        ButtonText
    }
}