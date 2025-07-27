using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using GGPKExplorer.Models;
using GGPKExplorer.ViewModels;

namespace GGPKExplorer.Views
{
    /// <summary>
    /// Interaction logic for SearchView.xaml
    /// </summary>
    public partial class SearchView : UserControl
    {
        public SearchView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Handles double-click on search results to navigate
        /// </summary>
        private void OnSearchResultDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && 
                element.DataContext is SearchResult result &&
                DataContext is SearchViewModel viewModel)
            {
                viewModel.NavigateToResultCommand.Execute(result);
            }
        }

        /// <summary>
        /// Handles double-click on filtered items to navigate
        /// </summary>
        private void OnFilteredItemDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && 
                element.DataContext is TreeNodeInfo nodeInfo &&
                DataContext is SearchViewModel viewModel)
            {
                // Create a temporary search result for navigation
                var searchResult = new SearchResult
                {
                    NodeInfo = nodeInfo,
                    RelevanceScore = 1.0,
                    MatchType = SearchMatchType.Name
                };
                
                viewModel.NavigateToResultCommand.Execute(searchResult);
            }
        }

        /// <summary>
        /// Handles key down events for keyboard navigation
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (DataContext is not SearchViewModel viewModel)
                return;

            switch (e.Key)
            {
                case Key.Enter:
                    // Always perform global search
                    viewModel.SearchCommand.Execute(null);
                    e.Handled = true;
                    break;

                case Key.Escape:
                    // Escape to clear search
                    viewModel.ClearSearchResultsCommand.Execute(null);
                    e.Handled = true;
                    break;


            }
        }

        /// <summary>
        /// Handles context menu for search results
        /// </summary>
        private void OnSearchResultContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is SearchResult result)
            {
                var contextMenu = new ContextMenu();

                // Navigate to item
                var navigateItem = new MenuItem
                {
                    Header = "Navigate to Item",
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Navigation24 }
                };
                navigateItem.Click += (s, args) =>
                {
                    if (DataContext is SearchViewModel viewModel)
                        viewModel.NavigateToResultCommand.Execute(result);
                };
                contextMenu.Items.Add(navigateItem);

                // Copy path
                var copyPathItem = new MenuItem
                {
                    Header = "Copy Path",
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.Copy24 }
                };
                copyPathItem.Click += (s, args) =>
                {
                    Clipboard.SetText(result.NodeInfo.FullPath);
                };
                contextMenu.Items.Add(copyPathItem);

                // Copy name
                var copyNameItem = new MenuItem
                {
                    Header = "Copy Name",
                    Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = Wpf.Ui.Controls.SymbolRegular.TextCaseLowercase24 }
                };
                copyNameItem.Click += (s, args) =>
                {
                    Clipboard.SetText(result.NodeInfo.Name);
                };
                contextMenu.Items.Add(copyNameItem);

                element.ContextMenu = contextMenu;
            }
        }

        /// <summary>
        /// Handles bringing search results into view when navigating
        /// </summary>
        private void OnSearchResultSelected(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListBox listBox && listBox.SelectedItem != null)
            {
                // Bring the selected item into view
                listBox.ScrollIntoView(listBox.SelectedItem);
            }
        }
    }
}