using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for searching GGPK files
    /// </summary>
    public interface ISearchService
    {
        /// <summary>
        /// Performs a search operation with the specified options
        /// </summary>
        /// <param name="options">Search options</param>
        /// <param name="progress">Progress reporting callback</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Search results</returns>
        Task<SearchResults> SearchAsync(SearchOptions options, IProgress<SearchProgressInfo>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Performs a quick filter search in the current directory
        /// </summary>
        /// <param name="query">Search query</param>
        /// <param name="currentPath">Current directory path</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Filtered results</returns>
        Task<IEnumerable<TreeNodeInfo>> QuickFilterAsync(string query, string currentPath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for files matching the specified criteria
        /// </summary>
        /// <param name="searchOptions">Search criteria and options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of matching files</returns>
        Task<IEnumerable<TreeNodeInfo>> SearchAsync(SearchOptions searchOptions, CancellationToken cancellationToken = default);

        /// <summary>
        /// Searches for files in a specific directory
        /// </summary>
        /// <param name="directoryPath">Directory to search in</param>
        /// <param name="searchOptions">Search criteria and options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of matching files</returns>
        Task<IEnumerable<TreeNodeInfo>> SearchInDirectoryAsync(string directoryPath, SearchOptions searchOptions, CancellationToken cancellationToken = default);
    }
}