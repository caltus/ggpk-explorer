using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Wpf.Ui.Controls;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for displaying floating toast notifications that don't affect window layout
    /// </summary>
    public class FloatingToastService : IToastService
    {
        private readonly List<Popup> _activeToasts = new();
        private const double ToastSpacing = 10;
        private const double BottomMargin = 80;

        public void ShowSuccess(string message, string? title = null, int timeout = 4000)
        {
            ShowFloatingToast(message, title, ControlAppearance.Success, timeout);
        }

        public void ShowError(string message, string? title = null, int timeout = 6000)
        {
            ShowFloatingToast(message, title, ControlAppearance.Danger, timeout);
        }

        public void ShowInfo(string message, string? title = null, int timeout = 3000)
        {
            ShowFloatingToast(message, title, ControlAppearance.Info, timeout);
        }

        public void ShowWarning(string message, string? title = null, int timeout = 5000)
        {
            ShowFloatingToast(message, title, ControlAppearance.Caution, timeout);
        }

        private void ShowFloatingToast(string message, string? title, ControlAppearance appearance, int timeout)
        {
            // Ensure we're on the UI thread
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(() => ShowFloatingToast(message, title, appearance, timeout));
                return;
            }

            try
            {
                var mainWindow = Application.Current.MainWindow;
                if (mainWindow == null) 
                {
                    System.Diagnostics.Debug.WriteLine("FloatingToastService: MainWindow is null, cannot show toast");
                    return;
                }

                // Wait for window to be loaded and positioned
                if (!mainWindow.IsLoaded || mainWindow.ActualWidth == 0 || mainWindow.ActualHeight == 0)
                {
                    System.Diagnostics.Debug.WriteLine("FloatingToastService: MainWindow not ready, deferring toast");
                    mainWindow.Dispatcher.BeginInvoke(() => ShowFloatingToast(message, title, appearance, timeout), DispatcherPriority.Loaded);
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"FloatingToastService: Showing toast - {title}: {message}");

                // Create the toast content
                var toastContent = CreateToastContent(message, title, appearance);
                
                // Create popup
                var popup = new Popup
                {
                    Child = toastContent,
                    AllowsTransparency = true,
                    PopupAnimation = PopupAnimation.Fade,
                    Placement = PlacementMode.Absolute,
                    StaysOpen = true,
                    IsOpen = false
                };

                // Position the popup
                PositionPopup(popup, mainWindow);

                // Add to active toasts
                _activeToasts.Add(popup);

                System.Diagnostics.Debug.WriteLine($"FloatingToastService: Popup positioned at ({popup.HorizontalOffset}, {popup.VerticalOffset})");

                // Show with animation
                popup.IsOpen = true;
                AnimateIn(toastContent);

                // Auto-hide after timeout
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(timeout)
                };
                timer.Tick += (s, e) =>
                {
                    timer.Stop();
                    HideToast(popup, toastContent);
                };
                timer.Start();

                System.Diagnostics.Debug.WriteLine($"FloatingToastService: Toast shown successfully, will hide in {timeout}ms");
            }
            catch (Exception ex)
            {
                // Fallback to system notification
                System.Diagnostics.Debug.WriteLine($"FloatingToastService: Exception showing toast: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"FloatingToastService: Stack trace: {ex.StackTrace}");
                
                try
                {
                    System.Windows.MessageBox.Show($"{title ?? GetDefaultTitle(appearance)}\n\n{message}", 
                                   "Notification", 
                                   System.Windows.MessageBoxButton.OK, 
                                   GetMessageBoxImage(appearance));
                }
                catch (Exception fallbackEx)
                {
                    System.Diagnostics.Debug.WriteLine($"FloatingToastService: Even fallback MessageBox failed: {fallbackEx.Message}");
                }
            }
        }

        private Border CreateToastContent(string message, string? title, ControlAppearance appearance)
        {
            var border = new Border
            {
                Background = GetBackgroundBrush(appearance),
                BorderBrush = GetBorderBrush(appearance),
                BorderThickness = new Thickness(1, 1, 1, 1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16, 12, 16, 12),
                MinWidth = 300,
                MaxWidth = 500,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Black,
                    Direction = 270,
                    ShadowDepth = 4,
                    BlurRadius = 12,
                    Opacity = 0.3
                }
            };

            var stackPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            // Add icon
            var icon = new SymbolIcon
            {
                Symbol = GetIcon(appearance),
                FontSize = 16,
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = GetForegroundBrush(appearance)
            };
            stackPanel.Children.Add(icon);

            // Add content
            var contentPanel = new StackPanel();

            if (!string.IsNullOrEmpty(title))
            {
                var titleBlock = new System.Windows.Controls.TextBlock
                {
                    Text = title,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 14,
                    Foreground = GetForegroundBrush(appearance),
                    Margin = new Thickness(0, 0, 0, 4)
                };
                contentPanel.Children.Add(titleBlock);
            }

            var messageBlock = new System.Windows.Controls.TextBlock
            {
                Text = message,
                FontSize = 13,
                Foreground = GetForegroundBrush(appearance),
                TextWrapping = TextWrapping.Wrap
            };
            contentPanel.Children.Add(messageBlock);

            stackPanel.Children.Add(contentPanel);
            border.Child = stackPanel;

            return border;
        }

        private void PositionPopup(Popup popup, Window mainWindow)
        {
            try
            {
                // Get screen working area to ensure popup stays on screen
                var workingArea = SystemParameters.WorkArea;
                
                // Calculate position relative to main window
                var windowRect = new Rect(mainWindow.Left, mainWindow.Top, mainWindow.ActualWidth, mainWindow.ActualHeight);
                
                // Ensure window rect is valid
                if (windowRect.Width <= 0 || windowRect.Height <= 0)
                {
                    System.Diagnostics.Debug.WriteLine("FloatingToastService: Invalid window dimensions, using screen center");
                    windowRect = new Rect(workingArea.Left + workingArea.Width / 4, workingArea.Top + workingArea.Height / 4, 
                                         workingArea.Width / 2, workingArea.Height / 2);
                }
                
                // Position at bottom center of the window
                var toastWidth = 500; // Max width from CreateToastContent
                var x = windowRect.Left + (windowRect.Width / 2) - (toastWidth / 2);
                var y = windowRect.Bottom - BottomMargin - ((_activeToasts.Count) * (60 + ToastSpacing));

                // Ensure popup stays within screen bounds
                x = Math.Max(workingArea.Left + 10, Math.Min(x, workingArea.Right - toastWidth - 10));
                y = Math.Max(workingArea.Top + 10, Math.Min(y, workingArea.Bottom - 100));

                popup.HorizontalOffset = x;
                popup.VerticalOffset = y;
                
                System.Diagnostics.Debug.WriteLine($"FloatingToastService: Window rect: {windowRect}, Toast position: ({x}, {y})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FloatingToastService: Error positioning popup: {ex.Message}");
                // Fallback to screen center
                var workingArea = SystemParameters.WorkArea;
                popup.HorizontalOffset = workingArea.Left + workingArea.Width / 2 - 250;
                popup.VerticalOffset = workingArea.Top + workingArea.Height - 200;
            }
        }

        private void AnimateIn(FrameworkElement element)
        {
            element.Opacity = 0;
            element.RenderTransform = new TranslateTransform(0, 20);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
            var slideIn = new DoubleAnimation(20, 0, TimeSpan.FromMilliseconds(300))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            element.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);
        }

        private void HideToast(Popup popup, FrameworkElement content)
        {
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200));
            var slideOut = new DoubleAnimation(0, -20, TimeSpan.FromMilliseconds(200));

            fadeOut.Completed += (s, e) =>
            {
                popup.IsOpen = false;
                _activeToasts.Remove(popup);
                RepositionToasts();
            };

            content.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            content.RenderTransform.BeginAnimation(TranslateTransform.YProperty, slideOut);
        }

        private void RepositionToasts()
        {
            if (Application.Current.MainWindow == null) return;

            for (int i = 0; i < _activeToasts.Count; i++)
            {
                var popup = _activeToasts[i];
                var mainWindow = Application.Current.MainWindow;
                var windowRect = new Rect(mainWindow.Left, mainWindow.Top, mainWindow.ActualWidth, mainWindow.ActualHeight);
                
                var x = windowRect.Left + (windowRect.Width / 2) - 250;
                var y = windowRect.Bottom - BottomMargin - (i * (60 + ToastSpacing));

                popup.HorizontalOffset = x;
                popup.VerticalOffset = y;
            }
        }

        private static Brush GetBackgroundBrush(ControlAppearance appearance)
        {
            return appearance switch
            {
                ControlAppearance.Success => new SolidColorBrush(Color.FromRgb(16, 124, 16)),
                ControlAppearance.Danger => new SolidColorBrush(Color.FromRgb(196, 43, 28)),
                ControlAppearance.Info => new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                ControlAppearance.Caution => new SolidColorBrush(Color.FromRgb(157, 93, 0)),
                _ => new SolidColorBrush(Color.FromRgb(72, 70, 68))
            };
        }

        private static Brush GetBorderBrush(ControlAppearance appearance)
        {
            return appearance switch
            {
                ControlAppearance.Success => new SolidColorBrush(Color.FromRgb(20, 140, 20)),
                ControlAppearance.Danger => new SolidColorBrush(Color.FromRgb(220, 53, 38)),
                ControlAppearance.Info => new SolidColorBrush(Color.FromRgb(10, 130, 220)),
                ControlAppearance.Caution => new SolidColorBrush(Color.FromRgb(177, 113, 10)),
                _ => new SolidColorBrush(Color.FromRgb(92, 90, 88))
            };
        }

        private static Brush GetForegroundBrush(ControlAppearance appearance)
        {
            return new SolidColorBrush(Colors.White);
        }

        private static SymbolRegular GetIcon(ControlAppearance appearance)
        {
            return appearance switch
            {
                ControlAppearance.Success => SymbolRegular.CheckmarkCircle24,
                ControlAppearance.Danger => SymbolRegular.ErrorCircle24,
                ControlAppearance.Info => SymbolRegular.Info24,
                ControlAppearance.Caution => SymbolRegular.Warning24,
                _ => SymbolRegular.Info24
            };
        }

        private static string GetDefaultTitle(ControlAppearance appearance)
        {
            return appearance switch
            {
                ControlAppearance.Success => "Success",
                ControlAppearance.Danger => "Error",
                ControlAppearance.Info => "Information",
                ControlAppearance.Caution => "Warning",
                _ => "Notification"
            };
        }

        private static System.Windows.MessageBoxImage GetMessageBoxImage(ControlAppearance appearance)
        {
            return appearance switch
            {
                ControlAppearance.Success => System.Windows.MessageBoxImage.Information,
                ControlAppearance.Danger => System.Windows.MessageBoxImage.Error,
                ControlAppearance.Info => System.Windows.MessageBoxImage.Information,
                ControlAppearance.Caution => System.Windows.MessageBoxImage.Warning,
                _ => System.Windows.MessageBoxImage.Information
            };
        }
    }
}