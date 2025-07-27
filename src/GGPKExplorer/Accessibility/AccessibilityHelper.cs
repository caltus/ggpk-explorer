using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace GGPKExplorer.Accessibility
{
    /// <summary>
    /// Helper class for accessibility features and automation properties
    /// </summary>
    public static class AccessibilityHelper
    {
        /// <summary>
        /// Sets up accessibility properties for a UI element
        /// </summary>
        /// <param name="element">The UI element to configure</param>
        /// <param name="name">Accessible name</param>
        /// <param name="helpText">Help text for screen readers</param>
        /// <param name="role">Automation control type</param>
        public static void SetupAccessibility(FrameworkElement element, string name, string? helpText = null, AutomationControlType? role = null)
        {
            if (element == null) return;

            // Set accessible name
            if (!string.IsNullOrEmpty(name))
            {
                AutomationProperties.SetName(element, name);
            }

            // Set help text
            if (!string.IsNullOrEmpty(helpText))
            {
                AutomationProperties.SetHelpText(element, helpText);
            }

            // Set automation control type
            if (role.HasValue)
            {
                AutomationProperties.SetAutomationId(element, $"{role.Value}_{name?.Replace(" ", "_")}");
            }

            // Ensure element is accessible
            AutomationProperties.SetIsOffscreenBehavior(element, IsOffscreenBehavior.FromClip);
        }

        /// <summary>
        /// Sets up keyboard navigation for a container
        /// </summary>
        /// <param name="container">Container element</param>
        /// <param name="isTabStop">Whether container should be a tab stop</param>
        public static void SetupKeyboardNavigation(FrameworkElement container, bool isTabStop = true)
        {
            if (container == null) return;

            if (container is Control control)
            {
                control.IsTabStop = isTabStop;
            }
            container.Focusable = true;

            // Set keyboard navigation mode
            KeyboardNavigation.SetTabNavigation(container, KeyboardNavigationMode.Continue);
            KeyboardNavigation.SetDirectionalNavigation(container, KeyboardNavigationMode.Continue);
            KeyboardNavigation.SetControlTabNavigation(container, KeyboardNavigationMode.Continue);
        }

        /// <summary>
        /// Sets up high contrast theme support
        /// </summary>
        /// <param name="element">Element to configure</param>
        public static void SetupHighContrastSupport(FrameworkElement element)
        {
            if (element == null) return;

            // Listen for system theme changes
            SystemEvents.UserPreferenceChanged += (sender, e) =>
            {
                if (e.Category == Microsoft.Win32.UserPreferenceCategory.Accessibility)
                {
                    Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateHighContrastColors(element);
                    }));
                }
            };

            // Initial setup
            UpdateHighContrastColors(element);
        }

        /// <summary>
        /// Updates colors for high contrast mode
        /// </summary>
        /// <param name="element">Element to update</param>
        private static void UpdateHighContrastColors(FrameworkElement element)
        {
            if (SystemParameters.HighContrast)
            {
                // Apply high contrast colors
                if (element is Control control)
                {
                    control.Foreground = new SolidColorBrush(System.Windows.SystemColors.WindowTextColor);
                    control.Background = new SolidColorBrush(System.Windows.SystemColors.WindowColor);
                }
                else if (element is TextBlock textBlock)
                {
                    textBlock.Foreground = new SolidColorBrush(System.Windows.SystemColors.WindowTextColor);
                }
            }
        }

        /// <summary>
        /// Announces text to screen readers
        /// </summary>
        /// <param name="text">Text to announce</param>
        /// <param name="priority">Announcement priority</param>
        public static void AnnounceToScreenReader(string text, AutomationNotificationKind priority = AutomationNotificationKind.ItemAdded)
        {
            if (string.IsNullOrEmpty(text)) return;

            try
            {
                // Create a temporary element for the announcement
                var announcer = new TextBlock { Text = text, Visibility = Visibility.Collapsed };
                
                if (Application.Current.MainWindow != null)
                {
                    var mainGrid = Application.Current.MainWindow.Content as Grid;
                    if (mainGrid != null)
                    {
                        mainGrid.Children.Add(announcer);
                        
                        // Announce the text
                        var peer = UIElementAutomationPeer.FromElement(announcer);
                        if (peer != null)
                        {
                            peer.RaiseNotificationEvent(
                                priority,
                                AutomationNotificationProcessing.ImportantMostRecent,
                                text,
                                Guid.NewGuid().ToString()
                            );
                        }
                        
                        // Remove the temporary element
                        Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            mainGrid.Children.Remove(announcer);
                        }), System.Windows.Threading.DispatcherPriority.Background);
                    }
                }
            }
            catch (Exception)
            {
                // Silently fail if announcement doesn't work
            }
        }

        /// <summary>
        /// Sets up live region for dynamic content updates
        /// </summary>
        /// <param name="element">Element to configure as live region</param>
        /// <param name="politeness">Live region politeness level</param>
        public static void SetupLiveRegion(FrameworkElement element, AutomationLiveSetting politeness = AutomationLiveSetting.Polite)
        {
            if (element == null) return;

            AutomationProperties.SetLiveSetting(element, politeness);
            AutomationProperties.SetIsOffscreenBehavior(element, IsOffscreenBehavior.FromClip);
        }

        /// <summary>
        /// Gets the high contrast color for a specific system color
        /// </summary>
        /// <param name="colorType">Type of system color</param>
        /// <returns>High contrast brush</returns>
        public static Brush? GetHighContrastBrush(string colorType)
        {
            if (!SystemParameters.HighContrast)
                return null;

            return colorType switch
            {
                "WindowText" => new SolidColorBrush(System.Windows.SystemColors.WindowTextColor),
                "Window" => new SolidColorBrush(System.Windows.SystemColors.WindowColor),
                "Highlight" => new SolidColorBrush(System.Windows.SystemColors.HighlightColor),
                "HighlightText" => new SolidColorBrush(System.Windows.SystemColors.HighlightTextColor),
                "GrayText" => new SolidColorBrush(System.Windows.SystemColors.GrayTextColor),
                _ => new SolidColorBrush(System.Windows.SystemColors.WindowTextColor)
            };
        }
    }

    /// <summary>
    /// System colors enumeration for high contrast support
    /// </summary>
    public enum SystemColors
    {
        WindowText,
        Window,
        Highlight,
        HighlightText,
        GrayText
    }
}