using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GGPKExplorer.Models;
using GGPKExplorer.Services;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// ViewModel for search functionality with real-time filtering and global search
    /// Reference: docs/LibGGPK3_Deep_Research_Report.md - Search Operations section
    /// </summary>
    public partial class SearchViewModel : ObservableObject
    {
        private readonly ISearchService _searchService;
        private readonly IGGPKService _ggpkService;
        private CancellationTokenSource? _searchCancellationTokenSource;
        private CancellationTokenSource? _filterCancellationTokenSource;

        /// <summary>
        /// Collection of search results
        /// </summary>
        public ObservableCollection<SearchResult> SearchResults { get; } = new();

        /// <summary>
        /// Collection view for search results with sorting and filtering
        /// </summary>
        public ICollectionView SearchResultsView { get; }

        /// <summary>
        /// Collection of filtered items for real-time filtering
        /// </summary>
        public ObservableCollection<TreeNodeInfo> FilteredItems { get; } = new();

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private bool _isSearching = false;

        [ObservableProperty]
        private bool _isFiltering = false;



        [ObservableProperty]
        private bool _matchCase = false;

        [ObservableProperty]
        private bool _useRegex = false;

        [ObservableProperty]
        private SimpleFileTypeFilter _selectedFileTypeFilter = SimpleFileTypeFilter.All;

        [ObservableProperty]
        private SearchResult? _selectedSearchResult;

        [ObservableProperty]
        private string _searchStatusText = "Ready";

        [ObservableProperty]
        private double _searchProgress = 0.0;

        [ObservableProperty]
        private int _totalResults = 0;

        [ObservableProperty]
        private TimeSpan _searchTime = TimeSpan.Zero;

        [ObservableProperty]
        private string _currentPath = "/";

        [ObservableProperty]
        private bool _showSearchResults = false;

        public SearchViewModel(ISearchService searchService, IGGPKService ggpkService)
        {
            _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
            _ggpkService = ggpkService ?? throw new ArgumentNullException(nameof(ggpkService));

            // Set up collection view for search results
            SearchResultsView = CollectionViewSource.GetDefaultView(SearchResults);
            SearchResultsView.SortDescriptions.Add(new SortDescription(nameof(SearchResult.RelevanceScore), ListSortDirection.Descending));

            // Register for navigation messages to update current path
            WeakReferenceMessenger.Default.Register<NavigationMessage>(this, OnNavigationMessage);
        }

        /// <summary>
        /// Command to perform a global search
        /// </summary>
        [RelayCommand]
        private async Task SearchAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                ClearSearchResults();
                return;
            }

            try
            {
                // Cancel any existing search
                _searchCancellationTokenSource?.Cancel();
                _searchCancellationTokenSource = new CancellationTokenSource();

                IsSearching = true;
                ShowSearchResults = true;
                SearchStatusText = "Searching...";
                SearchProgress = 0.0;

                var options = new SearchOptions
                {
                    Query = SearchQuery,
                    MatchCase = MatchCase,
                    UseRegex = UseRegex,
                    TypeFilter = ConvertToFileTypeFilter(SelectedFileTypeFilter),
                    MaxResults = 1000,
                    SearchInPaths = true
                };

                var progress = new Progress<SearchProgressInfo>(OnSearchProgress);
                var results = await _searchService.SearchAsync(options, progress, _searchCancellationTokenSource.Token);

                // Update results on UI thread
                SearchResults.Clear();
                foreach (var result in results.Results)
                {
                    SearchResults.Add(result);
                }

                TotalResults = results.Results.Count;
                SearchTime = results.SearchTime;

                if (results.WasCancelled)
                {
                    SearchStatusText = "Search cancelled";
                }
                else if (!string.IsNullOrEmpty(results.ErrorMessage))
                {
                    SearchStatusText = $"Search error: {results.ErrorMessage}";
                }
                else
                {
                    SearchStatusText = $"Found {TotalResults} results in {SearchTime.TotalMilliseconds:F0}ms";
                }
            }
            catch (OperationCanceledException)
            {
                SearchStatusText = "Search cancelled";
            }
            catch (Exception ex)
            {
                SearchStatusText = $"Search error: {ex.Message}";
            }
            finally
            {
                IsSearching = false;
                SearchProgress = 100.0;
            }
        }

        /// <summary>
        /// Command to perform real-time filtering
        /// </summary>
        [RelayCommand]
        private async Task FilterAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchQuery))
            {
                FilteredItems.Clear();
                return;
            }

            try
            {
                // Cancel any existing filter operation
                _filterCancellationTokenSource?.Cancel();
                _filterCancellationTokenSource = new CancellationTokenSource();

                IsFiltering = true;

                var results = await _searchService.QuickFilterAsync(SearchQuery, CurrentPath, _filterCancellationTokenSource.Token);

                // Update filtered items on UI thread
                FilteredItems.Clear();
                foreach (var item in results)
                {
                    FilteredItems.Add(item);
                }
            }
            catch (OperationCanceledException)
            {
                // Filter was cancelled, ignore
            }
            catch (Exception)
            {
                // Filter failed, clear results
                FilteredItems.Clear();
            }
            finally
            {
                IsFiltering = false;
            }
        }

        /// <summary>
        /// Command to cancel the current search operation
        /// </summary>
        [RelayCommand]
        private void CancelSearch()
        {
            _searchCancellationTokenSource?.Cancel();
            _filterCancellationTokenSource?.Cancel();
            SearchStatusText = "Search cancelled";
        }

        /// <summary>
        /// Command to clear search results
        /// </summary>
        [RelayCommand]
        private void ClearSearchResults()
        {
            _searchCancellationTokenSource?.Cancel();
            _filterCancellationTokenSource?.Cancel();
            
            SearchResults.Clear();
            FilteredItems.Clear();
            TotalResults = 0;
            SearchTime = TimeSpan.Zero;
            SearchProgress = 0.0;
            SearchStatusText = "Ready";
            ShowSearchResults = false;
        }

        /// <summary>
        /// Command to navigate to a search result
        /// </summary>
        [RelayCommand]
        private void NavigateToResult(SearchResult? result)
        {
            if (result?.NodeInfo != null)
            {
                // Send navigation message to other components
                WeakReferenceMessenger.Default.Send(new NavigationMessage(result.NodeInfo.FullPath));
                
                // If it's a file, navigate to its parent directory and select the file
                if (result.NodeInfo.Type != NodeType.Directory)
                {
                    var parentPath = System.IO.Path.GetDirectoryName(result.NodeInfo.FullPath)?.Replace('\\', '/') ?? "/";
                    WeakReferenceMessenger.Default.Send(new NavigationMessage(parentPath));
                }
            }
        }

        /// <summary>
        /// Command to toggle between local and global search
        /// </summary>

        /// <summary>
        /// Handles search progress updates
        /// </summary>
        private void OnSearchProgress(SearchProgressInfo progress)
        {
            SearchProgress = progress.Percentage;
            SearchStatusText = $"{progress.Operation} ({progress.MatchesFound} matches found)";
        }

        /// <summary>
        /// Handles navigation messages to update current path
        /// </summary>
        private void OnNavigationMessage(object recipient, NavigationMessage message)
        {
            CurrentPath = message.Path;
            
            // Always perform global search now
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                _ = SearchAsync();
            }
        }

        /// <summary>
        /// Handles changes to the search query
        /// </summary>
        partial void OnSearchQueryChanged(string value)
        {
            // Debounce the search/filter operation
            _ = Task.Delay(300).ContinueWith(async _ =>
            {
                if (SearchQuery == value) // Make sure the query hasn't changed
                {
                    await SearchAsync();
                }
            });
        }

        /// <summary>
        /// Handles changes to search options that should trigger a new search
        /// </summary>
        partial void OnMatchCaseChanged(bool value)
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                _ = SearchAsync();
            }
        }

        partial void OnUseRegexChanged(bool value)
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                _ = SearchAsync();
            }
        }

        partial void OnSelectedFileTypeFilterChanged(SimpleFileTypeFilter value)
        {
            if (!string.IsNullOrWhiteSpace(SearchQuery))
            {
                _ = SearchAsync();
            }
        }

        /// <summary>
        /// Converts SimpleFileTypeFilter to FileTypeFilter
        /// </summary>
        private FileTypeFilter ConvertToFileTypeFilter(SimpleFileTypeFilter simpleFilter)
        {
            return simpleFilter switch
            {
                SimpleFileTypeFilter.All => new FileTypeFilter
                {
                    IncludeFiles = true,
                    IncludeDirectories = true,
                    IncludeBundleFiles = true,
                    IncludeCompressedFiles = true
                },
                SimpleFileTypeFilter.FilesOnly => new FileTypeFilter
                {
                    IncludeFiles = true,
                    IncludeDirectories = false,
                    IncludeBundleFiles = true,
                    IncludeCompressedFiles = true
                },
                SimpleFileTypeFilter.DirectoriesOnly => new FileTypeFilter
                {
                    IncludeFiles = false,
                    IncludeDirectories = true,
                    IncludeBundleFiles = false,
                    IncludeCompressedFiles = false
                },
                SimpleFileTypeFilter.Images => new FileTypeFilter
                {
                    IncludeFiles = true,
                    IncludeDirectories = false,
                    IncludeBundleFiles = true,
                    IncludeCompressedFiles = true,
                    IncludeExtensions = new[] { ".dds", ".png", ".jpg", ".jpeg", ".bmp", ".tga" }
                },
                SimpleFileTypeFilter.Audio => new FileTypeFilter
                {
                    IncludeFiles = true,
                    IncludeDirectories = false,
                    IncludeBundleFiles = true,
                    IncludeCompressedFiles = true,
                    IncludeExtensions = new[] { ".ogg", ".wav", ".mp3" }
                },
                SimpleFileTypeFilter.Text => new FileTypeFilter
                {
                    IncludeFiles = true,
                    IncludeDirectories = false,
                    IncludeBundleFiles = true,
                    IncludeCompressedFiles = true,
                    IncludeExtensions = new[] { ".txt", ".xml", ".json", ".lua", ".hlsl", ".fx" }
                },
                SimpleFileTypeFilter.Data => new FileTypeFilter
                {
                    IncludeFiles = true,
                    IncludeDirectories = false,
                    IncludeBundleFiles = true,
                    IncludeCompressedFiles = true,
                    IncludeExtensions = new[] { ".dat", ".bin" }
                },
                _ => new FileTypeFilter()
            };
        }

        /// <summary>
        /// Cleanup resources
        /// </summary>
        public void Cleanup()
        {
            _searchCancellationTokenSource?.Cancel();
            _filterCancellationTokenSource?.Cancel();
            
            WeakReferenceMessenger.Default.Unregister<NavigationMessage>(this);
            
            _searchCancellationTokenSource?.Dispose();
            _filterCancellationTokenSource?.Dispose();
        }
    }
}