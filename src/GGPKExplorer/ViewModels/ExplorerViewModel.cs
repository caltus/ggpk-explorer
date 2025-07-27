using System;
using System.ComponentModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GGPKExplorer.Models;
using GGPKExplorer.Services;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for the dual-pane explorer view that coordinates between TreeView and FileListView
    /// </summary>
    public partial class ExplorerViewModel : ObservableObject
    {
        private readonly IGGPKService _ggpkService;
        private readonly IFileOperationsService _fileOperationsService;
        private readonly ISettingsService _settingsService;
        private readonly ISearchService _searchService;

        /// <summary>
        /// Navigation tree view model
        /// </summary>
        public NavigationTreeViewModel TreeViewModel { get; }



        /// <summary>
        /// Preview pane view model
        /// </summary>
        public PreviewPaneViewModel PreviewViewModel { get; }

        /// <summary>
        /// Search view model
        /// </summary>
        public SearchViewModel SearchViewModel { get; }

        [ObservableProperty]
        private string _currentPath = string.Empty;

        [ObservableProperty]
        private bool _isSplitViewOpen = true;

        [ObservableProperty]
        private double _splitterPosition = 300.0;

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _statusText = "Ready";

        [ObservableProperty]
        private string _treeSearchQuery = string.Empty;

        [ObservableProperty]
        private bool _useRegexSearch = false;

        public ExplorerViewModel(
            IGGPKService ggpkService, 
            IFileOperationsService fileOperationsService,
            ISettingsService settingsService,
            ISearchService searchService)
        {
            _ggpkService = ggpkService ?? throw new ArgumentNullException(nameof(ggpkService));
            _fileOperationsService = fileOperationsService ?? throw new ArgumentNullException(nameof(fileOperationsService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));

            // Create child view models
            TreeViewModel = new NavigationTreeViewModel(_ggpkService, _fileOperationsService);
            PreviewViewModel = new PreviewPaneViewModel(_ggpkService, _fileOperationsService);
            SearchViewModel = new SearchViewModel(searchService, _ggpkService);

            // Subscribe to tree selection changes
            TreeViewModel.NodeSelected += OnTreeNodeSelected;
            TreeViewModel.FolderNavigated += OnFolderNavigated;
            TreeViewModel.PropertyChanged += OnTreeViewModelPropertyChanged;
            
            // Subscribe to search query changes
            PropertyChanged += OnPropertyChanged;
            SearchViewModel.PropertyChanged += OnSearchViewModelPropertyChanged;

            // Subscribe to GGPK service events
            _ggpkService.GGPKLoaded += OnGGPKLoaded;
            _ggpkService.ErrorOccurred += OnErrorOccurred;

            // Load settings
            LoadSettings();

            // Register for navigation messages
            WeakReferenceMessenger.Default.Register<NavigationMessage>(this, OnNavigationMessage);
        }

        /// <summary>
        /// Command to navigate to a specific path
        /// </summary>
        [RelayCommand]
        private async Task NavigateToAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || path == CurrentPath)
                return;

            try
            {
                IsLoading = true;
                StatusText = $"Navigating to {path}...";

                CurrentPath = path;

                // Update tree selection
                await TreeViewModel.NavigateToAsync(path);

                StatusText = $"Showing contents of {path}";

                // Send navigation message to other components
                WeakReferenceMessenger.Default.Send(new NavigationMessage(path));
            }
            catch (Exception ex)
            {
                StatusText = $"Error navigating to {path}: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }



        /// <summary>
        /// Command to go back in navigation history
        /// </summary>
        [RelayCommand]
        private void GoBack()
        {
            // TODO: Implement navigation history in future tasks
            StatusText = "Navigation history will be implemented in a future update";
        }

        /// <summary>
        /// Command to go forward in navigation history
        /// </summary>
        [RelayCommand]
        private void GoForward()
        {
            // TODO: Implement navigation history in future tasks
            StatusText = "Navigation history will be implemented in a future update";
        }

        /// <summary>
        /// Command to toggle the split view pane visibility
        /// </summary>
        [RelayCommand]
        private void ToggleSplitView()
        {
            IsSplitViewOpen = !IsSplitViewOpen;
            StatusText = IsSplitViewOpen ? "Navigation pane shown" : "Navigation pane hidden";
        }

        /// <summary>
        /// Command to clear the search filter
        /// </summary>
        [RelayCommand]
        private void ClearSearch()
        {
            TreeSearchQuery = string.Empty;
        }

        /// <summary>
        /// Handles tree node selection changes
        /// </summary>
        private async void OnTreeNodeSelected(object? sender, TreeNodeSelectedEventArgs e)
        {
            if (e.SelectedNode?.NodeInfo != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: OnTreeNodeSelected - {e.SelectedNode.NodeInfo.Name} ({e.SelectedNode.NodeInfo.Type})");
                    
                    CurrentPath = e.SelectedNode.NodeInfo.FullPath;
                    
                    // Update preview for both files and directories
                    System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: Calling PreviewViewModel.UpdatePreviewAsync");
                    await PreviewViewModel.UpdatePreviewAsync(e.SelectedNode.NodeInfo);
                    
                    StatusText = $"Selected: {e.SelectedNode.NodeInfo.Name}";
                    System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: Tree selection handled for: {e.SelectedNode.NodeInfo.Name}");
                }
                catch (Exception ex)
                {
                    StatusText = $"Error selecting node: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: Selection error: {ex}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("ExplorerViewModel: OnTreeNodeSelected - No node info available");
            }
        }

        /// <summary>
        /// Handles folder navigation events from double-clicking folders
        /// </summary>
        private async void OnFolderNavigated(object? sender, FolderNavigatedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: OnFolderNavigated event received from {sender?.GetType().Name}");
            
            if (e.FolderNode?.NodeInfo != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: OnFolderNavigated - {e.FolderNode.NodeInfo.Name}");
                    
                    // Update current path
                    CurrentPath = e.FolderNode.NodeInfo.FullPath;
                    System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: Updated CurrentPath to {CurrentPath}");
                    
                    // Show folder contents in preview pane
                    System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: Calling PreviewViewModel.UpdatePreviewAsync");
                    await PreviewViewModel.UpdatePreviewAsync(e.FolderNode.NodeInfo);
                    
                    StatusText = $"Showing contents of: {e.FolderNode.NodeInfo.Name}";
                    System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: Folder navigation handled for: {e.FolderNode.NodeInfo.Name}");
                }
                catch (Exception ex)
                {
                    StatusText = $"Error navigating to folder: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: Folder navigation error: {ex}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"ExplorerViewModel: OnFolderNavigated - Invalid folder node or NodeInfo is null");
            }
        }

        /// <summary>
        /// Handles property changes from the tree view model
        /// </summary>
        private void OnTreeViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(NavigationTreeViewModel.IsLoading))
            {
                UpdateLoadingState();
            }
            else if (e.PropertyName == nameof(NavigationTreeViewModel.StatusText))
            {
                UpdateStatusText();
            }
        }





        /// <summary>
        /// Handles GGPK file loaded event
        /// </summary>
        private async void OnGGPKLoaded(object? sender, GGPKLoadedEventArgs e)
        {
            StatusText = $"Loaded {System.IO.Path.GetFileName(e.FilePath)}";
            
            // Navigate to root after loading
            var root = await _ggpkService.GetRootAsync();
            if (root != null)
            {
                await NavigateToAsync(root.FullPath);
            }
        }

        /// <summary>
        /// Handles errors from the GGPK service
        /// </summary>
        private void OnErrorOccurred(object? sender, ErrorEventArgs e)
        {
            StatusText = $"Error: {e.Exception.Message}";
        }

        /// <summary>
        /// Handles navigation messages from other components
        /// </summary>
        private async void OnNavigationMessage(object recipient, NavigationMessage message)
        {
            if (message.Path != CurrentPath)
            {
                await NavigateToAsync(message.Path);
            }
        }

        /// <summary>
        /// Updates the overall loading state based on child view models
        /// </summary>
        private void UpdateLoadingState()
        {
            IsLoading = TreeViewModel.IsLoading || PreviewViewModel.IsLoading;
        }

        /// <summary>
        /// Updates the status text from child view models
        /// </summary>
        private void UpdateStatusText()
        {
            if (!string.IsNullOrEmpty(TreeViewModel.StatusText))
            {
                StatusText = TreeViewModel.StatusText;
            }
        }

        /// <summary>
        /// Loads settings for the explorer view
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                // Load splitter position from settings
                if (_settingsService != null)
                {
                    SplitterPosition = _settingsService.GetSetting("ExplorerView.SplitterPosition", 300.0);
                    IsSplitViewOpen = _settingsService.GetSetting("ExplorerView.IsSplitViewOpen", true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                // Use defaults
                SplitterPosition = 300.0;
                IsSplitViewOpen = true;
            }
        }

        /// <summary>
        /// Saves settings for the explorer view
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                if (_settingsService != null)
                {
                    _settingsService.SetSetting("ExplorerView.SplitterPosition", SplitterPosition);
                    _settingsService.SetSetting("ExplorerView.IsSplitViewOpen", IsSplitViewOpen);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles splitter position changes
        /// </summary>
        partial void OnSplitterPositionChanged(double value)
        {
            // Save settings when splitter position changes
            SaveSettings();
        }

        /// <summary>
        /// Handles split view open/close changes
        /// </summary>
        partial void OnIsSplitViewOpenChanged(bool value)
        {
            // Save settings when split view state changes
            SaveSettings();
        }

        /// <summary>
        /// Loads a GGPK file asynchronously
        /// </summary>
        /// <param name="filePath">Path to the GGPK file</param>
        /// <returns>True if the file was loaded successfully</returns>
        public async Task<bool> LoadGGPKAsync(string filePath)
        {
            try
            {
                IsLoading = true;
                StatusText = $"Loading {System.IO.Path.GetFileName(filePath)}...";

                // Use the GGPK service to open the file
                var success = await _ggpkService.OpenGGPKAsync(filePath);
                
                if (success)
                {
                    StatusText = $"Successfully loaded {System.IO.Path.GetFileName(filePath)}";
                    
                    // Initialize the tree view with the root node
                    await TreeViewModel.InitializeAsync();
                    
                    // Clear preview pane
                    await PreviewViewModel.UpdatePreviewAsync(null);
                }
                else
                {
                    StatusText = "Failed to load GGPK file";
                }

                return success;
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading GGPK file: {ex.Message}";
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Cleanup()
        {
            // Unsubscribe from events
            TreeViewModel.NodeSelected -= OnTreeNodeSelected;
            TreeViewModel.FolderNavigated -= OnFolderNavigated;
            SearchViewModel.PropertyChanged -= OnSearchViewModelPropertyChanged;
            TreeViewModel.PropertyChanged -= OnTreeViewModelPropertyChanged;
            _ggpkService.GGPKLoaded -= OnGGPKLoaded;
            _ggpkService.ErrorOccurred -= OnErrorOccurred;

            // Unregister from messenger
            WeakReferenceMessenger.Default.Unregister<NavigationMessage>(this);

            // Cancel any ongoing operations
            TreeViewModel.CancelOperations();

            // Cleanup search view model
            SearchViewModel.Cleanup();

            // Save settings before cleanup
            SaveSettings();
        }

        /// <summary>
        /// Handles property changes for search functionality
        /// </summary>
        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(TreeSearchQuery))
            {
                // Apply search filter to tree
                TreeViewModel.ApplySearchFilter(TreeSearchQuery);
                
                if (string.IsNullOrEmpty(TreeSearchQuery))
                {
                    StatusText = "Ready";
                }
                else
                {
                    StatusText = $"Searching for: {TreeSearchQuery}";
                }
            }
        }

        /// <summary>
        /// Handles property changes from SearchViewModel
        /// </summary>
        private void OnSearchViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SearchViewModel.SearchQuery))
            {
                // Update tree search query when search view model query changes
                TreeSearchQuery = SearchViewModel.SearchQuery;
            }
        }
    }

    /// <summary>
    /// Message for navigation between components
    /// </summary>
    public class NavigationMessage
    {
        public string Path { get; }

        public NavigationMessage(string path)
        {
            Path = path;
        }
    }
}