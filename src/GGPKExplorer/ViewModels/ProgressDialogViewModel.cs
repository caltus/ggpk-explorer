using System;
using System.Text;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGPKExplorer.Services;
using Wpf.Ui.Controls;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for the progress dialog used during file operations
    /// </summary>
    public partial class ProgressDialogViewModel : ObservableObject
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly StringBuilder _detailsBuilder;

        public ProgressDialogViewModel(string title, string operationTitle, CancellationTokenSource cancellationTokenSource)
        {
            Title = title;
            OperationTitle = operationTitle;
            _cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));
            _detailsBuilder = new StringBuilder();
            
            CancelButtonText = "Cancel";
            CancelButtonAppearance = ControlAppearance.Secondary;
            IsOperationInProgress = true;
            CanToggleDetails = true;
        }

        [ObservableProperty]
        private string title = string.Empty;

        [ObservableProperty]
        private string operationTitle = string.Empty;

        [ObservableProperty]
        private double progressPercentage;

        [ObservableProperty]
        private string progressStatus = string.Empty;

        [ObservableProperty]
        private string progressText = string.Empty;

        [ObservableProperty]
        private string currentFile = string.Empty;

        [ObservableProperty]
        private bool hasCurrentFile;

        [ObservableProperty]
        private bool showDetails;

        [ObservableProperty]
        private string detailsText = string.Empty;

        [ObservableProperty]
        private bool canToggleDetails;

        [ObservableProperty]
        private string cancelButtonText;

        [ObservableProperty]
        private ControlAppearance cancelButtonAppearance;

        [ObservableProperty]
        private bool isOperationInProgress;

        partial void OnCurrentFileChanged(string value)
        {
            HasCurrentFile = !string.IsNullOrEmpty(value);
        }

        [RelayCommand]
        private void ToggleDetails()
        {
            ShowDetails = !ShowDetails;
        }

        [RelayCommand]
        private void Cancel()
        {
            if (IsOperationInProgress && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                ProgressStatus = "Cancelling operation...";
                CancelButtonText = "Cancelling...";
                CancelButtonAppearance = ControlAppearance.Secondary;
                
                AddDetail("Operation cancelled by user");
            }
            else if (!IsOperationInProgress)
            {
                // Close dialog
                OnCloseRequested();
            }
        }

        /// <summary>
        /// Updates the progress information
        /// </summary>
        public void UpdateProgress(ProgressInfo progressInfo)
        {
            ProgressPercentage = progressInfo.Percentage;
            ProgressStatus = progressInfo.Status ?? progressInfo.Operation;
            CurrentFile = progressInfo.CurrentFile ?? string.Empty;
            
            if (progressInfo.TotalFiles > 0)
            {
                ProgressText = $"{progressInfo.FilesProcessed}/{progressInfo.TotalFiles} files";
            }
            else if (progressInfo.TotalBytes > 0)
            {
                var processedMB = progressInfo.BytesProcessed / (1024.0 * 1024.0);
                var totalMB = progressInfo.TotalBytes / (1024.0 * 1024.0);
                ProgressText = $"{processedMB:F1}/{totalMB:F1} MB";
            }
            else
            {
                ProgressText = $"{progressInfo.Percentage:F1}%";
            }

            // Add to details if significant progress
            if (!string.IsNullOrEmpty(progressInfo.CurrentFile))
            {
                AddDetail($"Processing: {progressInfo.CurrentFile}");
            }
        }

        /// <summary>
        /// Marks the operation as completed
        /// </summary>
        public void CompleteOperation(bool success, string? message = null)
        {
            IsOperationInProgress = false;
            ProgressPercentage = 100;
            
            if (success)
            {
                ProgressStatus = message ?? "Operation completed successfully";
                CancelButtonText = "Close";
                CancelButtonAppearance = ControlAppearance.Primary;
                AddDetail("Operation completed successfully");
            }
            else
            {
                ProgressStatus = message ?? "Operation failed";
                CancelButtonText = "Close";
                CancelButtonAppearance = ControlAppearance.Primary;
                AddDetail($"Operation failed: {message}");
            }
        }

        /// <summary>
        /// Adds an error message to the operation
        /// </summary>
        public void AddError(string error)
        {
            AddDetail($"ERROR: {error}");
        }

        /// <summary>
        /// Adds a detail message to the log
        /// </summary>
        public void AddDetail(string detail)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            _detailsBuilder.AppendLine($"[{timestamp}] {detail}");
            DetailsText = _detailsBuilder.ToString();
        }

        /// <summary>
        /// Event raised when the dialog should be closed
        /// </summary>
        public event EventHandler? CloseRequested;

        private void OnCloseRequested()
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}