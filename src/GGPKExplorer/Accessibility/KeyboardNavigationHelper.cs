using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GGPKExplorer.ViewModels;

namespace GGPKExplorer.Accessibility
{
    /// <summary>
    /// Helper class for implementing keyboard navigation throughout the application
    /// </summary>
    public static class KeyboardNavigationHelper
    {
        /// <summary>
        /// Sets up keyboard navigation for the main window
        /// </summary>
        /// <param name="window">Main window to configure</param>
        public static void SetupMainWindowNavigation(Window window)
        {
            if (window == null) return;

            // Set up global keyboard shortcuts
            window.InputBindings.Clear();
            
            // File operations
            window.InputBindings.Add(new KeyBinding(ApplicationCommands.Open, Key.O, ModifierKeys.Control));
            window.InputBindings.Add(new KeyBinding(ApplicationCommands.Close, Key.W, ModifierKeys.Control));
            window.InputBindings.Add(new KeyBinding(ApplicationCommands.Properties, Key.Enter, ModifierKeys.Alt));
            
            // Navigation
            window.InputBindings.Add(new KeyBinding(NavigationCommands.Refresh, Key.F5, ModifierKeys.None));
            window.InputBindings.Add(new KeyBinding(NavigationCommands.BrowseBack, Key.Left, ModifierKeys.Alt));
            window.InputBindings.Add(new KeyBinding(NavigationCommands.BrowseForward, Key.Right, ModifierKeys.Alt));
            
            // Search
            window.InputBindings.Add(new KeyBinding(ApplicationCommands.Find, Key.F, ModifierKeys.Control));
            window.InputBindings.Add(new KeyBinding(ApplicationCommands.Find, Key.F3, ModifierKeys.None));
            
            // Selection
            window.InputBindings.Add(new KeyBinding(ApplicationCommands.SelectAll, Key.A, ModifierKeys.Control));
            
            // Application
            window.InputBindings.Add(new KeyBinding(ApplicationCommands.Help, Key.F1, ModifierKeys.None));
            
            // Set up tab navigation
            KeyboardNavigation.SetTabNavigation(window, KeyboardNavigationMode.Continue);
            KeyboardNavigation.SetControlTabNavigation(window, KeyboardNavigationMode.Continue);
        }

        /// <summary>
        /// Sets up keyboard navigation for TreeView
        /// </summary>
        /// <param name="treeView">TreeView to configure</param>
        public static void SetupTreeViewNavigation(TreeView treeView)
        {
            if (treeView == null) return;

            treeView.KeyDown += TreeView_KeyDown;
            treeView.PreviewKeyDown += TreeView_PreviewKeyDown;
            
            // Set tab index for proper navigation order
            treeView.TabIndex = 1;
            treeView.IsTabStop = true;
            
            // Configure keyboard navigation
            KeyboardNavigation.SetTabNavigation(treeView, KeyboardNavigationMode.Continue);
            KeyboardNavigation.SetDirectionalNavigation(treeView, KeyboardNavigationMode.Continue);
        }

        /// <summary>
        /// Sets up keyboard navigation for ListView
        /// </summary>
        /// <param name="listView">ListView to configure</param>
        public static void SetupListViewNavigation(ListView listView)
        {
            if (listView == null) return;

            listView.KeyDown += ListView_KeyDown;
            listView.PreviewKeyDown += ListView_PreviewKeyDown;
            
            // Set tab index for proper navigation order
            listView.TabIndex = 2;
            listView.IsTabStop = true;
            
            // Configure keyboard navigation
            KeyboardNavigation.SetTabNavigation(listView, KeyboardNavigationMode.Continue);
            KeyboardNavigation.SetDirectionalNavigation(listView, KeyboardNavigationMode.Continue);
        }

        /// <summary>
        /// Sets up keyboard navigation for dialogs
        /// </summary>
        /// <param name="dialog">Dialog to configure</param>
        public static void SetupDialogNavigation(Window dialog)
        {
            if (dialog == null) return;

            // Handle Escape key to close dialog
            dialog.KeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    dialog.DialogResult = false;
                    e.Handled = true;
                }
            };

            // Set up tab navigation
            KeyboardNavigation.SetTabNavigation(dialog, KeyboardNavigationMode.Cycle);
            KeyboardNavigation.SetControlTabNavigation(dialog, KeyboardNavigationMode.Cycle);
        }

        private static void TreeView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var treeView = sender as TreeView;
            if (treeView == null) return;

            // Handle special keys that need preview handling
            switch (e.Key)
            {
                case Key.F2:
                    // Rename functionality (future implementation)
                    e.Handled = true;
                    break;
                case Key.Delete:
                    // Delete functionality (future implementation)
                    e.Handled = true;
                    break;
            }
        }

        private static void TreeView_KeyDown(object sender, KeyEventArgs e)
        {
            var treeView = sender as TreeView;
            if (treeView?.SelectedItem is TreeNodeViewModel selectedNode)
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        // Navigate to selected item - use ToggleExpandCommand instead
                        if (selectedNode.ToggleExpandCommand?.CanExecute(null) == true)
                        {
                            selectedNode.ToggleExpandCommand.Execute(null);
                            e.Handled = true;
                        }
                        break;

                    case Key.Space:
                        // Toggle expansion
                        if (selectedNode.CanExpand)
                        {
                            selectedNode.IsExpanded = !selectedNode.IsExpanded;
                            e.Handled = true;
                        }
                        break;

                    case Key.Right:
                        // Expand if collapsed, or move to first child if expanded
                        if (selectedNode.CanExpand && !selectedNode.IsExpanded)
                        {
                            selectedNode.IsExpanded = true;
                            e.Handled = true;
                        }
                        break;

                    case Key.Left:
                        // Collapse if expanded, or move to parent if collapsed
                        if (selectedNode.CanExpand && selectedNode.IsExpanded)
                        {
                            selectedNode.IsExpanded = false;
                            e.Handled = true;
                        }
                        break;

                    case Key.Apps:
                    case Key.F10 when (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift:
                        // Show context menu
                        ShowContextMenu(treeView, selectedNode);
                        e.Handled = true;
                        break;
                }
            }
        }

        private static void ListView_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null) return;

            // Handle special keys that need preview handling
            switch (e.Key)
            {
                case Key.F2:
                    // Rename functionality (future implementation)
                    e.Handled = true;
                    break;
                case Key.Delete:
                    // Delete functionality (future implementation)
                    e.Handled = true;
                    break;
            }
        }

        private static void ListView_KeyDown(object sender, KeyEventArgs e)
        {
            var listView = sender as ListView;
            if (listView == null) return;

            switch (e.Key)
            {
                case Key.Enter:
                    // Open selected item
                    if (listView.SelectedItem != null)
                    {
                        // Simulate double-click
                        var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                        {
                            RoutedEvent = Control.MouseDoubleClickEvent
                        };
                        listView.RaiseEvent(args);
                        e.Handled = true;
                    }
                    break;

                case Key.Space:
                    // Toggle selection (for multi-select)
                    if (listView.SelectionMode == SelectionMode.Multiple || listView.SelectionMode == SelectionMode.Extended)
                    {
                        var focusedItem = GetFocusedListViewItem(listView);
                        if (focusedItem != null)
                        {
                            focusedItem.IsSelected = !focusedItem.IsSelected;
                            e.Handled = true;
                        }
                    }
                    break;

                case Key.A when (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control:
                    // Select all
                    listView.SelectAll();
                    e.Handled = true;
                    break;

                case Key.Apps:
                case Key.F10 when (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift:
                    // Show context menu
                    ShowContextMenu(listView, listView.SelectedItem);
                    e.Handled = true;
                    break;
            }
        }

        private static ListViewItem? GetFocusedListViewItem(ListView listView)
        {
            var focusedElement = Keyboard.FocusedElement as DependencyObject;
            while (focusedElement != null)
            {
                if (focusedElement is ListViewItem item && item.Parent == listView)
                {
                    return item;
                }
                focusedElement = VisualTreeHelper.GetParent(focusedElement);
            }
            return null;
        }

        private static void ShowContextMenu(FrameworkElement element, object dataContext)
        {
            if (element.ContextMenu != null)
            {
                element.ContextMenu.DataContext = dataContext;
                element.ContextMenu.PlacementTarget = element;
                element.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
                element.ContextMenu.IsOpen = true;
            }
        }

        /// <summary>
        /// Moves focus to the next focusable element
        /// </summary>
        /// <param name="element">Current element</param>
        /// <param name="forward">Direction to move focus</param>
        public static void MoveFocus(FrameworkElement element, bool forward = true)
        {
            if (element == null) return;

            var request = new TraversalRequest(forward ? FocusNavigationDirection.Next : FocusNavigationDirection.Previous);
            element.MoveFocus(request);
        }

        /// <summary>
        /// Sets focus to a specific element and announces it to screen readers
        /// </summary>
        /// <param name="element">Element to focus</param>
        /// <param name="announcement">Optional announcement text</param>
        public static void SetFocusWithAnnouncement(FrameworkElement element, string? announcement = null)
        {
            if (element == null) return;

            element.Focus();
            
            if (!string.IsNullOrEmpty(announcement))
            {
                AccessibilityHelper.AnnounceToScreenReader(announcement);
            }
        }
    }
}