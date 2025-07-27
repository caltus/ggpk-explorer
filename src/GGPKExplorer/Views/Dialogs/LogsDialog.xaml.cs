using System;
using System.Windows;
using GGPKExplorer.ViewModels;
using Wpf.Ui.Controls;

namespace GGPKExplorer.Views.Dialogs
{
    /// <summary>
    /// Logs Dialog for viewing application logs
    /// </summary>
    public partial class LogsDialog : FluentWindow
    {
        private LogViewerViewModel? _logViewerViewModel;

        public LogsDialog()
        {
            System.Diagnostics.Debug.WriteLine("LogsDialog: Constructor starting");
            
            try
            {
                InitializeComponent();
                System.Diagnostics.Debug.WriteLine("LogsDialog: InitializeComponent completed");
                
                // Initialize the log viewer
                InitializeLogViewer();
                
                System.Diagnostics.Debug.WriteLine("LogsDialog: Constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogsDialog: ERROR in constructor: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"LogsDialog: StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Initializes the log viewer control with its ViewModel
        /// </summary>
        private void InitializeLogViewer()
        {
            try
            {
                _logViewerViewModel = new LogViewerViewModel();
                LogViewerControl.DataContext = _logViewerViewModel;
                System.Diagnostics.Debug.WriteLine("LogsDialog: Log viewer initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogsDialog: Error initializing log viewer: {ex.Message}");
                throw;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("LogsDialog: Close button clicked");
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogsDialog: Error closing dialog: {ex.Message}");
                Close(); // Force close even if there's an error
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("LogsDialog: OnClosing called");
                base.OnClosing(e);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogsDialog: Error in OnClosing: {ex.Message}");
                // Don't cancel closing due to errors
            }
        }

        /// <summary>
        /// Handles mouse down on header for dragging the window
        /// </summary>
        private void Header_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (e.ButtonState == System.Windows.Input.MouseButtonState.Pressed)
                {
                    System.Diagnostics.Debug.WriteLine("LogsDialog: Starting window drag");
                    this.DragMove();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"LogsDialog: Error during drag: {ex.Message}");
                // Don't throw, just log the error
            }
        }
    }
}