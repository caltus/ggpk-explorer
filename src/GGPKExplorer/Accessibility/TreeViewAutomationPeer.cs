using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using GGPKExplorer.ViewModels;
using GGPKExplorer.Extensions;

namespace GGPKExplorer.Accessibility
{
    /// <summary>
    /// Custom automation peer for TreeView to provide enhanced accessibility support
    /// </summary>
    public class GGPKTreeViewAutomationPeer : TreeViewAutomationPeer, ISelectionProvider, IExpandCollapseProvider
    {
        private readonly TreeView _treeView;

        public GGPKTreeViewAutomationPeer(TreeView treeView) : base(treeView)
        {
            _treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
        }

        protected override string GetClassNameCore()
        {
            return "GGPKNavigationTreeView";
        }

        protected override string GetNameCore()
        {
            return "GGPK File Navigation Tree";
        }

        protected override string GetHelpTextCore()
        {
            return "Navigate through GGPK file structure. Use arrow keys to navigate, Enter to select, Space to expand/collapse folders.";
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Tree;
        }

        public override object GetPattern(PatternInterface patternInterface)
        {
            switch (patternInterface)
            {
                case PatternInterface.Selection:
                    return this;
                case PatternInterface.ExpandCollapse:
                    return this;
                default:
                    return base.GetPattern(patternInterface);
            }
        }

        #region ISelectionProvider Implementation

        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => false;

        public IRawElementProviderSimple[] GetSelection()
        {
            var selectedItem = _treeView.SelectedItem;
            if (selectedItem == null)
                return new IRawElementProviderSimple[0];

            var container = _treeView.ItemContainerGenerator.ContainerFromItem(selectedItem) as TreeViewItem;
            if (container != null)
            {
                var peer = UIElementAutomationPeer.FromElement(container);
                if (peer != null)
                {
                    return new IRawElementProviderSimple[] { ProviderFromPeer(peer) };
                }
            }

            return new IRawElementProviderSimple[0];
        }

        #endregion

        #region IExpandCollapseProvider Implementation

        public ExpandCollapseState ExpandCollapseState
        {
            get
            {
                var selectedItem = _treeView.SelectedItem as TreeNodeViewModel;
                if (selectedItem == null)
                    return ExpandCollapseState.LeafNode;

                if (!selectedItem.CanExpand)
                    return ExpandCollapseState.LeafNode;

                return selectedItem.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
            }
        }

        public void Collapse()
        {
            var selectedItem = _treeView.SelectedItem as TreeNodeViewModel;
            if (selectedItem?.CanExpand == true && selectedItem.IsExpanded)
            {
                selectedItem.IsExpanded = false;
                RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    ExpandCollapseState.Expanded, ExpandCollapseState.Collapsed);
            }
        }

        public void Expand()
        {
            var selectedItem = _treeView.SelectedItem as TreeNodeViewModel;
            if (selectedItem?.CanExpand == true && !selectedItem.IsExpanded)
            {
                selectedItem.IsExpanded = true;
                RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded);
            }
        }

        #endregion

        /// <summary>
        /// Announces navigation changes to screen readers
        /// </summary>
        /// <param name="selectedNode">The newly selected node</param>
        public void AnnounceNavigation(TreeNodeViewModel selectedNode)
        {
            if (selectedNode == null) return;

            var announcement = $"Selected {selectedNode.DisplayName}";
            if (selectedNode.NodeInfo.Type == Models.NodeType.Directory)
            {
                announcement += selectedNode.CanExpand ? 
                    (selectedNode.IsExpanded ? ", expanded folder" : ", collapsed folder") : 
                    ", empty folder";
            }
            else
            {
                announcement += $", file, size {selectedNode.NodeInfo.Size} bytes";
            }

            AccessibilityHelper.AnnounceToScreenReader(announcement, AutomationNotificationKind.ItemAdded);
        }
    }

    /// <summary>
    /// Custom automation peer for TreeViewItem to provide enhanced accessibility support
    /// </summary>
    public class GGPKTreeViewItemAutomationPeer : TreeViewItemAutomationPeer, IExpandCollapseProvider, ISelectionItemProvider
    {
        private readonly TreeViewItem _treeViewItem;
        private readonly TreeNodeViewModel? _viewModel;

        public GGPKTreeViewItemAutomationPeer(TreeViewItem treeViewItem) : base(treeViewItem)
        {
            _treeViewItem = treeViewItem ?? throw new ArgumentNullException(nameof(treeViewItem));
            _viewModel = treeViewItem.DataContext as TreeNodeViewModel ?? null;
        }

        protected override string GetClassNameCore()
        {
            return "GGPKTreeViewItem";
        }

        protected override string GetNameCore()
        {
            return _viewModel?.DisplayName ?? base.GetNameCore();
        }

        protected override string GetHelpTextCore()
        {
            if (_viewModel == null) return base.GetHelpTextCore();

            var helpText = $"{_viewModel.DisplayName}";
            if (_viewModel.NodeInfo.Type == Models.NodeType.Directory)
            {
                helpText += _viewModel.CanExpand ? " folder" : " empty folder";
                if (_viewModel.CanExpand)
                {
                    helpText += _viewModel.IsExpanded ? ", expanded" : ", collapsed";
                }
            }
            else
            {
                helpText += $" file, {_viewModel.NodeInfo.Size} bytes";
            }

            return helpText;
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.TreeItem;
        }

        public override object? GetPattern(PatternInterface patternInterface)
        {
            switch (patternInterface)
            {
                case PatternInterface.ExpandCollapse:
                    return _viewModel?.CanExpand == true ? this : null;
                case PatternInterface.SelectionItem:
                    return this;
                default:
                    return base.GetPattern(patternInterface);
            }
        }

        #region IExpandCollapseProvider Implementation

        public ExpandCollapseState ExpandCollapseState
        {
            get
            {
                if (_viewModel?.CanExpand != true)
                    return ExpandCollapseState.LeafNode;

                return _viewModel.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
            }
        }

        public void Collapse()
        {
            if (_viewModel?.CanExpand == true && _viewModel.IsExpanded)
            {
                _viewModel.IsExpanded = false;
                RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    ExpandCollapseState.Expanded, ExpandCollapseState.Collapsed);
            }
        }

        public void Expand()
        {
            if (_viewModel?.CanExpand == true && !_viewModel.IsExpanded)
            {
                _viewModel.IsExpanded = true;
                RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded);
            }
        }

        #endregion

        #region ISelectionItemProvider Implementation

        public bool IsSelected => _viewModel?.IsSelected == true;

        public IRawElementProviderSimple? SelectionContainer
        {
            get
            {
                var treeView = _treeViewItem.FindAncestor<TreeView>();
                if (treeView != null)
                {
                    var peer = UIElementAutomationPeer.FromElement(treeView);
                    return peer != null ? ProviderFromPeer(peer) : null;
                }
                return null;
            }
        }

        public void AddToSelection()
        {
            // TreeView doesn't support multi-selection in this implementation
            Select();
        }

        public void RemoveFromSelection()
        {
            if (_viewModel?.IsSelected == true)
            {
                _viewModel.IsSelected = false;
            }
        }

        public void Select()
        {
            if (_viewModel != null)
            {
                _viewModel.IsSelected = true;
                RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, false, true);
            }
        }

        #endregion
    }
}