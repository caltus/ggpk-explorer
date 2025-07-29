using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGPKExplorer.Models;
using GGPKExplorer.Services;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for a tree node in the navigation tree
    /// </summary>
    public partial class TreeNodeViewModel : ObservableObject
    {
        private readonly IGGPKService _ggpkService;
        private readonly IFileOperationsService _fileOperationsService;
        private CancellationTokenSource? _loadCancellationTokenSource;

        /// <summary>
        /// The underlying tree node information
        /// </summary>
        public TreeNodeInfo NodeInfo { get; }

        /// <summary>
        /// Collection of child nodes
        /// </summary>
        public ObservableCollection<TreeNodeViewModel> Children { get; }

        /// <summary>
        /// Parent node (null for root)
        /// </summary>
        public TreeNodeViewModel? Parent { get; }

        [ObservableProperty]
        private bool _isExpanded;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isMultiSelected;

        /// <summary>
        /// Event raised when this node is selected
        /// </summary>
        public event EventHandler<TreeNodeSelectedEventArgs>? NodeSelected;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private bool _hasLoadedChildren;

        [ObservableProperty]
        private bool _isVisible = true;

        /// <summary>
        /// Gets whether this node can be expanded (has children)
        /// </summary>
        public bool CanExpand => NodeInfo.Type == NodeType.Directory && NodeInfo.HasChildren;

        /// <summary>
        /// Gets the display name for the node
        /// </summary>
        public string DisplayName => NodeInfo.Name;

        /// <summary>
        /// Gets the icon path for the node
        /// </summary>
        public string IconPath => GetIconPath();

        /// <summary>
        /// Gets whether to show file details for this node
        /// </summary>
        public bool ShowFileDetails => NodeInfo.Type != NodeType.Directory;

        /// <summary>
        /// Command to expand/collapse the node
        /// </summary>
        public ICommand ToggleExpandCommand { get; }

        /// <summary>
        /// Command to extract the node
        /// </summary>
        public ICommand ExtractCommand { get; }

        /// <summary>
        /// Command to show properties
        /// </summary>
        public ICommand PropertiesCommand { get; }



        /// <summary>
        /// Gets the file operations service for external access
        /// </summary>
        /// <returns>The file operations service instance</returns>
        public IFileOperationsService? GetFileOperationsService() => _fileOperationsService;

        public TreeNodeViewModel(TreeNodeInfo nodeInfo, IGGPKService ggpkService, IFileOperationsService fileOperationsService, TreeNodeViewModel? parent = null)
        {
            NodeInfo = nodeInfo ?? throw new ArgumentNullException(nameof(nodeInfo));
            _ggpkService = ggpkService ?? throw new ArgumentNullException(nameof(ggpkService));
            _fileOperationsService = fileOperationsService ?? throw new ArgumentNullException(nameof(fileOperationsService));
            Parent = parent;

            Children = new ObservableCollection<TreeNodeViewModel>();

            ToggleExpandCommand = new AsyncRelayCommand(ToggleExpandAsync);
            ExtractCommand = new AsyncRelayCommand(ExtractAsync);
            PropertiesCommand = new AsyncRelayCommand(ShowPropertiesAsync);

            // Add placeholder child for expandable nodes
            if (CanExpand && !HasLoadedChildren)
            {
                Children.Add(CreatePlaceholderNode());
            }
        }

        /// <summary>
        /// Toggles the expanded state of the node
        /// </summary>
        private async Task ToggleExpandAsync()
        {
            if (!CanExpand)
                return;

            if (IsExpanded)
            {
                IsExpanded = false;
            }
            else
            {
                await ExpandAsync();
            }
        }

        /// <summary>
        /// Expands the node and loads its children
        /// </summary>
        public async Task ExpandAsync()
        {
            if (!CanExpand || IsLoading)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Expanding node {NodeInfo.Name} ({NodeInfo.FullPath})");
                IsLoading = true;
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource = new CancellationTokenSource();

                if (!HasLoadedChildren)
                {
                    System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Loading children for {NodeInfo.Name}");
                    await LoadChildrenAsync(_loadCancellationTokenSource.Token);
                    System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Loaded {Children.Count} children for {NodeInfo.Name}");
                }

                IsExpanded = true;
                System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Successfully expanded {NodeInfo.Name}");
            }
            catch (OperationCanceledException)
            {
                // Operation was cancelled, ignore
                System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Node expansion cancelled for {NodeInfo.FullPath}");
            }
            catch (Exception ex)
            {
                // Handle error - could raise an event or show a message
                System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Error expanding node {NodeInfo.FullPath}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Stack trace: {ex.StackTrace}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Collapses the node
        /// </summary>
        public void Collapse()
        {
            IsExpanded = false;
            _loadCancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Loads the children of this node
        /// </summary>
        private async Task LoadChildrenAsync(CancellationToken cancellationToken)
        {
            if (HasLoadedChildren || NodeInfo.Type != NodeType.Directory)
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine("TreeNodeViewModel: Loading children for directory " + NodeInfo.FullPath);
                var childNodes = await _ggpkService.GetChildrenAsync(NodeInfo.FullPath, cancellationToken);
                System.Diagnostics.Debug.WriteLine("TreeNodeViewModel: Retrieved " + childNodes.Count().ToString() + " child nodes from service");
                
                // Clear placeholder
                Children.Clear();

                // Sort children: directories first, then files
                var sortedChildren = childNodes
                    .OrderBy(child => child.Type != NodeType.Directory)
                    .ThenBy(child => child.Name)
                    .ToList();

                // Add actual children
                foreach (var childInfo in sortedChildren)
                {
                    if (cancellationToken.IsCancellationRequested)
                        return;

                    var childViewModel = new TreeNodeViewModel(childInfo, _ggpkService, _fileOperationsService, this);
                    
                    // Subscribe to child node selection events
                    childViewModel.NodeSelected += OnChildNodeSelected;
                    
                    Children.Add(childViewModel);
                }

                HasLoadedChildren = true;
                System.Diagnostics.Debug.WriteLine("TreeNodeViewModel: Successfully loaded " + Children.Count.ToString() + " children for " + NodeInfo.Name);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Loading children cancelled for {NodeInfo.FullPath}");
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Error loading children for {NodeInfo.FullPath}: {ex.Message}");
                // On error, keep the placeholder to allow retry
                if (Children.Count == 0)
                {
                    Children.Add(CreatePlaceholderNode());
                }
                throw;
            }
        }

        /// <summary>
        /// Refreshes the children of this node
        /// </summary>
        public async Task RefreshAsync()
        {
            if (NodeInfo.Type != NodeType.Directory)
                return;

            HasLoadedChildren = false;
            Children.Clear();

            if (CanExpand)
            {
                Children.Add(CreatePlaceholderNode());
                
                if (IsExpanded)
                {
                    await ExpandAsync();
                }
            }
        }

        /// <summary>
        /// Extracts this node to the file system
        /// </summary>
        private async Task ExtractAsync()
        {
            try
            {
                // Show folder browser dialog to select destination
                var folderDialog = new OpenFolderDialog
                {
                    Title = $"Select destination folder for {NodeInfo.Name}",
                    Multiselect = false
                };

                if (folderDialog.ShowDialog() == true)
                {
                    var destinationPath = folderDialog.FolderName;
                    
                    // Set initial status message
                    var explorerViewModel = FindExplorerViewModel();
                    if (explorerViewModel != null)
                    {
                        explorerViewModel.IsProgressVisible = true;
                        explorerViewModel.ProgressValue = 0;
                        explorerViewModel.ProgressText = "Preparing extraction...";
                        explorerViewModel.StatusText = NodeInfo.Type == NodeType.Directory
                            ? $"Preparing to extract folder '{NodeInfo.Name}'"
                            : $"Preparing to extract file '{NodeInfo.Name}'";
                    }
                    
                    // Create progress reporter to update the UI
                    var progress = new Progress<Services.ProgressInfo>(progressInfo =>
                    {
                        // Find the ExplorerViewModel to update progress
                        var explorerViewModel = FindExplorerViewModel();
                        if (explorerViewModel != null)
                        {
                            explorerViewModel.IsProgressVisible = true;
                            explorerViewModel.ProgressValue = progressInfo.Percentage;
                            
                            // Enhanced progress text with current file info
                            if (!string.IsNullOrEmpty(progressInfo.CurrentFile))
                            {
                                var currentFileName = Path.GetFileName(progressInfo.CurrentFile);
                                explorerViewModel.ProgressText = $"Extracting: {currentFileName}";
                            }
                            else
                            {
                                explorerViewModel.ProgressText = progressInfo.Status ?? "Extracting...";
                            }
                            
                            // Enhanced status text with detailed progress information
                            var statusText = NodeInfo.Type == NodeType.Directory
                                ? $"Extracting folder '{NodeInfo.Name}'"
                                : $"Extracting file '{NodeInfo.Name}'";
                            
                            if (progressInfo.TotalFiles > 0)
                            {
                                statusText += $" - {progressInfo.FilesProcessed}/{progressInfo.TotalFiles} files";
                                
                                // Add percentage if available
                                if (progressInfo.Percentage > 0)
                                {
                                    statusText += $" ({progressInfo.Percentage:F0}%)";
                                }
                                
                                // Add bytes information if available
                                if (progressInfo.TotalBytes > 0)
                                {
                                    var processedSize = FormatFileSize(progressInfo.BytesProcessed);
                                    var totalSize = FormatFileSize(progressInfo.TotalBytes);
                                    statusText += $" - {processedSize}/{totalSize}";
                                }
                            }
                            
                            explorerViewModel.StatusText = statusText;
                        }
                    });
                    
                    // Use ExtractMultipleAsync to preserve relative path structure with progress reporting
                    var sourcePaths = new[] { NodeInfo.FullPath };
                    var results = await _fileOperationsService.ExtractMultipleAsync(sourcePaths, destinationPath, progress);
                    
                    // Update final status and hide progress when done
                    var explorerVm = FindExplorerViewModel();
                    if (explorerVm != null)
                    {
                        explorerVm.IsProgressVisible = false;
                        explorerVm.ProgressValue = 100; // Show completion briefly
                        explorerVm.ProgressText = "Extraction complete";
                        
                        // Brief delay to show completion, then hide
                        _ = Task.Delay(1000).ContinueWith(_ =>
                        {
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                if (explorerVm != null)
                                {
                                    explorerVm.ProgressValue = 0;
                                    explorerVm.ProgressText = string.Empty;
                                }
                            });
                        });
                    }
                    
                    bool success = results.IsSuccess;
                    var mainViewModel = FindMainViewModel();

                    if (success)
                    {
                        // Update status text with detailed success information
                        if (explorerVm != null)
                        {
                            var statusText = NodeInfo.Type == NodeType.Directory
                                ? $"Successfully extracted folder '{NodeInfo.Name}' - {results.SuccessfulFiles} files"
                                : $"Successfully extracted file '{NodeInfo.Name}'";
                            
                            if (results.TotalBytesExtracted > 0)
                            {
                                statusText += $" ({FormatFileSize(results.TotalBytesExtracted)})";
                            }
                            
                            if (results.Duration.TotalSeconds > 0.1)
                            {
                                statusText += $" in {results.Duration.TotalSeconds:F1}s";
                            }
                            
                            explorerVm.StatusText = statusText;
                        }
                        
                        // Show appropriate toast based on whether it's a file or directory
                        if (NodeInfo.Type == NodeType.Directory)
                        {
                            // For directories, show folder and file counts
                            System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Toast notification - SuccessfulFiles: {results.SuccessfulFiles}, TotalFiles: {results.TotalFiles}");
                            
                            var message = results.SuccessfulFiles == 1 
                                ? $"Extracted 1 file from folder '{NodeInfo.Name}'"
                                : $"Extracted {results.SuccessfulFiles} files from folder '{NodeInfo.Name}'";
                            
                            if (results.Duration.TotalSeconds > 1)
                            {
                                message += $" in {results.Duration.TotalSeconds:F1}s";
                            }
                            
                            // Add destination info
                            var destinationFolder = Path.GetFileName(destinationPath.TrimEnd(Path.DirectorySeparatorChar));
                            message += $" to '{destinationFolder}'";
                            
                            mainViewModel?.ToastService?.ShowSuccess(message, "Folder Extracted");
                        }
                        else
                        {
                            // For individual files
                            var fileSize = FormatFileSize(NodeInfo.Size);
                            var message = $"Extracted file '{NodeInfo.Name}' ({fileSize})";
                            
                            if (results.Duration.TotalSeconds > 0.1)
                            {
                                message += $" in {results.Duration.TotalSeconds:F1}s";
                            }
                            
                            // Add destination info
                            var destinationFolder = Path.GetFileName(destinationPath.TrimEnd(Path.DirectorySeparatorChar));
                            message += $" to '{destinationFolder}'";
                            
                            mainViewModel?.ToastService?.ShowSuccess(message, "File Extracted");
                        }
                    }
                    else if (results.IsPartialSuccess)
                    {
                        // Partial success - some files extracted, some failed
                        if (explorerVm != null)
                        {
                            var statusText = NodeInfo.Type == NodeType.Directory
                                ? $"Partially extracted folder '{NodeInfo.Name}' - {results.SuccessfulFiles}/{results.TotalFiles} files succeeded"
                                : $"Partially extracted '{NodeInfo.Name}' with errors";
                            
                            if (results.TotalBytesExtracted > 0)
                            {
                                statusText += $" ({FormatFileSize(results.TotalBytesExtracted)} extracted)";
                            }
                            
                            explorerVm.StatusText = statusText;
                        }
                        
                        var message = NodeInfo.Type == NodeType.Directory
                            ? $"Partially extracted folder '{NodeInfo.Name}': {results.SuccessfulFiles} succeeded, {results.FailedFiles} failed"
                            : $"Failed to extract file '{NodeInfo.Name}': {results.Errors[0]?.ErrorMessage ?? "Unknown error"}";
                            
                        mainViewModel?.ToastService?.ShowWarning(message, "Partial Extraction");
                    }
                    else
                    {
                        // Complete failure
                        var errorMessage = results.Errors.Length > 0 
                            ? results.Errors[0].ErrorMessage
                            : "Unknown error occurred";
                            
                        if (explorerVm != null)
                        {
                            var statusText = NodeInfo.Type == NodeType.Directory
                                ? $"Failed to extract folder '{NodeInfo.Name}': {errorMessage}"
                                : $"Failed to extract file '{NodeInfo.Name}': {errorMessage}";
                            
                            explorerVm.StatusText = statusText;
                        }
                        
                        var message = NodeInfo.Type == NodeType.Directory
                            ? $"Failed to extract folder '{NodeInfo.Name}': {errorMessage}"
                            : $"Failed to extract file '{NodeInfo.Name}': {errorMessage}";
                            
                        mainViewModel?.ToastService?.ShowError(message, "Extraction Failed");
                    }
                }
            }
            catch (Exception ex)
            {
                // Hide progress on error
                var explorerVmError = FindExplorerViewModel();
                var mainViewModel = FindMainViewModel();
                
                if (explorerVmError != null)
                {
                    explorerVmError.IsProgressVisible = false;
                    explorerVmError.ProgressValue = 0;
                    explorerVmError.ProgressText = string.Empty;
                    
                    var statusText = NodeInfo.Type == NodeType.Directory
                        ? $"Error extracting folder '{NodeInfo.Name}': {ex.Message}"
                        : $"Error extracting file '{NodeInfo.Name}': {ex.Message}";
                    
                    explorerVmError.StatusText = statusText;
                }
                
                var message = NodeInfo.Type == NodeType.Directory
                    ? $"Error extracting folder '{NodeInfo.Name}': {ex.Message}"
                    : $"Error extracting file '{NodeInfo.Name}': {ex.Message}";
                    
                mainViewModel?.ToastService?.ShowError(message, "Extraction Error");
            }
        }

        private string GetTypeDescription()
        {
            return NodeInfo.Type switch
            {
                NodeType.Directory => "Folder",
                NodeType.BundleFile => "Bundle File",
                NodeType.CompressedFile => "Compressed File",
                NodeType.File => _fileOperationsService.GetFileTypeDescription(System.IO.Path.GetExtension(NodeInfo.Name)),
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Shows properties for this node using the modern Properties dialog
        /// </summary>
        private Task ShowPropertiesAsync()
        {
            try
            {
                // Create the properties dialog view model with the node info
                var propertiesViewModel = new PropertiesDialogViewModel(NodeInfo);
                
                // Show the modern properties dialog
                Views.Dialogs.PropertiesDialog.ShowDialog(propertiesViewModel);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing properties for {NodeInfo.FullPath}: {ex.Message}");
                
                var mainViewModel = FindMainViewModel();
                mainViewModel?.ToastService?.ShowError(
                    $"Error displaying properties for '{NodeInfo.Name}': {ex.Message}",
                    "Properties Error");
            }
            
            return Task.CompletedTask;
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

        private string GetRelativePathFromRoot(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return "unknown_file";

            // Remove leading slash and normalize path separators
            var relativePath = fullPath.TrimStart('/', '\\');
            
            // Replace forward slashes with backslashes for Windows paths
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            
            // If the path is empty after trimming, use just the filename
            if (string.IsNullOrEmpty(relativePath))
            {
                return "root_file";
            }

            return relativePath;
        }

        /// <summary>
        /// Finds the ExplorerViewModel by traversing up the visual tree
        /// This is a simple approach - in a more complex app, you'd use dependency injection or messaging
        /// </summary>
        private ExplorerViewModel? FindExplorerViewModel()
        {
            try
            {
                // Try to find the main window and get the ExplorerViewModel from its DataContext
                var mainWindow = Application.Current?.MainWindow;
                if (mainWindow?.DataContext is MainViewModel mainViewModel)
                {
                    return mainViewModel.ExplorerViewModel;
                }
            }
            catch
            {
                // Ignore errors in finding the view model
            }
            
            return null;
        }

        /// <summary>
        /// Finds the MainViewModel to access services like toast notifications
        /// </summary>
        private MainViewModel? FindMainViewModel()
        {
            try
            {
                var mainWindow = Application.Current?.MainWindow;
                return mainWindow?.DataContext as MainViewModel;
            }
            catch
            {
                // Ignore errors in finding the view model
            }
            
            return null;
        }

        /// <summary>
        /// Creates a placeholder node for lazy loading
        /// </summary>
        private TreeNodeViewModel CreatePlaceholderNode()
        {
            var placeholderInfo = new TreeNodeInfo
            {
                Name = "Loading...",
                FullPath = NodeInfo.FullPath + "/placeholder",
                Type = NodeType.Directory,
                HasChildren = false
            };

            return new TreeNodeViewModel(placeholderInfo, _ggpkService, _fileOperationsService, this);
        }

        /// <summary>
        /// Gets the appropriate icon path for this node type
        /// </summary>
        private string GetIconPath()
        {
            return NodeInfo.Type switch
            {
                NodeType.Directory => IsExpanded ? "FolderOpen" : "Folder",
                NodeType.File => "Document",
                NodeType.BundleFile => "Archive",
                NodeType.CompressedFile => "DocumentZip",
                _ => "Document"
            };
        }

        /// <summary>
        /// Cancels any ongoing loading operation
        /// </summary>
        public void CancelLoading()
        {
            _loadCancellationTokenSource?.Cancel();
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);

            // Update icon when expanded state changes
            if (e.PropertyName == nameof(IsExpanded))
            {
                OnPropertyChanged(nameof(IconPath));
            }
        }

        /// <summary>
        /// Handles IsSelected property changes
        /// </summary>
        partial void OnIsSelectedChanged(bool value)
        {
            if (value)
            {
                // Raise selection event when this node is selected
                NodeSelected?.Invoke(this, new TreeNodeSelectedEventArgs(this));
                System.Diagnostics.Debug.WriteLine($"TreeNodeViewModel: Node selected - {NodeInfo.Name} ({NodeInfo.Type})");
            }
        }

        /// <summary>
        /// Handles selection events from child nodes
        /// </summary>
        private void OnChildNodeSelected(object? sender, TreeNodeSelectedEventArgs e)
        {
            // Forward the selection event up the tree
            NodeSelected?.Invoke(sender, e);
        }
    }
}