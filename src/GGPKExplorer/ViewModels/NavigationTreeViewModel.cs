using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGPKExplorer.Models;
using GGPKExplorer.Services;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for the navigation tree view
    /// </summary>
    public partial class NavigationTreeViewModel : ObservableObject
    {
        private readonly IGGPKService _ggpkService;
        private readonly IFileOperationsService _fileOperationsService;
        private CancellationTokenSource? _loadCancellationTokenSource = null;

        /// <summary>
        /// Collection of root nodes in the tree
        /// </summary>
        public ObservableCollection<TreeNodeViewModel> RootNodes { get; }

        [ObservableProperty]
        private TreeNodeViewModel? _selectedNode;

        [ObservableProperty]
        private bool _isLoading;

        [ObservableProperty]
        private string _statusText = "Ready";

        /// <summary>
        /// Event raised when a node is selected
        /// </summary>
        public event EventHandler<TreeNodeSelectedEventArgs>? NodeSelected;

        /// <summary>
        /// Event raised when a folder is double-clicked for navigation
        /// </summary>
        public event EventHandler<FolderNavigatedEventArgs>? FolderNavigated;



        /// <summary>
        /// Command to expand all nodes
        /// </summary>
        public ICommand ExpandAllCommand { get; }

        /// <summary>
        /// Command to collapse all nodes
        /// </summary>
        public ICommand CollapseAllCommand { get; }

        [ObservableProperty]
        private string _searchFilter = string.Empty;

        /// <summary>
        /// Handles SearchFilter property changes to apply filtering
        /// </summary>
        partial void OnSearchFilterChanged(string value)
        {
            ApplySearchFilter(value);
        }

        public NavigationTreeViewModel(IGGPKService ggpkService, IFileOperationsService fileOperationsService)
        {
            _ggpkService = ggpkService ?? throw new ArgumentNullException(nameof(ggpkService));
            _fileOperationsService = fileOperationsService ?? throw new ArgumentNullException(nameof(fileOperationsService));

            RootNodes = new ObservableCollection<TreeNodeViewModel>();

            ExpandAllCommand = new AsyncRelayCommand(ExpandAllAsync);
            CollapseAllCommand = new RelayCommand(CollapseAll);

            // Note: We don't subscribe to GGPKLoaded event here as initialization is handled manually via InitializeAsync
        }

        /// <summary>
        /// Selects a node in the tree
        /// </summary>
        /// <param name="node">The node to select</param>
        public void SelectNode(TreeNodeViewModel node)
        {
            if (SelectedNode != null)
            {
                SelectedNode.IsSelected = false;
            }

            SelectedNode = node;
            if (node != null)
            {
                node.IsSelected = true;
                NodeSelected?.Invoke(this, new TreeNodeSelectedEventArgs(node));
                StatusText = $"Selected: {node.NodeInfo.FullPath}";
            }
            else
            {
                StatusText = "Ready";
            }
        }

        /// <summary>
        /// Navigates to a specific path in the tree
        /// </summary>
        /// <param name="path">The path to navigate to</param>
        public async Task NavigateToAsync(string path)
        {
            if (string.IsNullOrEmpty(path) || !_ggpkService.IsLoaded)
                return;

            try
            {
                IsLoading = true;
                StatusText = $"Navigating to {path}...";

                // Find the node by path
                var node = await FindNodeByPathAsync(path);
                if (node != null)
                {
                    // Expand parent nodes if necessary
                    await ExpandToNodeAsync(node);
                    SelectNode(node);
                }
                else
                {
                    // Don't show error for root path, just clear status
                    if (path == "/" || string.IsNullOrEmpty(path))
                    {
                        StatusText = "Ready";
                    }
                    else
                    {
                        StatusText = $"Path not found: {path}";
                    }
                }
            }
            catch (Exception ex)
            {
                StatusText = $"Error navigating to path: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }



        /// <summary>
        /// Expands all nodes in the tree
        /// </summary>
        private async Task ExpandAllAsync()
        {
            try
            {
                IsLoading = true;
                StatusText = "Expanding all nodes...";

                foreach (var rootNode in RootNodes)
                {
                    await ExpandNodeRecursivelyAsync(rootNode);
                }

                StatusText = "All nodes expanded";
            }
            catch (Exception ex)
            {
                StatusText = $"Error expanding nodes: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Collapses all nodes in the tree
        /// </summary>
        private void CollapseAll()
        {
            foreach (var rootNode in RootNodes)
            {
                CollapseNodeRecursively(rootNode);
            }
            StatusText = "All nodes collapsed";
        }

        /// <summary>
        /// Applies search filter to the tree
        /// </summary>
        public void ApplySearchFilter(string searchQuery)
        {
            SearchFilter = searchQuery ?? string.Empty;
            
            // Apply filter to all nodes
            foreach (var rootNode in RootNodes)
            {
                ApplyFilterToNode(rootNode, SearchFilter);
            }
        }

        /// <summary>
        /// Recursively applies filter to a node and its children
        /// </summary>
        private bool ApplyFilterToNode(TreeNodeViewModel node, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                // No filter - show all nodes
                node.IsVisible = true;
                foreach (var child in node.Children)
                {
                    ApplyFilterToNode(child, filter);
                }
                return true;
            }

            // Check if this node matches the filter
            bool nodeMatches = node.NodeInfo.Name.Contains(filter, StringComparison.OrdinalIgnoreCase);
            
            // Check if any children match
            bool hasMatchingChildren = false;
            foreach (var child in node.Children)
            {
                if (ApplyFilterToNode(child, filter))
                {
                    hasMatchingChildren = true;
                }
            }

            // Show node if it matches or has matching children
            bool shouldShow = nodeMatches || hasMatchingChildren;
            node.IsVisible = shouldShow;

            // Auto-expand nodes that have matching children
            if (hasMatchingChildren && !string.IsNullOrEmpty(filter))
            {
                _ = Task.Run(async () => await node.ExpandAsync());
            }

            return shouldShow;
        }

        /// <summary>
        /// Cancels any ongoing operations
        /// </summary>
        public void CancelOperations()
        {
            _loadCancellationTokenSource?.Cancel();
        }

        // OnGGPKLoaded event handler removed to prevent duplicate loading
        // Tree initialization is now handled manually via InitializeAsync method

        /// <summary>
        /// Clears the tree when GGPK is closed
        /// </summary>
        public void ClearTree()
        {
            RootNodes.Clear();
            SelectedNode = null;
            StatusText = "Ready";
        }

        /// <summary>
        /// Initializes the tree view with the current GGPK data
        /// </summary>
        public async Task InitializeAsync()
        {
            if (_ggpkService.IsLoaded)
            {
                await LoadRootNodesAsync(CancellationToken.None);
            }
        }

        /// <summary>
        /// Navigates to a folder when double-clicked to show its contents
        /// </summary>
        public async Task NavigateToFolderAsync(TreeNodeViewModel folderNode)
        {
            System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: NavigateToFolderAsync called with {folderNode?.NodeInfo?.Name}");
            
            if (folderNode?.NodeInfo == null || folderNode.NodeInfo.Type != NodeType.Directory)
            {
                System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: Invalid folder node - NodeInfo: {folderNode?.NodeInfo}, Type: {folderNode?.NodeInfo?.Type}");
                return;
            }

            try
            {
                System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: Navigating to folder {folderNode.NodeInfo.FullPath}");
                
                // Ensure the folder is expanded and its children are loaded
                if (!folderNode.IsExpanded)
                {
                    System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: Expanding folder {folderNode.NodeInfo.Name}");
                    await folderNode.ExpandAsync();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: Folder {folderNode.NodeInfo.Name} is already expanded");
                }
                
                // Raise a navigation event that the ExplorerViewModel can handle
                System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: Raising FolderNavigated event for {folderNode.NodeInfo.Name}");
                FolderNavigated?.Invoke(this, new FolderNavigatedEventArgs(folderNode));
                
                StatusText = $"Navigated to: {folderNode.NodeInfo.Name}";
                System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: Successfully navigated to {folderNode.NodeInfo.Name}");
            }
            catch (Exception ex)
            {
                StatusText = $"Error navigating to folder: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: Error navigating to folder {folderNode.NodeInfo.FullPath}: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: Exception details: {ex}");
            }
        }

        /// <summary>
        /// Subscribes to events from a tree node and its children recursively
        /// </summary>
        private void SubscribeToNodeEvents(TreeNodeViewModel node)
        {
            // Subscribe to this node's selection event
            node.NodeSelected += OnChildNodeSelected;
            
            // Subscribe to existing children
            foreach (var child in node.Children)
            {
                SubscribeToNodeEvents(child);
            }
        }

        /// <summary>
        /// Handles selection events from child nodes
        /// </summary>
        private void OnChildNodeSelected(object? sender, TreeNodeSelectedEventArgs e)
        {
            // Update the selected node in this view model
            if (SelectedNode != null)
            {
                SelectedNode.IsSelected = false;
            }
            
            SelectedNode = e.SelectedNode;
            
            // Forward the event to parent components
            NodeSelected?.Invoke(this, e);
            
            StatusText = $"Selected: {e.SelectedNode.NodeInfo.FullPath}";
            System.Diagnostics.Debug.WriteLine($"NavigationTreeViewModel: Child node selected - {e.SelectedNode.NodeInfo.Name}");
        }

        /// <summary>
        /// Loads the root nodes of the tree
        /// </summary>
        private async Task LoadRootNodesAsync(CancellationToken cancellationToken)
        {
            try
            {
                RootNodes.Clear();

                var rootInfo = await _ggpkService.GetRootAsync(cancellationToken);
                if (rootInfo != null)
                {
                    var rootNode = new TreeNodeViewModel(rootInfo, _ggpkService, _fileOperationsService);
                    
                    // Subscribe to node selection events
                    SubscribeToNodeEvents(rootNode);
                    
                    RootNodes.Add(rootNode);

                    // Auto-expand the root node
                    await rootNode.ExpandAsync();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                StatusText = $"Error loading tree: {ex.Message}";
            }
        }

        /// <summary>
        /// Finds a node by its path
        /// </summary>
        private async Task<TreeNodeViewModel?> FindNodeByPathAsync(string path)
        {
            foreach (var rootNode in RootNodes)
            {
                var found = await FindNodeInSubtreeAsync(rootNode, path);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// Recursively searches for a node in a subtree
        /// </summary>
        private async Task<TreeNodeViewModel?> FindNodeInSubtreeAsync(TreeNodeViewModel node, string path)
        {
            if (string.Equals(node.NodeInfo.FullPath, path, StringComparison.OrdinalIgnoreCase))
                return node;

            if (!node.HasLoadedChildren && node.CanExpand)
            {
                await node.ExpandAsync();
            }

            foreach (var child in node.Children)
            {
                var found = await FindNodeInSubtreeAsync(child, path);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// Expands the tree to show a specific node
        /// </summary>
        private async Task ExpandToNodeAsync(TreeNodeViewModel targetNode)
        {
            // Build path from root to target node
            var pathNodes = new List<TreeNodeViewModel>();
            var current = targetNode;
            while (current != null)
            {
                pathNodes.Insert(0, current);
                current = current.Parent;
            }

            // Expand each node in the path
            foreach (var node in pathNodes)
            {
                if (node.CanExpand && !node.IsExpanded)
                {
                    await node.ExpandAsync();
                }
            }
        }

        /// <summary>
        /// Recursively expands a node and all its children
        /// </summary>
        private async Task ExpandNodeRecursivelyAsync(TreeNodeViewModel node)
        {
            if (node.CanExpand && !node.IsExpanded)
            {
                await node.ExpandAsync();
            }

            foreach (var child in node.Children)
            {
                if (child.CanExpand)
                {
                    await ExpandNodeRecursivelyAsync(child);
                }
            }
        }

        /// <summary>
        /// Recursively collapses a node and all its children
        /// </summary>
        private void CollapseNodeRecursively(TreeNodeViewModel node)
        {
            foreach (var child in node.Children)
            {
                CollapseNodeRecursively(child);
            }

            if (node.IsExpanded)
            {
                node.Collapse();
            }
        }
    }

    /// <summary>
    /// Event arguments for folder navigation events
    /// </summary>
    public class FolderNavigatedEventArgs : EventArgs
    {
        public TreeNodeViewModel FolderNode { get; }

        public FolderNavigatedEventArgs(TreeNodeViewModel folderNode)
        {
            FolderNode = folderNode ?? throw new ArgumentNullException(nameof(folderNode));
        }
    }
}