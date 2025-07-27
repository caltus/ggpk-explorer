using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using GGPKExplorer.Models;
using GGPKExplorer.ViewModels;
using GGPKExplorer.Views.Dialogs;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for managing file extraction operations with UI dialogs
    /// Reference: docs/LibGGPK3_Deep_Research_Report.md - File Operations section
    /// </summary>
    public class ExtractionService : IExtractionService
    {
        private readonly IFileOperationsService _fileOperationsService;

        public ExtractionService(IFileOperationsService fileOperationsService)
        {
            _fileOperationsService = fileOperationsService ?? throw new ArgumentNullException(nameof(fileOperationsService));
        }

        public async Task<ExtractionResults?> ShowExtractionDialogAsync(IEnumerable<TreeNodeInfo> filesToExtract, Window? owner = null)
        {
            try
            {
                var files = filesToExtract.ToList();
                if (!files.Any())
                    return null;

                // For now, use a simple folder browser dialog and extract directly
                // TODO: Implement full extraction dialog in a future iteration
                var dialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "Select destination folder for extraction",
                    Multiselect = false
                };

                if (dialog.ShowDialog() == true)
                {
                    var destinationPath = dialog.FolderName;
                    
                    // Extract files with progress dialog
                    return await ExtractWithProgressAsync(files, destinationPath, owner);
                }

                return null;
            }
            catch (Exception ex)
            {
                // Log error and show message to user
                System.Windows.MessageBox.Show(
                    $"Failed to show extraction dialog: {ex.Message}",
                    "Extraction Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                
                return null;
            }
        }

        public Task<IProgressDialog> ShowProgressDialogAsync(string title, string operationTitle, Window? owner = null)
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var viewModel = new ProgressDialogViewModel(title, operationTitle, cancellationTokenSource);
            var dialog = new ProgressDialog(viewModel);

            if (owner != null)
            {
                // Set the dialog's parent window for proper positioning
                // Note: WPF-UI FluentWindow should support Owner property
                if (dialog is Window window)
                {
                    window.Owner = owner;
                }
            }

            // Show dialog asynchronously
            dialog.Show();

            // Return wrapper that implements IProgressDialog
            return Task.FromResult<IProgressDialog>(new ProgressDialogWrapper(dialog, viewModel, cancellationTokenSource));
        }

        public async Task<ExtractionResults> ExtractWithProgressAsync(IEnumerable<TreeNodeInfo> filesToExtract, string destinationPath, Window? owner = null)
        {
            var files = filesToExtract.ToList();
            var fileCount = files.Count;
            var progressDialog = await ShowProgressDialogAsync(
                "Extracting Files", 
                $"Extracting {fileCount} item{(fileCount == 1 ? "" : "s")}...", 
                owner);

            try
            {
                var cancellationToken = ((ProgressDialogWrapper)progressDialog).CancellationToken;
                var progress = new Progress<ProgressInfo>(progressDialog.UpdateProgress);

                // Extract files
                var sourcePaths = files.Select(f => f.FullPath).ToList();
                var results = await _fileOperationsService.ExtractMultipleAsync(
                    sourcePaths, 
                    destinationPath, 
                    progress, 
                    cancellationToken);

                // Update dialog based on results
                if (results.IsSuccess)
                {
                    progressDialog.CompleteOperation(true, $"Successfully extracted {results.SuccessfulFiles} files");
                }
                else if (results.IsPartialSuccess)
                {
                    progressDialog.CompleteOperation(false, $"Partially completed: {results.SuccessfulFiles} successful, {results.FailedFiles} failed");
                    
                    // Add error details
                    foreach (var error in results.Errors.Take(5)) // Show first 5 errors
                    {
                        progressDialog.AddError($"{error.FilePath}: {error.ErrorMessage}");
                    }
                    
                    if (results.Errors.Length > 5)
                    {
                        progressDialog.AddDetail($"... and {results.Errors.Length - 5} more errors");
                    }
                }
                else
                {
                    progressDialog.CompleteOperation(false, "Extraction failed");
                    
                    // Add error details
                    foreach (var error in results.Errors.Take(10))
                    {
                        progressDialog.AddError($"{error.FilePath}: {error.ErrorMessage}");
                    }
                }

                return results;
            }
            catch (OperationCanceledException)
            {
                progressDialog.CompleteOperation(false, "Extraction was cancelled");
                
                return new ExtractionResults
                {
                    SuccessfulFiles = 0,
                    FailedFiles = files.Count,
                    TotalFiles = files.Count,
                    Errors = new[] { new ExtractionError { ErrorMessage = "Operation was cancelled by user" } }
                };
            }
            catch (Exception ex)
            {
                progressDialog.CompleteOperation(false, $"Extraction failed: {ex.Message}");
                progressDialog.AddError(ex.Message);
                
                return new ExtractionResults
                {
                    SuccessfulFiles = 0,
                    FailedFiles = files.Count,
                    TotalFiles = files.Count,
                    Errors = new[] { new ExtractionError { ErrorMessage = ex.Message, Exception = ex } }
                };
            }
        }

        /// <summary>
        /// Wrapper class that implements IProgressDialog interface
        /// </summary>
        private class ProgressDialogWrapper : IProgressDialog
        {
            private readonly ProgressDialog _dialog;
            private readonly ProgressDialogViewModel _viewModel;
            private readonly CancellationTokenSource _cancellationTokenSource;

            public ProgressDialogWrapper(ProgressDialog dialog, ProgressDialogViewModel viewModel, CancellationTokenSource cancellationTokenSource)
            {
                _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
                _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                _cancellationTokenSource = cancellationTokenSource ?? throw new ArgumentNullException(nameof(cancellationTokenSource));

                // Forward cancellation events
                _cancellationTokenSource.Token.Register(() => Cancelled?.Invoke(this, EventArgs.Empty));
            }

            public CancellationToken CancellationToken => _cancellationTokenSource.Token;

            public event EventHandler? Cancelled;

            public void UpdateProgress(ProgressInfo progressInfo)
            {
                Application.Current.Dispatcher.Invoke(() => _viewModel.UpdateProgress(progressInfo));
            }

            public void CompleteOperation(bool success, string? message = null)
            {
                Application.Current.Dispatcher.Invoke(() => _viewModel.CompleteOperation(success, message));
            }

            public void AddError(string error)
            {
                Application.Current.Dispatcher.Invoke(() => _viewModel.AddError(error));
            }

            public void AddDetail(string detail)
            {
                Application.Current.Dispatcher.Invoke(() => _viewModel.AddDetail(detail));
            }

            public void Close()
            {
                Application.Current.Dispatcher.Invoke(() => _dialog.Close());
            }

            public void Dispose()
            {
                _cancellationTokenSource?.Dispose();
                _dialog?.Close();
            }
        }
    }
}