using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for GGPK file operations
    /// </summary>
    public interface IGGPKService : IDisposable
    {
        /// <summary>
        /// Event raised when a GGPK file is successfully loaded
        /// </summary>
        event EventHandler<GGPKLoadedEventArgs>? GGPKLoaded;

        /// <summary>
        /// Event raised when progress is made on long-running operations
        /// </summary>
        event EventHandler<ProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        event EventHandler<ErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Gets whether a GGPK file is currently loaded
        /// </summary>
        bool IsLoaded { get; }

        /// <summary>
        /// Gets the path of the currently loaded GGPK file
        /// </summary>
        string? CurrentFilePath { get; }

        /// <summary>
        /// Gets the version of the currently loaded GGPK file
        /// </summary>
        uint? Version { get; }

        /// <summary>
        /// Gets whether the current GGPK file contains bundles
        /// </summary>
        bool IsBundled { get; }

        /// <summary>
        /// Opens a GGPK file for reading
        /// </summary>
        /// <param name="filePath">Path to the GGPK file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the file was opened successfully</returns>
        Task<bool> OpenGGPKAsync(string filePath, CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes the currently opened GGPK file
        /// </summary>
        void CloseGGPK();

        /// <summary>
        /// Gets the children of a directory node
        /// </summary>
        /// <param name="path">Path to the directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of child nodes</returns>
        Task<IEnumerable<TreeNodeInfo>> GetChildrenAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets information about a specific file or directory
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File information</returns>
        Task<TreeNodeInfo?> GetNodeInfoAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Reads the content of a file
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File content as byte array</returns>
        Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed properties of a file or directory
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed file properties</returns>
        Task<FileProperties> GetFilePropertiesAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a path exists in the GGPK file
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the path exists</returns>
        Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the root directory information
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Root directory node</returns>
        Task<TreeNodeInfo> GetRootAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels the current operation if one is running
        /// </summary>
        void CancelCurrentOperation();
    }


}