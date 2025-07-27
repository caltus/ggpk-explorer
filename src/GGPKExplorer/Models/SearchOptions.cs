using System;
using System.Collections.Generic;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Options for configuring search operations
    /// </summary>
    public class SearchOptions
    {
        /// <summary>
        /// The search query string
        /// </summary>
        public string Query { get; set; } = string.Empty;

        /// <summary>
        /// Whether the search should be case-sensitive
        /// </summary>
        public bool MatchCase { get; set; } = false;

        /// <summary>
        /// Whether to use regular expressions in the search
        /// </summary>
        public bool UseRegex { get; set; } = false;

        /// <summary>
        /// Filter for file types to include in search
        /// </summary>
        public FileTypeFilter TypeFilter { get; set; } = new();

        /// <summary>
        /// Whether to search in file contents (if supported)
        /// </summary>
        public bool SearchInContent { get; set; } = false;

        /// <summary>
        /// Maximum number of search results to return
        /// </summary>
        public int MaxResults { get; set; } = 1000;

        /// <summary>
        /// Whether to include hidden files in search results
        /// </summary>
        public bool IncludeHiddenFiles { get; set; } = false;

        /// <summary>
        /// Whether to search in file paths in addition to names
        /// </summary>
        public bool SearchInPaths { get; set; } = true;
    }



    /// <summary>
    /// Filter for file types in search operations
    /// </summary>
    public class FileTypeFilter
    {
        /// <summary>
        /// Whether to include regular files
        /// </summary>
        public bool IncludeFiles { get; set; } = true;

        /// <summary>
        /// Whether to include directories
        /// </summary>
        public bool IncludeDirectories { get; set; } = true;

        /// <summary>
        /// Whether to include bundle files
        /// </summary>
        public bool IncludeBundleFiles { get; set; } = true;

        /// <summary>
        /// Whether to include compressed files
        /// </summary>
        public bool IncludeCompressedFiles { get; set; } = true;

        /// <summary>
        /// Specific file extensions to include (empty means all)
        /// </summary>
        public string[] IncludeExtensions { get; set; } = [];

        /// <summary>
        /// Specific file extensions to exclude
        /// </summary>
        public string[] ExcludeExtensions { get; set; } = [];

        /// <summary>
        /// Minimum file size in bytes (0 means no minimum)
        /// </summary>
        public long MinSize { get; set; } = 0;

        /// <summary>
        /// Maximum file size in bytes (0 means no maximum)
        /// </summary>
        public long MaxSize { get; set; } = 0;
    }

    /// <summary>
    /// Represents a single search result
    /// </summary>
    public class SearchResult
    {
        /// <summary>
        /// Node information for the found item
        /// </summary>
        public TreeNodeInfo NodeInfo { get; set; } = new();

        /// <summary>
        /// Relevance score for ranking results (0.0 to 1.0)
        /// </summary>
        public double RelevanceScore { get; set; }

        /// <summary>
        /// Highlighted text showing the match
        /// </summary>
        public string HighlightedText { get; set; } = string.Empty;

        /// <summary>
        /// Context information about where the match was found
        /// </summary>
        public string MatchContext { get; set; } = string.Empty;

        /// <summary>
        /// Type of match (name, path, etc.)
        /// </summary>
        public SearchMatchType MatchType { get; set; }

        /// <summary>
        /// Position of the match within the text
        /// </summary>
        public int MatchPosition { get; set; }

        /// <summary>
        /// Length of the matched text
        /// </summary>
        public int MatchLength { get; set; }
    }

    /// <summary>
    /// Type of search match
    /// </summary>
    public enum SearchMatchType
    {
        /// <summary>
        /// Match found in file/directory name
        /// </summary>
        Name,

        /// <summary>
        /// Match found in full path
        /// </summary>
        Path,

        /// <summary>
        /// Match found in file extension
        /// </summary>
        Extension
    }

    /// <summary>
    /// Results of a search operation
    /// </summary>
    public class SearchResults
    {
        /// <summary>
        /// List of search results
        /// </summary>
        public List<SearchResult> Results { get; set; } = new();

        /// <summary>
        /// Total number of items searched
        /// </summary>
        public int TotalSearched { get; set; }

        /// <summary>
        /// Time taken for the search operation
        /// </summary>
        public TimeSpan SearchTime { get; set; }

        /// <summary>
        /// Whether the search was cancelled
        /// </summary>
        public bool WasCancelled { get; set; }

        /// <summary>
        /// Error message if the search failed
        /// </summary>
        public string? ErrorMessage { get; set; }

        /// <summary>
        /// Search options used for this search
        /// </summary>
        public SearchOptions Options { get; set; } = new();
    }

    /// <summary>
    /// Progress information for search operations
    /// </summary>
    public class SearchProgressInfo
    {
        /// <summary>
        /// Current progress percentage (0-100)
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Current operation description
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// Number of items processed so far
        /// </summary>
        public int ItemsProcessed { get; set; }

        /// <summary>
        /// Total number of items to process
        /// </summary>
        public int TotalItems { get; set; }

        /// <summary>
        /// Number of matches found so far
        /// </summary>
        public int MatchesFound { get; set; }

        /// <summary>
        /// Current path being searched
        /// </summary>
        public string CurrentPath { get; set; } = string.Empty;
    }

    /// <summary>
    /// Simple file type filter for UI
    /// </summary>
    public enum SimpleFileTypeFilter
    {
        /// <summary>
        /// Include all file types
        /// </summary>
        All,

        /// <summary>
        /// Include only files (no directories)
        /// </summary>
        FilesOnly,

        /// <summary>
        /// Include only directories
        /// </summary>
        DirectoriesOnly,

        /// <summary>
        /// Include only image files
        /// </summary>
        Images,

        /// <summary>
        /// Include only audio files
        /// </summary>
        Audio,

        /// <summary>
        /// Include only text/code files
        /// </summary>
        Text,

        /// <summary>
        /// Include only data files
        /// </summary>
        Data
    }
}