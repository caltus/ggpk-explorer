using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GGPKExplorer.ViewModels;

namespace GGPKExplorer.Views
{
    /// <summary>
    /// Interaction logic for ExplorerView.xaml
    /// Implements the dual-pane explorer layout with TreeView and FileListView
    /// </summary>
    public partial class ExplorerView : UserControl
    {
        /// <summary>
        /// Gets or sets the view model for this view
        /// </summary>
        public ExplorerViewModel? ViewModel
        {
            get => DataContext as ExplorerViewModel;
            set => DataContext = value;
        }

        public ExplorerView()
        {
            InitializeComponent();
            
            // Set up keyboard shortcuts
            SetupKeyboardShortcuts();
        }

        /// <summary>
        /// Sets up keyboard shortcuts for the explorer view
        /// </summary>
        private void SetupKeyboardShortcuts()
        {
            // F5 key is reserved for future refresh functionality

            // Alt+Left - Go Back
            var backBinding = new KeyBinding(
                new RelayCommand(() => 
                {
                    if (ViewModel?.GoBackCommand.CanExecute(null) == true)
                        ViewModel.GoBackCommand.Execute(null);
                }),
                Key.Left, ModifierKeys.Alt);
            InputBindings.Add(backBinding);

            // Alt+Right - Go Forward
            var forwardBinding = new KeyBinding(
                new RelayCommand(() => 
                {
                    if (ViewModel?.GoForwardCommand.CanExecute(null) == true)
                        ViewModel.GoForwardCommand.Execute(null);
                }),
                Key.Right, ModifierKeys.Alt);
            InputBindings.Add(forwardBinding);

            // Ctrl+1 - Toggle Split View
            var toggleBinding = new KeyBinding(
                new RelayCommand(() => 
                {
                    if (ViewModel != null)
                        ViewModel.IsSplitViewOpen = !ViewModel.IsSplitViewOpen;
                }),
                Key.D1, ModifierKeys.Control);
            InputBindings.Add(toggleBinding);
        }

        /// <summary>
        /// Handles the Loaded event to initialize the view
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Focus the tree view when the control loads
            if (ViewModel?.TreeViewModel != null)
            {
                Keyboard.Focus(this);
            }
        }

        /// <summary>
        /// Handles the Unloaded event to clean up resources
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Clean up the view model when unloading
            ViewModel?.Cleanup();
        }

        /// <summary>
        /// Handles multiple nodes selection to update status bar
        /// </summary>
        private void NavigationTreeView_MultipleNodesSelected(object sender, MultipleNodesSelectedEventArgs e)
        {
            if (ViewModel != null)
            {
                var selectedCount = e.SelectedNodes.Count;
                if (selectedCount > 1)
                {
                    // Count files vs folders
                    var fileCount = e.SelectedNodes.Count(node => node.NodeInfo.Type == Models.NodeType.File || 
                                                                   node.NodeInfo.Type == Models.NodeType.BundleFile || 
                                                                   node.NodeInfo.Type == Models.NodeType.CompressedFile);
                    var folderCount = selectedCount - fileCount;
                    
                    string statusText;
                    if (fileCount > 0 && folderCount > 0)
                    {
                        statusText = $"Selected {selectedCount} items ({fileCount} files, {folderCount} folders)";
                    }
                    else if (fileCount > 0)
                    {
                        statusText = $"Selected {fileCount} file{(fileCount == 1 ? "" : "s")}";
                    }
                    else
                    {
                        statusText = $"Selected {folderCount} folder{(folderCount == 1 ? "" : "s")}";
                    }
                    
                    ViewModel.StatusText = statusText;
                }
                else if (selectedCount == 1)
                {
                    // Single selection - show the item name
                    var selectedNode = e.SelectedNodes.First();
                    ViewModel.StatusText = $"Selected: {selectedNode.NodeInfo.Name}";
                }
                else
                {
                    // No selection
                    ViewModel.StatusText = "Ready";
                }
            }
        }

        /// <summary>
        /// Simple relay command implementation for keyboard shortcuts
        /// </summary>
        private class RelayCommand : ICommand
        {
            private readonly System.Action _execute;
            private readonly System.Func<bool>? _canExecute;

            public RelayCommand(System.Action execute, System.Func<bool>? canExecute = null)
            {
                _execute = execute ?? throw new System.ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event System.EventHandler? CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object? parameter)
            {
                return _canExecute?.Invoke() ?? true;
            }

            public void Execute(object? parameter)
            {
                _execute();
            }
        }
    }
}