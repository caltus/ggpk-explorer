using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGPKExplorer.Models;
using GGPKExplorer.Services;
using Microsoft.Win32;
using Wpf.Ui.Controls;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for the extraction dialog
    /// Reference: docs/LibGGPK3_Deep_Research_Report.md - File Operations section
    /// </summary>
    public partial class ExtractionDialogViewModel : ObservableObject
    {
        private readonly IFileOperationsService _fileOperationsService;
        private CancellationTokenSource? _cancellationTokenSource;

        public ExtractionDialogViewModel(IFileOperationsService fileOperationsService, IEnumerable<TreeNodeInfo> filesToExtract)
        {
            _fileOperationsService = fileOperationsService ?? throw new ArgumentNullException(nameof(fileOperationsService));
            
            FilesToExtract = new ObservableCollection<ExtractionFileItem>(
                filesToExtract.Select(f => new ExtractionFileItem
                {
                    Name = f.Name,
                    FullPath = f.FullPath,
                    SizeFormatted = FormatFileSize(f.Size),
                    Type = f.Type,
                    IconSymbol = GetIconSymbol(f.Type)
                }));

            // Set default options
            PreserveDirectoryStructure = true;
            OverwriteExisting = false;
            CreateDestinationFolder = true;

            UpdateFileCountText();
        }

        [ObservableProperty]
        private string destinationPath = string.Empty;

        [ObservableProperty]
        private bool preserveDirectoryStructure;

        [ObservableProperty]
        private bool overwriteExisting;

        [ObservableProperty]
        private bool createDestinationFolder;

        [ObservableProperty]
        private ObservableCollection<ExtractionFileItem> filesToExtract;

        [ObservableProperty]
        private string fileCountText = string.Empty;

        [ObservableProperty]
        private bool isExtracting;

        [ObservableProperty]
        private double progressPercentage;

        [ObservableProperty]
        private string progressStatus = string.Empty;

        [ObservableProperty]
        private string progressText = string.Empty;

        [ObservableProperty]
        private string currentFile = string.Empty;

        [ObservableProperty]
        private bool hasError;

        [ObservableProperty]
        private string errorMessage = string.Empty;

        [ObservableProperty]
        private bool canExtract;

        partial void OnDestinationPathChanged(string value)
        {
            UpdateCanExtract();
        }

        [RelayCommand]
        private void BrowseDestination()
        {
            try
            {
                var dialog = new OpenFolderDialog
                {
                    Title = "Select destination folder for extraction",
                    Multiselect = false
                };

                if (!string.IsNullOrEmpty(DestinationPath) && Directory.Exists(DestinationPath))
                {
                    dialog.InitialDirectory = DestinationPath;
                }

                if (dialog.ShowDialog() == true)
                {
                    DestinationPath = dialog.FolderName;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to open folder browser: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task StartExtraction()
        {
            if (!CanExtract || IsExtracting)
                return;

            try
            {
                IsExtracting = true;
                HasError = false;
                _cancellationTokenSource = new CancellationTokenSource();

                // Validate destination path
                if (!_fileOperationsService.ValidateDestinationPath(DestinationPath))
                {
                    ShowError("Invalid destination path. Please select a valid folder.");
                    return;
                }

                // Create destination folder if needed
                if (CreateDestinationFolder && !Directory.Exists(DestinationPath))
                {
                    try
                    {
                        Directory.CreateDirectory(DestinationPath);
                    }
                    catch (Exception ex)
                    {
                        ShowError($"Failed to create destination folder: {ex.Message}");
                        return;
                    }
                }

                // Check for existing files if overwrite is disabled
                if (!OverwriteExisting)
                {
                    var existingFiles = FilesToExtract
                        .Where(f => File.Exists(Path.Combine(DestinationPath, f.Name)))
                        .ToList();

                    if (existingFiles.Any())
                    {
                        var fileNames = string.Join(", ", existingFiles.Take(3).Select(f => f.Name));
                        if (existingFiles.Count > 3)
                            fileNames += $" and {existingFiles.Count - 3} more";

                        ShowError($"Files already exist: {fileNames}. Enable 'Overwrite existing files' to continue.");
                        return;
                    }
                }

                // Set up progress reporting
                var progress = new Progress<ProgressInfo>(UpdateProgress);

                // Extract files
                var sourcePaths = FilesToExtract.Select(f => f.FullPath).ToList();
                var results = await _fileOperationsService.ExtractMultipleAsync(
                    sourcePaths, 
                    DestinationPath, 
                    progress, 
                    _cancellationTokenSource.Token);

                // Handle results
                if (results.IsSuccess)
                {
                    ProgressStatus = "Extraction completed successfully";
                    ProgressPercentage = 100;
                    
                    // Show success message and close dialog after a brief delay
                    await Task.Delay(1000, _cancellationTokenSource.Token);
                    
                    // Close dialog (this would need to be handled by the parent)
                    OnExtractionCompleted(results);
                }
                else if (results.IsPartialSuccess)
                {
                    ShowError($"Extraction partially completed. {results.SuccessfulFiles} successful, {results.FailedFiles} failed.");
                }
                else
                {
                    var errorDetails = results.Errors.Length > 0 
                        ? results.Errors.First().ErrorMessage 
                        : "Unknown error occurred";
                    ShowError($"Extraction failed: {errorDetails}");
                }
            }
            catch (OperationCanceledException)
            {
                ProgressStatus = "Extraction cancelled";
                ShowError("Extraction was cancelled by user");
            }
            catch (Exception ex)
            {
                ShowError($"Extraction failed: {ex.Message}");
            }
            finally
            {
                IsExtracting = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        [RelayCommand]
        private void CancelExtraction()
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                ProgressStatus = "Cancelling extraction...";
            }
        }

        private void UpdateProgress(ProgressInfo progressInfo)
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
        }

        private void UpdateFileCountText()
        {
            var fileCount = FilesToExtract.Count(f => f.Type != NodeType.Directory);
            var folderCount = FilesToExtract.Count(f => f.Type == NodeType.Directory);
            
            var parts = new List<string>();
            if (fileCount > 0)
                parts.Add($"{fileCount} file{(fileCount == 1 ? "" : "s")}");
            if (folderCount > 0)
                parts.Add($"{folderCount} folder{(folderCount == 1 ? "" : "s")}");
            
            FileCountText = string.Join(" and ", parts);
        }

        private void UpdateCanExtract()
        {
            CanExtract = !string.IsNullOrWhiteSpace(DestinationPath) && 
                        FilesToExtract.Any() && 
                        !IsExtracting;
        }

        private void ShowError(string message)
        {
            ErrorMessage = message;
            HasError = true;
        }

        private string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 B";
            
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            
            return $"{size:0.##} {sizes[order]}";
        }

        private SymbolRegular GetIconSymbol(NodeType nodeType)
        {
            return nodeType switch
            {
                NodeType.Directory => SymbolRegular.Folder24,
                NodeType.BundleFile => SymbolRegular.Archive24,
                NodeType.CompressedFile => SymbolRegular.Archive24,
                _ => SymbolRegular.Document24
            };
        }

        // Event to notify parent when extraction is completed
        public event EventHandler<ExtractionResults>? ExtractionCompleted;

        private void OnExtractionCompleted(ExtractionResults results)
        {
            ExtractionCompleted?.Invoke(this, results);
        }
    }

    /// <summary>
    /// Represents a file item in the extraction dialog
    /// </summary>
    public class ExtractionFileItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public string SizeFormatted { get; set; } = string.Empty;
        public NodeType Type { get; set; }
        public SymbolRegular IconSymbol { get; set; }
    }
}