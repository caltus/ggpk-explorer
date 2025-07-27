using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;
using GGPKExplorer.Models;

namespace GGPKExplorer.Views.Dialogs
{
    /// <summary>
    /// User-friendly error dialog for displaying comprehensive error information
    /// Reference: Requirements 8.1 - Global exception handler with user-friendly error dialogs
    /// </summary>
    public partial class ErrorDialog : ContentDialog
    {
        private Exception? _exception;
        private string _context = "";
        private string _diagnosticInfo = "";
        private bool _showRecoveryOptions;

        public ErrorDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Configures the error dialog with exception information
        /// </summary>
        /// <param name="title">Error dialog title</param>
        /// <param name="message">User-friendly error message</param>
        /// <param name="exception">The exception that occurred</param>
        /// <param name="context">Context information about where the error occurred</param>
        /// <param name="diagnosticInfo">Diagnostic information about the system state</param>
        /// <param name="showRecoveryOptions">Whether to show recovery options</param>
        public void ConfigureError(string title, string message, Exception exception, string context = "", string diagnosticInfo = "", bool showRecoveryOptions = false)
        {
            _exception = exception;
            _context = context;
            _diagnosticInfo = diagnosticInfo;
            _showRecoveryOptions = showRecoveryOptions;

            // Set dialog title and message
            Title = title;
            ErrorTitleTextBlock.Text = title;
            ErrorMessageTextBlock.Text = message;

            // Show context if provided
            if (!string.IsNullOrEmpty(context))
            {
                ContextTextBlock.Text = $"Context: {context}";
                ContextTextBlock.Visibility = Visibility.Visible;
            }

            // Show recovery options if requested
            if (showRecoveryOptions)
            {
                RecoveryOptionsPanel.Visibility = Visibility.Visible;
                
                // Configure recovery options based on exception type
                ConfigureRecoveryOptions(exception);
            }

            // Prepare technical details
            PrepareTechnicalDetails();
        }

        /// <summary>
        /// Configures recovery options based on the exception type
        /// </summary>
        /// <param name="exception">The exception that occurred</param>
        private void ConfigureRecoveryOptions(Exception exception)
        {
            // Show different recovery options based on exception type
            switch (exception)
            {
                case GGPKCorruptedException:
                    RetryButton.Content = "Try Partial Recovery";
                    RetryButton.ToolTip = "Attempt to recover readable portions of the file";
                    IgnoreButton.Content = "Continue with Limited Functionality";
                    IgnoreButton.ToolTip = "Continue without the corrupted file";
                    break;
                
                case BundleDecompressionException:
                    RetryButton.Content = "Retry Without Bundles";
                    RetryButton.ToolTip = "Continue using only GGPK data without bundle decompression";
                    IgnoreButton.Content = "Skip Bundle Files";
                    IgnoreButton.ToolTip = "Continue without bundle file support";
                    break;
                
                case FileOperationException fileOpEx when fileOpEx.OperationType == FileOperationType.Extract:
                    RetryButton.Content = "Retry Extraction";
                    RetryButton.ToolTip = "Try the extraction operation again";
                    IgnoreButton.Content = "Skip This File";
                    IgnoreButton.ToolTip = "Continue extraction without this file";
                    break;
                
                case OutOfMemoryException:
                    RetryButton.Content = "Free Memory and Retry";
                    RetryButton.ToolTip = "Force garbage collection and retry the operation";
                    IgnoreButton.Content = "Work with Smaller Files";
                    IgnoreButton.ToolTip = "Recommendation to use smaller GGPK files";
                    break;
                
                default:
                    RetryButton.Content = "Retry Operation";
                    IgnoreButton.Content = "Continue";
                    break;
            }
        }

        /// <summary>
        /// Prepares technical details for display
        /// </summary>
        private void PrepareTechnicalDetails()
        {
            if (_exception == null) return;

            var details = $"Exception Type: {_exception.GetType().FullName}\n";
            details += $"Message: {_exception.Message}\n";
            
            if (!string.IsNullOrEmpty(_context))
            {
                details += $"Context: {_context}\n";
            }
            
            details += $"Stack Trace:\n{_exception.StackTrace}\n";
            
            if (_exception.InnerException != null)
            {
                details += $"\nInner Exception: {_exception.InnerException.GetType().FullName}\n";
                details += $"Inner Message: {_exception.InnerException.Message}\n";
                details += $"Inner Stack Trace:\n{_exception.InnerException.StackTrace}\n";
            }
            
            if (!string.IsNullOrEmpty(_diagnosticInfo))
            {
                details += $"\nDiagnostic Information:\n{_diagnosticInfo}";
            }

            TechnicalDetailsTextBlock.Text = details;
            TechnicalDetailsExpander.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Handles the retry button click
        /// </summary>
        private void RetryButton_Click(object sender, RoutedEventArgs e)
        {
            // Set dialog result to indicate retry was requested
            Hide(Wpf.Ui.Controls.ContentDialogResult.Secondary);
        }

        /// <summary>
        /// Handles the ignore/continue button click
        /// </summary>
        private void IgnoreButton_Click(object sender, RoutedEventArgs e)
        {
            // Set dialog result to indicate ignore was requested
            Hide(Wpf.Ui.Controls.ContentDialogResult.Primary);
        }

        /// <summary>
        /// Handles the open log button click
        /// </summary>
        private void OpenLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Open the log directory
                var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GGPKExplorer", "Logs");
                if (Directory.Exists(logDir))
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = logDir,
                        UseShellExecute = true
                    });
                }
                else
                {
                    System.Windows.MessageBox.Show("Log directory not found.", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to open log directory: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Gets the full error information for copying to clipboard
        /// </summary>
        /// <returns>Complete error information as string</returns>
        public string GetFullErrorInfo()
        {
            var info = $"{Title}\n";
            info += $"{ErrorMessageTextBlock.Text}\n\n";
            
            if (!string.IsNullOrEmpty(_context))
            {
                info += $"Context: {_context}\n\n";
            }
            
            info += $"Technical Details:\n{TechnicalDetailsTextBlock.Text}\n\n";
            
            if (!string.IsNullOrEmpty(_diagnosticInfo))
            {
                info += $"System Information:\n{_diagnosticInfo}";
            }
            
            return info;
        }
    }
}