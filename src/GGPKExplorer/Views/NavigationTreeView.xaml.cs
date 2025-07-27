using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Automation.Peers;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Microsoft.Extensions.DependencyInjection;
using GGPKExplorer.ViewModels;
using GGPKExplorer.Accessibility;
using GGPKExplorer.Services;

namespace GGPKExplorer.Views
{
    /// <summary>
    /// Interaction logic for NavigationTreeView.xaml
    /// </summary>
    public partial class NavigationTreeView : UserControl
    {
        /// <summary>
        /// Dependency property for the ViewModel
        /// </summary>
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(nameof(ViewModel), typeof(NavigationTreeViewModel), typeof(NavigationTreeView),
                new PropertyMetadata(null, OnViewModelChanged));

        /// <summary>
        /// Gets or sets the ViewModel for this view
        /// </summary>
        public NavigationTreeViewModel? ViewModel
        {
            get => (NavigationTreeViewModel?)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        /// <summary>
        /// Event raised when a tree node is selected
        /// </summary>
        public event System.EventHandler<TreeNodeSelectedEventArgs>? NodeSelected;

        /// <summary>
        /// Event raised when a tree node is double-clicked
        /// </summary>
        public event System.EventHandler<TreeNodeSelectedEventArgs>? NodeDoubleClicked;

        /// <summary>
        /// Event raised when multiple nodes are selected
        /// </summary>
        public event System.EventHandler<MultipleNodesSelectedEventArgs>? MultipleNodesSelected;

        /// <summary>
        /// Collection of currently selected nodes for multi-selection
        /// </summary>
        private readonly List<TreeNodeViewModel> _selectedNodes = new();

        /// <summary>
        /// Last selected node for range selection with Shift+Click
        /// </summary>
        private TreeNodeViewModel? _lastSelectedNode;

        /// <summary>
        /// Command to extract multiple selected nodes
        /// </summary>
        public ICommand ExtractSelectedCommand { get; private set; }

        /// <summary>
        /// Dependency property for tracking multi-selection state
        /// </summary>
        public static readonly DependencyProperty IsMultiSelectionActiveProperty =
            DependencyProperty.Register(nameof(IsMultiSelectionActive), typeof(bool), typeof(NavigationTreeView),
                new PropertyMetadata(false));

        /// <summary>
        /// Gets or sets whether multi-selection is currently active
        /// </summary>
        public bool IsMultiSelectionActive
        {
            get => (bool)GetValue(IsMultiSelectionActiveProperty);
            set => SetValue(IsMultiSelectionActiveProperty, value);
        }

        public NavigationTreeView()
        {
            InitializeComponent();
            SetupAccessibility();
            
            // Initialize commands
            ExtractSelectedCommand = new AsyncRelayCommand(ExtractSelectedNodesAsync, () => _selectedNodes.Count > 0);
        }

        /// <summary>
        /// Gets the toast service from the application's service provider
        /// </summary>
        private IToastService? GetToastService()
        {
            try
            {
                if (Application.Current is App app && app.Services != null)
                {
                    return app.Services.GetService<IToastService>();
                }
            }
            catch
            {
                // Ignore errors getting the service
            }
            return null;
        }

        /// <summary>
        /// Sets up accessibility features for the tree view
        /// </summary>
        private void SetupAccessibility()
        {
            // Set up keyboard navigation
            KeyboardNavigationHelper.SetupTreeViewNavigation(NavigationTree);
            
            // Set up accessibility properties
            AccessibilityHelper.SetupAccessibility(NavigationTree, 
                "GGPK Navigation Tree", 
                "Navigate through GGPK file structure. Use arrow keys to navigate, Enter to select, Space to expand/collapse folders.");
            
            // Set up high contrast support
            AccessibilityHelper.SetupHighContrastSupport(this);
        }

        /// <summary>
        /// Creates a custom automation peer for enhanced accessibility
        /// </summary>
        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new GGPKTreeViewAutomationPeer(NavigationTree);
        }

        /// <summary>
        /// Handles ViewModel property changes
        /// </summary>
        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NavigationTreeView view)
            {
                view.DataContext = e.NewValue;
                
                if (e.OldValue is NavigationTreeViewModel oldViewModel)
                {
                    oldViewModel.NodeSelected -= view.OnViewModelNodeSelected;
                }

                if (e.NewValue is NavigationTreeViewModel newViewModel)
                {
                    newViewModel.NodeSelected += view.OnViewModelNodeSelected;
                }
            }
        }

        /// <summary>
        /// Handles node selection from the ViewModel
        /// </summary>
        private void OnViewModelNodeSelected(object? sender, TreeNodeSelectedEventArgs e)
        {
            NodeSelected?.Invoke(this, e);
        }

        /// <summary>
        /// Handles TreeView selection changed event
        /// </summary>
        private void NavigationTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeNodeViewModel selectedNode && ViewModel != null)
            {
                // Only handle this if it's not a multi-selection operation
                // (multi-selection is handled in the mouse event handlers)
                if (!Keyboard.IsKeyDown(Key.LeftCtrl) && !Keyboard.IsKeyDown(Key.RightCtrl) && 
                    !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                {
                    ClearMultiSelection();
                    
                    // Set the last selected node for future range selection
                    _lastSelectedNode = selectedNode;
                    System.Diagnostics.Debug.WriteLine($"NavigationTree_SelectedItemChanged: Normal selection, _lastSelectedNode set to {selectedNode.NodeInfo.Name}");
                }

                ViewModel.SelectNode(selectedNode);
                
                // Announce selection to screen readers
                var peer = UIElementAutomationPeer.FromElement(NavigationTree) as GGPKTreeViewAutomationPeer;
                peer?.AnnounceNavigation(selectedNode);
            }
        }

        /// <summary>
        /// Handles TreeView mouse double-click event
        /// </summary>
        private void NavigationTree_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"NavigationTreeView: TreeView MouseDoubleClick event triggered");
            HandleDoubleClick(e);
        }

        /// <summary>
        /// Handles TreeViewItem mouse double-click event
        /// </summary>
        private void TreeViewItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"NavigationTreeView: TreeViewItem MouseDoubleClick event triggered");
            HandleDoubleClick(e);
            e.Handled = true; // Prevent event from bubbling up
        }

        /// <summary>
        /// Common double-click handling logic
        /// </summary>
        private void HandleDoubleClick(MouseButtonEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"NavigationTreeView: HandleDoubleClick called");
            
            if (NavigationTree.SelectedItem is TreeNodeViewModel selectedNode)
            {
                System.Diagnostics.Debug.WriteLine($"NavigationTreeView: Double-clicked on {selectedNode.NodeInfo?.Name} (CanExpand: {selectedNode.CanExpand})");
                
                NodeDoubleClicked?.Invoke(this, new TreeNodeSelectedEventArgs(selectedNode));
                
                // If it's a directory, toggle expansion and navigate to it
                if (selectedNode.CanExpand)
                {
                    System.Diagnostics.Debug.WriteLine($"NavigationTreeView: Executing ToggleExpandCommand for {selectedNode.NodeInfo?.Name}");
                    selectedNode.ToggleExpandCommand.Execute(null);
                    
                    // Also trigger navigation to show folder contents
                    if (ViewModel != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"NavigationTreeView: Calling NavigateToFolderAsync for {selectedNode.NodeInfo?.Name}");
                        _ = Task.Run(async () => await ViewModel.NavigateToFolderAsync(selectedNode));
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"NavigationTreeView: ViewModel is null!");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"NavigationTreeView: Node cannot expand (not a directory or no children)");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"NavigationTreeView: No selected item or not a TreeNodeViewModel");
            }
        }

        /// <summary>
        /// Handles TreeView key down events for keyboard navigation
        /// </summary>
        private void NavigationTree_KeyDown(object sender, KeyEventArgs e)
        {
            if (NavigationTree.SelectedItem is not TreeNodeViewModel selectedNode)
                return;

            switch (e.Key)
            {
                case Key.Enter:
                    // Enter key expands/collapses directories or raises double-click for files
                    if (selectedNode.CanExpand)
                    {
                        selectedNode.ToggleExpandCommand.Execute(null);
                    }
                    else
                    {
                        NodeDoubleClicked?.Invoke(this, new TreeNodeSelectedEventArgs(selectedNode));
                    }
                    e.Handled = true;
                    break;

                case Key.Right:
                    // Right arrow expands directories
                    if (selectedNode.CanExpand && !selectedNode.IsExpanded)
                    {
                        selectedNode.ToggleExpandCommand.Execute(null);
                        e.Handled = true;
                    }
                    break;

                case Key.Left:
                    // Left arrow collapses directories
                    if (selectedNode.CanExpand && selectedNode.IsExpanded)
                    {
                        selectedNode.Collapse();
                        e.Handled = true;
                    }
                    break;

                case Key.Up:
                case Key.Down:
                    // Handle Shift+Arrow for range selection
                    if (Keyboard.Modifiers == ModifierKeys.Shift)
                    {
                        HandleShiftArrowSelection(e.Key);
                        e.Handled = true;
                    }
                    break;

                case Key.F5:
                    // F5 key is reserved for future refresh functionality
                    e.Handled = true;
                    break;

                case Key.Apps:
                case Key.F10 when Keyboard.Modifiers == ModifierKeys.Shift:
                    // Context menu key or Shift+F10 shows context menu
                    ShowContextMenuForSelectedNode();
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Shows the context menu for the currently selected node
        /// </summary>
        private void ShowContextMenuForSelectedNode()
        {
            if (NavigationTree.SelectedItem is TreeNodeViewModel selectedNode)
            {
                // Find the TreeViewItem for the selected node
                var container = NavigationTree.ItemContainerGenerator.ContainerFromItem(selectedNode) as TreeViewItem;
                if (container != null)
                {
                    // Get the context menu from the data template
                    var contentPresenter = FindVisualChild<ContentPresenter>(container);
                    var grid = contentPresenter?.Content as Grid;
                    var contextMenu = grid?.ContextMenu;
                    
                    if (contextMenu != null)
                    {
                        contextMenu.PlacementTarget = container;
                        contextMenu.IsOpen = true;
                    }
                }
            }
        }

        /// <summary>
        /// Finds a visual child of the specified type
        /// </summary>
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                    return childOfChild;
            }
            return null;
        }

        /// <summary>
        /// Expands the tree to show a specific path
        /// </summary>
        /// <param name="path">Path to expand to</param>
        public async void ExpandToPath(string path)
        {
            if (ViewModel != null)
            {
                await ViewModel.NavigateToAsync(path);
            }
        }

        /// <summary>
        /// Gets the currently selected node
        /// </summary>
        public TreeNodeViewModel? SelectedNode => ViewModel?.SelectedNode;

        /// <summary>
        /// Handles key down events in the search box
        /// </summary>
        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // Focus back to tree view after search
                NavigationTree.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                // Clear search when Escape is pressed
                if (sender is Wpf.Ui.Controls.TextBox searchBox)
                {
                    searchBox.Text = string.Empty;
                    NavigationTree.Focus();
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// Cancels any ongoing operations
        /// </summary>
        public void CancelOperations()
        {
            ViewModel?.CancelOperations();
        }

        /// <summary>
        /// Handles TreeView preview mouse left button down for multi-selection
        /// </summary>
        private void NavigationTree_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var hitTest = e.OriginalSource as DependencyObject;
            var treeViewItem = FindParent<TreeViewItem>(hitTest);
            
            if (treeViewItem?.DataContext is TreeNodeViewModel node)
            {
                System.Diagnostics.Debug.WriteLine($"NavigationTree_PreviewMouseLeftButtonDown: Clicked on {node.NodeInfo.Name}");
                
                // Handle multi-selection with Ctrl key
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    System.Diagnostics.Debug.WriteLine($"Ctrl+Click detected on {node.NodeInfo.Name}");
                    ToggleNodeSelection(node);
                    _lastSelectedNode = node;
                    e.Handled = true; // Prevent normal selection
                }
                // Handle range selection with Shift key
                else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    System.Diagnostics.Debug.WriteLine($"Shift+Click detected on {node.NodeInfo.Name}, _lastSelectedNode={_lastSelectedNode?.NodeInfo.Name ?? "null"}");
                    SelectNodeRange(node);
                    e.Handled = true; // Prevent normal selection
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"Normal click on {node.NodeInfo.Name}");
                    // Normal single selection - clear multi-selection and add current node
                    ClearMultiSelection();
                    AddNodeToSelection(node);
                    _lastSelectedNode = node;
                    UpdateMultiSelectionState();
                    System.Diagnostics.Debug.WriteLine($"_lastSelectedNode set to {node.NodeInfo.Name}");
                }
            }
        }

        /// <summary>
        /// Handles TreeViewItem preview mouse left button down for multi-selection
        /// </summary>
        private void TreeViewItem_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is TreeViewItem treeViewItem && 
                treeViewItem.DataContext is TreeNodeViewModel node)
            {
                System.Diagnostics.Debug.WriteLine($"TreeViewItem_PreviewMouseLeftButtonDown: Clicked on {node.NodeInfo.Name}");
                
                // Handle multi-selection with Ctrl key
                if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                {
                    System.Diagnostics.Debug.WriteLine($"TreeViewItem Ctrl+Click detected on {node.NodeInfo.Name}");
                    ToggleNodeSelection(node);
                    _lastSelectedNode = node;
                    e.Handled = true; // Prevent normal selection
                }
                // Handle range selection with Shift key
                else if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                {
                    System.Diagnostics.Debug.WriteLine($"TreeViewItem Shift+Click detected on {node.NodeInfo.Name}, _lastSelectedNode={_lastSelectedNode?.NodeInfo.Name ?? "null"}");
                    SelectNodeRange(node);
                    e.Handled = true; // Prevent normal selection
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"TreeViewItem Normal click on {node.NodeInfo.Name}");
                    // Normal single selection - clear multi-selection and add current node
                    ClearMultiSelection();
                    AddNodeToSelection(node);
                    _lastSelectedNode = node;
                    UpdateMultiSelectionState();
                    System.Diagnostics.Debug.WriteLine($"TreeViewItem _lastSelectedNode set to {node.NodeInfo.Name}");
                }
            }
        }

        /// <summary>
        /// Selects a range of nodes from the last selected node to the current node
        /// </summary>
        private void SelectNodeRange(TreeNodeViewModel endNode)
        {
            System.Diagnostics.Debug.WriteLine($"SelectNodeRange called: endNode={endNode.NodeInfo.Name}, _lastSelectedNode={_lastSelectedNode?.NodeInfo.Name ?? "null"}");
            
            if (_lastSelectedNode == null)
            {
                // No previous selection, just select the current node
                System.Diagnostics.Debug.WriteLine("No last selected node, selecting single node");
                ClearMultiSelection();
                AddNodeToSelection(endNode);
                _lastSelectedNode = endNode;
                UpdateMultiSelectionState();
                return;
            }

            // Check if both nodes have the same parent (siblings)
            var startParent = FindParentNode(_lastSelectedNode);
            var endParent = FindParentNode(endNode);
            
            if (startParent != endParent)
            {
                // Nodes are not siblings, fallback to single selection
                System.Diagnostics.Debug.WriteLine("Nodes are not siblings, fallback to single selection");
                ClearMultiSelection();
                AddNodeToSelection(endNode);
                _lastSelectedNode = endNode;
                UpdateMultiSelectionState();
                return;
            }

            // Get the sibling nodes from the parent
            IList<TreeNodeViewModel>? siblings = null;
            
            // Try to get ViewModel from both the property and DataContext
            var viewModel = ViewModel ?? (DataContext as NavigationTreeViewModel);
            
            if (startParent != null)
            {
                // Both nodes have the same non-null parent, use parent's children
                siblings = startParent.Children;
            }
            else
            {
                // Both nodes are at root level, use root nodes
                siblings = viewModel?.RootNodes;
            }
            

            
            if (siblings == null || siblings.Count == 0)
            {
                // No siblings found, fallback to single selection
                System.Diagnostics.Debug.WriteLine("No siblings found, fallback to single selection");
                ClearMultiSelection();
                AddNodeToSelection(endNode);
                _lastSelectedNode = endNode;
                UpdateMultiSelectionState();
                return;
            }

            // Find the indices of the start and end nodes within siblings
            var startIndex = siblings.IndexOf(_lastSelectedNode);
            var endIndex = siblings.IndexOf(endNode);
            
            System.Diagnostics.Debug.WriteLine($"Start index: {startIndex}, End index: {endIndex} in {siblings.Count} siblings");
            
            if (startIndex == -1 || endIndex == -1)
            {
                // One of the nodes is not found in siblings, fallback to single selection
                System.Diagnostics.Debug.WriteLine("One of the nodes not found in siblings, fallback to single selection");
                ClearMultiSelection();
                AddNodeToSelection(endNode);
                _lastSelectedNode = endNode;
                UpdateMultiSelectionState();
                return;
            }

            // Ensure start index is less than end index
            if (startIndex > endIndex)
            {
                (startIndex, endIndex) = (endIndex, startIndex);
            }

            System.Diagnostics.Debug.WriteLine($"Selecting range from {startIndex} to {endIndex}");

            // Clear current selection and select the range
            ClearMultiSelection();
            
            for (int i = startIndex; i <= endIndex; i++)
            {
                AddNodeToSelection(siblings[i]);
                System.Diagnostics.Debug.WriteLine($"Added to selection: {siblings[i].NodeInfo.Name}");
            }
            
            UpdateMultiSelectionState();
        }

        /// <summary>
        /// Gets all visible nodes in the tree in display order
        /// </summary>
        private List<TreeNodeViewModel> GetAllVisibleNodes()
        {
            var visibleNodes = new List<TreeNodeViewModel>();
            
            if (ViewModel?.RootNodes != null)
            {
                foreach (var rootNode in ViewModel.RootNodes)
                {
                    AddVisibleNodesRecursive(rootNode, visibleNodes);
                }
            }
            
            return visibleNodes;
        }

        /// <summary>
        /// Recursively adds visible nodes to the list in display order
        /// </summary>
        private void AddVisibleNodesRecursive(TreeNodeViewModel node, List<TreeNodeViewModel> visibleNodes)
        {
            #if DEBUG
            System.Diagnostics.Debug.WriteLine($"AddVisibleNodesRecursive: {node.NodeInfo.Name}, IsVisible={node.IsVisible}, IsExpanded={node.IsExpanded}, Children={node.Children.Count}");
            #endif
            
            if (node.IsVisible)
            {
                visibleNodes.Add(node);
                #if DEBUG
                System.Diagnostics.Debug.WriteLine($"AddVisibleNodesRecursive: Added {node.NodeInfo.Name} to visible nodes (total now: {visibleNodes.Count})");
                #endif
                
                // If the node is expanded, add its children
                if (node.IsExpanded && node.Children.Count > 0)
                {
                    foreach (var child in node.Children)
                    {
                        AddVisibleNodesRecursive(child, visibleNodes);
                    }
                }
            }
            else
            {
                #if DEBUG
                System.Diagnostics.Debug.WriteLine($"AddVisibleNodesRecursive: Skipping {node.NodeInfo.Name} because IsVisible=false");
                #endif
            }
        }

        /// <summary>
        /// Finds the parent node of the given node
        /// </summary>
        /// <param name="node">The node to find the parent for</param>
        /// <returns>The parent node, or null if the node is a root node</returns>
        private TreeNodeViewModel? FindParentNode(TreeNodeViewModel node)
        {
            #if DEBUG
            System.Diagnostics.Debug.WriteLine($"FindParentNode: Looking for parent of {node.NodeInfo.Name}");
            #endif
            
            // Try to get ViewModel from both the property and DataContext
            var viewModel = ViewModel ?? (DataContext as NavigationTreeViewModel);
            
            if (viewModel?.RootNodes == null)
            {
                #if DEBUG
                System.Diagnostics.Debug.WriteLine($"FindParentNode: ViewModel or RootNodes is null");
                #endif
                return null;
            }

            // Check if it's a root node
            if (viewModel.RootNodes.Contains(node))
            {
                #if DEBUG
                System.Diagnostics.Debug.WriteLine($"FindParentNode: {node.NodeInfo.Name} is a root node");
                #endif
                return null;
            }

            // Search recursively through all nodes
            var parent = FindParentNodeRecursive(node, viewModel.RootNodes);
            
            #if DEBUG
            System.Diagnostics.Debug.WriteLine($"FindParentNode: Parent of {node.NodeInfo.Name} is {parent?.NodeInfo.Name ?? "null"}");
            #endif
            
            return parent;
        }

        /// <summary>
        /// Recursively searches for the parent of a node
        /// </summary>
        /// <param name="targetNode">The node to find the parent for</param>
        /// <param name="searchNodes">The collection of nodes to search in</param>
        /// <returns>The parent node, or null if not found</returns>
        private TreeNodeViewModel? FindParentNodeRecursive(TreeNodeViewModel targetNode, IEnumerable<TreeNodeViewModel> searchNodes)
        {
            foreach (var node in searchNodes)
            {
                // Check if this node is the parent
                if (node.Children.Contains(targetNode))
                    return node;

                // Recursively search in children
                var result = FindParentNodeRecursive(targetNode, node.Children);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Adds a node to the selection without toggling
        /// </summary>
        private void AddNodeToSelection(TreeNodeViewModel node)
        {
            if (!_selectedNodes.Contains(node))
            {
                _selectedNodes.Add(node);
                node.IsMultiSelected = true;
                // Clear WPF's IsSelected to prevent visual conflict
                node.IsSelected = false;
            }
        }



        /// <summary>
        /// Updates multi-selection state and commands
        /// </summary>
        private void UpdateMultiSelectionState()
        {
            // Update multi-selection state
            IsMultiSelectionActive = _selectedNodes.Count > 1;

            // Update command state
            if (ExtractSelectedCommand is AsyncRelayCommand extractCommand)
            {
                extractCommand.NotifyCanExecuteChanged();
            }

            // Raise multi-selection event for status bar updates
            MultipleNodesSelected?.Invoke(this, new MultipleNodesSelectedEventArgs(_selectedNodes.ToList()));
            
            System.Diagnostics.Debug.WriteLine($"Multi-selection state updated: {_selectedNodes.Count} nodes selected");
        }

        /// <summary>
        /// Toggles the selection state of a node in multi-selection mode
        /// </summary>
        private void ToggleNodeSelection(TreeNodeViewModel node)
        {
            if (_selectedNodes.Contains(node))
            {
                // Remove from selection
                _selectedNodes.Remove(node);
                node.IsMultiSelected = false;
                // Fix: Also clear WPF's IsSelected to prevent visual conflict
                node.IsSelected = false;

            }
            else
            {
                // Add to selection
                _selectedNodes.Add(node);
                node.IsMultiSelected = true;
                // Fix: Clear WPF's IsSelected to prevent visual conflict
                node.IsSelected = false;

            }

            // Update multi-selection state
            IsMultiSelectionActive = _selectedNodes.Count > 1;

            // Update command state
            if (ExtractSelectedCommand is AsyncRelayCommand extractCommand)
            {
                extractCommand.NotifyCanExecuteChanged();
            }

            // Raise multi-selection event for status bar updates
            MultipleNodesSelected?.Invoke(this, new MultipleNodesSelectedEventArgs(_selectedNodes.ToList()));
            
            System.Diagnostics.Debug.WriteLine($"Multi-selection: {_selectedNodes.Count} nodes selected");
        }



        /// <summary>
        /// Handles Shift+Arrow key selection for range selection
        /// </summary>
        private void HandleShiftArrowSelection(Key key)
        {
            var allNodes = GetAllVisibleNodes();
            var currentNode = NavigationTree.SelectedItem as TreeNodeViewModel;
            
            if (currentNode == null || allNodes.Count == 0)
                return;

            var currentIndex = allNodes.IndexOf(currentNode);
            if (currentIndex == -1)
                return;

            TreeNodeViewModel? targetNode = null;

            if (key == Key.Up && currentIndex > 0)
            {
                targetNode = allNodes[currentIndex - 1];
            }
            else if (key == Key.Down && currentIndex < allNodes.Count - 1)
            {
                targetNode = allNodes[currentIndex + 1];
            }

            if (targetNode != null)
            {
                // Set the new selection in the TreeView
                NavigationTree.Focus();
                
                // Find and select the target TreeViewItem
                var container = NavigationTree.ItemContainerGenerator.ContainerFromItem(targetNode) as TreeViewItem;
                if (container != null)
                {
                    container.IsSelected = true;
                    container.Focus();
                }

                // Perform range selection
                SelectNodeRange(targetNode);
            }
        }

        /// <summary>
        /// Clears all multi-selected nodes
        /// </summary>
        private void ClearMultiSelection()
        {
            foreach (var node in _selectedNodes)
            {
                node.IsMultiSelected = false;
                // Fix: Also clear WPF's IsSelected to prevent visual conflict
                node.IsSelected = false;
            }
            _selectedNodes.Clear();
            
            // Update multi-selection state
            IsMultiSelectionActive = false;
            
            // Update command state
            if (ExtractSelectedCommand is AsyncRelayCommand extractCommand)
            {
                extractCommand.NotifyCanExecuteChanged();
            }

            // Raise multi-selection event for status bar updates (empty selection)
            MultipleNodesSelected?.Invoke(this, new MultipleNodesSelectedEventArgs(_selectedNodes.ToList()));
        }





        /// <summary>
        /// Gets the currently selected nodes (including multi-selection)
        /// </summary>
        public IReadOnlyList<TreeNodeViewModel> SelectedNodes => _selectedNodes.AsReadOnly();

        /// <summary>
        /// Extracts all selected nodes to a chosen destination folder
        /// </summary>
        private async Task ExtractSelectedNodesAsync()
        {
            if (_selectedNodes.Count == 0)
                return;



            try
            {
                // Show folder browser dialog to select destination
                var folderDialog = new OpenFolderDialog
                {
                    Title = $"Select destination folder for {_selectedNodes.Count} selected items",
                    Multiselect = false
                };

                if (folderDialog.ShowDialog() == true)
                {
                    var destinationPath = folderDialog.FolderName;
                    
                    // Get the file operations service from the first node (they all share the same service)
                    var fileOperationsService = _selectedNodes[0].GetFileOperationsService();
                    if (fileOperationsService == null)
                    {
                        var toastService = GetToastService();
                        if (toastService != null)
                        {
                            toastService.ShowError("File operations service is not available.", "Extraction Error");
                        }
                        else
                        {
                            MessageBox.Show("File operations service is not available.", "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                        return;
                    }
                    
                    // Collect all paths to extract
                    var sourcePaths = _selectedNodes.Select(node => node.NodeInfo.FullPath).ToArray();
                    
                    // Show progress dialog or status
                    var itemNames = _selectedNodes.Select(node => node.NodeInfo.Name).ToList();
                    var itemsText = itemNames.Count <= 3 
                        ? string.Join(", ", itemNames)
                        : $"{string.Join(", ", itemNames.Take(2))} and {itemNames.Count - 2} more items";
                    
                    // Extract all selected items
                    var results = await fileOperationsService.ExtractMultipleAsync(sourcePaths, destinationPath);
                    
                    if (results.IsSuccess)
                    {
                        var toastService = GetToastService();
                        if (toastService != null)
                        {
                            toastService.ShowSuccess($"Successfully extracted {_selectedNodes.Count} items to: {destinationPath}", "Extraction Complete");
                        }
                        else
                        {
                            MessageBox.Show($"Successfully extracted {_selectedNodes.Count} items to:\n{destinationPath}\n\nItems: {itemsText}", "Extraction Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        
                        // Clear selection after successful extraction
                        ClearMultiSelection();
                    }
                    else
                    {
                        var errorMessage = results.Errors.Length > 0 
                            ? $"Failed to extract selected items:\n{string.Join("\n", results.Errors.Take(5).Select(e => $"• {e.ErrorMessage}"))}"
                            : "Failed to extract selected items";
                            
                        if (results.Errors.Length > 5)
                        {
                            errorMessage += $"\n... and {results.Errors.Length - 5} more errors";
                        }
                            
                        var toastService = GetToastService();
                        if (toastService != null)
                        {
                            toastService.ShowError(errorMessage, "Extraction Error");
                        }
                        else
                        {
                            MessageBox.Show(errorMessage, "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var toastService = GetToastService();
                if (toastService != null)
                {
                    toastService.ShowError($"Error extracting selected items: {ex.Message}", "Extraction Error");
                }
                else
                {
                    MessageBox.Show($"Error extracting selected items: {ex.Message}", "Extraction Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// Handles context menu opening to dynamically add multi-selection options
        /// </summary>
        private void NodeContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu)
            {
                // Remove any existing multi-selection items (check for items that start with "Extract Selected")
                var existingMultiItems = contextMenu.Items.OfType<MenuItem>()
                    .Where(item => item.Header?.ToString()?.StartsWith("Extract Selected") == true)
                    .ToList();
                foreach (var item in existingMultiItems)
                {
                    contextMenu.Items.Remove(item);
                }
                
                // Also remove any separators that might have been added for multi-selection
                var separators = contextMenu.Items.OfType<Separator>().ToList();
                foreach (var separator in separators)
                {
                    contextMenu.Items.Remove(separator);
                }

                // Add multi-selection extract option if multiple items are selected
                if (_selectedNodes.Count > 1)
                {
                    var extractSelectedItem = new MenuItem
                    {
                        Header = $"Extract Selected Items ({_selectedNodes.Count})...",
                        Command = ExtractSelectedCommand
                    };
                    
                    // Add icon
                    var icon = new Wpf.Ui.Controls.SymbolIcon
                    {
                        Symbol = Wpf.Ui.Controls.SymbolRegular.FolderArrowUp24
                    };
                    extractSelectedItem.Icon = icon;

                    // Insert at the top of the context menu
                    contextMenu.Items.Insert(0, extractSelectedItem);
                    
                    // Add separator after multi-selection item
                    contextMenu.Items.Insert(1, new Separator());
                }
            }
        }



        /// <summary>
        /// Finds a parent of the specified type in the visual tree
        /// </summary>
        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;

            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent is T result)
                return result;

            return FindParent<T>(parent);
        }
    }
}