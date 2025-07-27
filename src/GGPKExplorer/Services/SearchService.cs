using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{


    /// <summary>
    /// Service for performing search operations on GGPK files
    /// Reference: docs/LibGGPK3_Deep_Research_Report.md - Search Operations section
    /// </summary>
    public class SearchService : ISearchService
    {
        private readonly IGGPKService _ggpkService;

        public SearchService(IGGPKService ggpkService)
        {
            _ggpkService = ggpkService ?? throw new ArgumentNullException(nameof(ggpkService));
        }

        /// <summary>
        /// Performs a search operation with the specified options
        /// </summary>
        /// <param name="options">Search options</param>
        /// <param name="progress">Progress reporting callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Search results</returns>
        public async Task<SearchResults> SearchAsync(SearchOptions options, IProgress<SearchProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            if (string.IsNullOrWhiteSpace(options.Query))
                return new SearchResults { Options = options };

            var stopwatch = Stopwatch.StartNew();
            var results = new SearchResults { Options = options };

            try
            {
                // Prepare regex if needed
                Regex? searchRegex = null;
                if (options.UseRegex)
                {
                    try
                    {
                        var regexOptions = RegexOptions.Compiled;
                        if (!options.MatchCase)
                            regexOptions |= RegexOptions.IgnoreCase;

                        searchRegex = new Regex(options.Query, regexOptions);
                    }
                    catch (ArgumentException ex)
                    {
                        results.ErrorMessage = $"Invalid regular expression: {ex.Message}";
                        return results;
                    }
                }

                // Always search from root directory
                var startPath = "/";

                // Perform the search
                var searchResults = new List<SearchResult>();
                var itemsProcessed = 0;

                itemsProcessed = await SearchRecursiveAsync(
                    startPath, 
                    options, 
                    searchRegex, 
                    searchResults, 
                    progress, 
                    cancellationToken,
                    itemsProcessed);

                // Sort results by relevance score
                results.Results = searchResults
                    .OrderByDescending(r => r.RelevanceScore)
                    .ThenBy(r => r.NodeInfo.Name)
                    .Take(options.MaxResults > 0 ? options.MaxResults : int.MaxValue)
                    .ToList();

                results.TotalSearched = itemsProcessed;
            }
            catch (OperationCanceledException)
            {
                results.WasCancelled = true;
            }
            catch (Exception ex)
            {
                results.ErrorMessage = ex.Message;
            }
            finally
            {
                stopwatch.Stop();
                results.SearchTime = stopwatch.Elapsed;
            }

            return results;
        }

        /// <summary>
        /// Performs a quick filter search in the current directory
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="currentPath">Current directory path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Filtered results</returns>
        public async Task<IEnumerable<TreeNodeInfo>> QuickFilterAsync(string query, string currentPath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Enumerable.Empty<TreeNodeInfo>();

            try
            {
                var children = await _ggpkService.GetChildrenAsync(currentPath, cancellationToken);
                
                return children.Where(child => 
                    child.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    child.FullPath.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(child => child.Name);
            }
            catch (Exception)
            {
                return Enumerable.Empty<TreeNodeInfo>();
            }
        }

        /// <summary>
        /// Searches for files matching the specified criteria (ISearchService implementation)
        /// </summary>
        /// <param name="searchOptions">Search criteria and options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of matching files</returns>
        public async Task<IEnumerable<TreeNodeInfo>> SearchAsync(SearchOptions searchOptions, CancellationToken cancellationToken)
        {
            var results = await SearchAsync(searchOptions, null, cancellationToken);
            return results.Results?.Select(r => r.NodeInfo) ?? Enumerable.Empty<TreeNodeInfo>();
        }

        /// <summary>
        /// Searches for files in a specific directory (ISearchService implementation)
        /// </summary>
        /// <param name="directoryPath">Directory to search in</param>
        /// <param name="searchOptions">Search criteria and options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of matching files</returns>
        public async Task<IEnumerable<TreeNodeInfo>> SearchInDirectoryAsync(string directoryPath, SearchOptions searchOptions, CancellationToken cancellationToken)
        {
            // Create a modified search options that limits scope to the specified directory
            var modifiedOptions = new SearchOptions
            {
                Query = searchOptions.Query,
                MatchCase = searchOptions.MatchCase,
                UseRegex = searchOptions.UseRegex,
                TypeFilter = searchOptions.TypeFilter,
                SearchInPaths = searchOptions.SearchInPaths,
                MaxResults = searchOptions.MaxResults
            };

            return await QuickFilterAsync(searchOptions.Query, directoryPath, cancellationToken);
        }

        /// <summary>
        /// Recursively searches through directories
        /// </summary>
        private async Task<int> SearchRecursiveAsync(
            string path, 
            SearchOptions options, 
            Regex? searchRegex, 
            List<SearchResult> results, 
            IProgress<SearchProgressInfo>? progress, 
            CancellationToken cancellationToken,
            int itemsProcessed)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var children = await _ggpkService.GetChildrenAsync(path, cancellationToken);
                
                foreach (var child in children)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    itemsProcessed++;

                    // Report progress
                    progress?.Report(new SearchProgressInfo
                    {
                        ItemsProcessed = itemsProcessed,
                        MatchesFound = results.Count,
                        CurrentPath = child.FullPath,
                        Operation = $"Searching: {child.Name}"
                    });

                    // Check if this item matches the search criteria
                    var searchResult = EvaluateMatch(child, options, searchRegex);
                    if (searchResult != null)
                    {
                        results.Add(searchResult);

                        // Stop if we've reached the maximum results
                        if (options.MaxResults > 0 && results.Count >= options.MaxResults)
                            return itemsProcessed;
                    }

                    // Recursively search directories
                    if (child.Type == NodeType.Directory && 
                        child.HasChildren)
                    {
                        itemsProcessed = await SearchRecursiveAsync(
                            child.FullPath, 
                            options, 
                            searchRegex, 
                            results, 
                            progress, 
                            cancellationToken,
                            itemsProcessed);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                // Continue searching even if one directory fails
            }

            return itemsProcessed;
        }

        /// <summary>
        /// Evaluates whether a node matches the search criteria
        /// </summary>
        private SearchResult? EvaluateMatch(TreeNodeInfo node, SearchOptions options, Regex? searchRegex)
        {
            // Apply file type filter
            if (!PassesFileTypeFilter(node, options.TypeFilter))
                return null;

            var query = options.Query;
            var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

            SearchResult? bestMatch = null;
            double bestScore = 0.0;

            // Check name match
            var nameMatch = EvaluateTextMatch(node.Name, query, searchRegex, options.MatchCase);
            if (nameMatch != null)
            {
                var score = CalculateRelevanceScore(node.Name, query, nameMatch.Value.position, nameMatch.Value.length, SearchMatchType.Name);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = CreateSearchResult(node, query, nameMatch.Value.position, nameMatch.Value.length, SearchMatchType.Name, score);
                }
            }

            // Check path match if enabled
            if (options.SearchInPaths)
            {
                var pathMatch = EvaluateTextMatch(node.FullPath, query, searchRegex, options.MatchCase);
                if (pathMatch != null)
                {
                    var score = CalculateRelevanceScore(node.FullPath, query, pathMatch.Value.position, pathMatch.Value.length, SearchMatchType.Path);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatch = CreateSearchResult(node, query, pathMatch.Value.position, pathMatch.Value.length, SearchMatchType.Path, score);
                    }
                }
            }

            // Check extension match
            var extension = Path.GetExtension(node.Name);
            if (!string.IsNullOrEmpty(extension))
            {
                var extMatch = EvaluateTextMatch(extension, query, searchRegex, options.MatchCase);
                if (extMatch != null)
                {
                    var score = CalculateRelevanceScore(extension, query, extMatch.Value.position, extMatch.Value.length, SearchMatchType.Extension);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatch = CreateSearchResult(node, query, extMatch.Value.position, extMatch.Value.length, SearchMatchType.Extension, score);
                    }
                }
            }

            return bestMatch;
        }

        /// <summary>
        /// Evaluates if text matches the search criteria
        /// </summary>
        private (int position, int length)? EvaluateTextMatch(string text, string query, Regex? searchRegex, bool matchCase)
        {
            if (string.IsNullOrEmpty(text))
                return null;

            if (searchRegex != null)
            {
                var match = searchRegex.Match(text);
                return match.Success ? (match.Index, match.Length) : null;
            }
            else
            {
                var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var index = text.IndexOf(query, comparison);
                return index >= 0 ? (index, query.Length) : null;
            }
        }

        /// <summary>
        /// Calculates relevance score for a match
        /// </summary>
        private double CalculateRelevanceScore(string text, string query, int position, int length, SearchMatchType matchType)
        {
            double score = 0.0;

            // Base score by match type
            score += matchType switch
            {
                SearchMatchType.Name => 1.0,
                SearchMatchType.Extension => 0.8,
                SearchMatchType.Path => 0.6,
                _ => 0.5
            };

            // Bonus for exact matches
            if (string.Equals(text, query, StringComparison.OrdinalIgnoreCase))
                score += 0.5;

            // Bonus for matches at the beginning
            if (position == 0)
                score += 0.3;

            // Bonus for longer matches relative to text length
            if (text.Length > 0)
                score += (double)length / text.Length * 0.2;

            // Penalty for longer text (prefer shorter, more specific matches)
            score -= Math.Min(text.Length / 100.0, 0.3);

            return Math.Max(0.0, Math.Min(1.0, score));
        }

        /// <summary>
        /// Creates a search result object
        /// </summary>
        private SearchResult CreateSearchResult(TreeNodeInfo node, string query, int position, int length, SearchMatchType matchType, double score)
        {
            var text = matchType switch
            {
                SearchMatchType.Name => node.Name,
                SearchMatchType.Path => node.FullPath,
                SearchMatchType.Extension => Path.GetExtension(node.Name),
                _ => node.Name
            };

            var highlightedText = CreateHighlightedText(text, position, length);
            var context = matchType == SearchMatchType.Path ? Path.GetDirectoryName(node.FullPath) ?? "" : node.FullPath;

            return new SearchResult
            {
                NodeInfo = node,
                RelevanceScore = score,
                HighlightedText = highlightedText,
                MatchContext = context,
                MatchType = matchType,
                MatchPosition = position,
                MatchLength = length
            };
        }

        /// <summary>
        /// Creates highlighted text showing the match
        /// </summary>
        private string CreateHighlightedText(string text, int position, int length)
        {
            if (position < 0 || position >= text.Length || length <= 0)
                return text;

            var before = text.Substring(0, position);
            var match = text.Substring(position, Math.Min(length, text.Length - position));
            var after = text.Substring(Math.Min(position + length, text.Length));

            return $"{before}**{match}**{after}";
        }

        /// <summary>
        /// Checks if a node passes the file type filter
        /// </summary>
        private bool PassesFileTypeFilter(TreeNodeInfo node, FileTypeFilter filter)
        {
            // Check basic type filters
            if (!filter.IncludeFiles && node.Type != NodeType.Directory)
                return false;
            
            if (!filter.IncludeDirectories && node.Type == NodeType.Directory)
                return false;
            
            if (!filter.IncludeBundleFiles && node.Type == NodeType.BundleFile)
                return false;
            
            if (!filter.IncludeCompressedFiles && node.Compression != null)
                return false;

            // Check extension filters
            if (filter.IncludeExtensions.Length > 0)
            {
                var extension = Path.GetExtension(node.Name).ToLowerInvariant();
                if (!filter.IncludeExtensions.Contains(extension))
                    return false;
            }

            if (filter.ExcludeExtensions.Length > 0)
            {
                var extension = Path.GetExtension(node.Name).ToLowerInvariant();
                if (filter.ExcludeExtensions.Contains(extension))
                    return false;
            }

            // Check size filters
            if (filter.MinSize > 0 && node.Size < filter.MinSize)
                return false;
            
            if (filter.MaxSize > 0 && node.Size > filter.MaxSize)
                return false;

            return true;
        }

        /// <summary>
        /// Checks if a file is an image file
        /// </summary>
        private bool IsImageFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension is ".dds" or ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tga";
        }

        /// <summary>
        /// Checks if a file is an audio file
        /// </summary>
        private bool IsAudioFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension is ".ogg" or ".wav" or ".mp3";
        }

        /// <summary>
        /// Checks if a file is a text file
        /// </summary>
        private bool IsTextFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension is ".txt" or ".xml" or ".json" or ".lua" or ".hlsl" or ".fx";
        }

        /// <summary>
        /// Checks if a file is a data file
        /// </summary>
        private bool IsDataFile(string fileName)
        {
            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return extension is ".dat" or ".bin";
        }

        /// <summary>
        /// Gets the current directory path (placeholder implementation)
        /// </summary>
        private async Task<string> GetCurrentDirectoryPath()
        {
            // This would typically come from the current navigation state
            // For now, return root
            try
            {
                var root = await _ggpkService.GetRootAsync();
                return root?.FullPath ?? "/";
            }
            catch
            {
                return "/";
            }
        }
    }
}