using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GGPKExplorer.Models;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service interface for file operations like extraction and search
    /// </summary>
    public interface IFileOperationsService
    {
        /// <summary>
        /// Event raised when an error occurs during file operations
        /// </summary>
        event EventHandler<ErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Extracts a single file to the specified destination
        /// </summary>
        /// <param name="sourcePath">Path to the file within the GGPK</param>
        /// <param name="destinationPath">Destination path on the file system</param>
        /// <param name="progress">Progress reporting interface</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if extraction was successful</returns>
        Task<bool> ExtractFileAsync(string sourcePath, string destinationPath, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts a directory and all its contents to the specified destination
        /// </summary>
        /// <param name="sourcePath">Path to the directory within the GGPK</param>
        /// <param name="destinationPath">Destination directory on the file system</param>
        /// <param name="progress">Progress reporting interface</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if extraction was successful</returns>
        Task<bool> ExtractDirectoryAsync(string sourcePath, string destinationPath, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts multiple files or directories to the specified destination
        /// </summary>
        /// <param name="sourcePaths">Paths to the files/directories within the GGPK</param>
        /// <param name="destinationPath">Destination directory on the file system</param>
        /// <param name="progress">Progress reporting interface</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Extraction results with success/failure information</returns>
        Task<ExtractionResults> ExtractMultipleAsync(IEnumerable<string> sourcePaths, string destinationPath, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts files to the specified destination (alias for ExtractMultipleAsync)
        /// </summary>
        /// <param name="sourcePaths">Paths to the files/directories within the GGPK</param>
        /// <param name="destinationPath">Destination directory on the file system</param>
        /// <param name="progress">Progress reporting interface</param>
        /// <returns>True if extraction was successful</returns>
        Task<bool> ExtractToAsync(IEnumerable<string> sourcePaths, string destinationPath, IProgress<ProgressInfo>? progress = null);



        /// <summary>
        /// Gets detailed properties of a file or directory
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed file properties</returns>
        Task<FileProperties> GetPropertiesAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a file or directory can be extracted
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>True if the path can be extracted</returns>
        bool CanExtract(string path);

        /// <summary>
        /// Gets a human-readable description of a file type based on extension
        /// </summary>
        /// <param name="extension">File extension (including the dot)</param>
        /// <returns>File type description</returns>
        string GetFileTypeDescription(string extension);

        /// <summary>
        /// Gets the appropriate icon for a file type
        /// </summary>
        /// <param name="nodeType">Type of the node</param>
        /// <param name="extension">File extension (for files)</param>
        /// <returns>Path to the icon resource</returns>
        string GetIconPath(NodeType nodeType, string? extension = null);

        /// <summary>
        /// Formats a file size in bytes to a human-readable string
        /// </summary>
        /// <param name="bytes">Size in bytes</param>
        /// <returns>Formatted size string</returns>
        string FormatFileSize(long bytes);

        /// <summary>
        /// Validates a destination path for extraction
        /// </summary>
        /// <param name="destinationPath">Path to validate</param>
        /// <returns>True if the path is valid for extraction</returns>
        bool ValidateDestinationPath(string destinationPath);
    }

    /// <summary>
    /// Progress information for file operations
    /// </summary>
    public class ProgressInfo
    {
        /// <summary>
        /// Progress percentage (0-100)
        /// </summary>
        public double Percentage { get; set; }

        /// <summary>
        /// Current operation description
        /// </summary>
        public string Operation { get; set; } = string.Empty;

        /// <summary>
        /// Current file being processed
        /// </summary>
        public string? CurrentFile { get; set; }

        /// <summary>
        /// Number of files processed
        /// </summary>
        public int FilesProcessed { get; set; }

        /// <summary>
        /// Total number of files to process
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Bytes processed
        /// </summary>
        public long BytesProcessed { get; set; }

        /// <summary>
        /// Total bytes to process
        /// </summary>
        public long TotalBytes { get; set; }

        /// <summary>
        /// Additional status information
        /// </summary>
        public string? Status { get; set; }
    }

    /// <summary>
    /// Results of an extraction operation
    /// </summary>
    public class ExtractionResults
    {
        /// <summary>
        /// Number of files successfully extracted
        /// </summary>
        public int SuccessfulFiles { get; set; }

        /// <summary>
        /// Number of files that failed to extract
        /// </summary>
        public int FailedFiles { get; set; }

        /// <summary>
        /// Total number of files processed
        /// </summary>
        public int TotalFiles { get; set; }

        /// <summary>
        /// Total bytes extracted
        /// </summary>
        public long TotalBytesExtracted { get; set; }

        /// <summary>
        /// Time taken for the extraction
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// List of files that failed to extract with error information
        /// </summary>
        public ExtractionError[] Errors { get; set; } = [];

        /// <summary>
        /// Whether the extraction was completed successfully
        /// </summary>
        public bool IsSuccess => FailedFiles == 0;

        /// <summary>
        /// Whether the extraction was partially successful
        /// </summary>
        public bool IsPartialSuccess => SuccessfulFiles > 0 && FailedFiles > 0;
    }

    /// <summary>
    /// Information about an extraction error
    /// </summary>
    public class ExtractionError
    {
        /// <summary>
        /// Path of the file that failed to extract
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Error message
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// The exception that caused the error
        /// </summary>
        public Exception? Exception { get; set; }
    }
}