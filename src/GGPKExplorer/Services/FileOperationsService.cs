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
    /// Service for file operations like extraction, search, and properties
    /// Reference: Requirements 8.1, 8.2, 8.3, 8.4, 8.5 - Enhanced with comprehensive error handling
    /// </summary>
    public class FileOperationsService : IFileOperationsService
    {
        private readonly IGGPKService _ggpkService;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IJsonLoggingService _jsonLogger;
        private readonly Dictionary<string, string> _fileTypeDescriptions;
        private readonly Dictionary<NodeType, string> _nodeTypeIcons;
        private readonly Dictionary<string, string> _extensionIcons;

        public event EventHandler<Models.ErrorEventArgs>? ErrorOccurred;

        public FileOperationsService(IGGPKService ggpkService, IErrorHandlingService errorHandlingService)
        {
            _ggpkService = ggpkService ?? throw new ArgumentNullException(nameof(ggpkService));
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
            _jsonLogger = new JsonLoggingService(Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonLoggingService>.Instance);
            
            _fileTypeDescriptions = InitializeFileTypeDescriptions();
            _nodeTypeIcons = InitializeNodeTypeIcons();
            _extensionIcons = InitializeExtensionIcons();
        }

        public async Task<bool> ExtractFileAsync(string sourcePath, string destinationPath, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var correlationId = _jsonLogger.BeginOperationScope("ExtractFile", new Dictionary<string, object>
            {
                ["SourcePath"] = sourcePath,
                ["DestinationPath"] = destinationPath
            });

            try
            {
                if (!_ggpkService.IsLoaded)
                    throw new InvalidOperationException("No GGPK file is currently loaded");

                if (!ValidateDestinationPath(destinationPath))
                    throw new ArgumentException("Invalid destination path", nameof(destinationPath));

                progress?.Report(new ProgressInfo
                {
                    Percentage = 0,
                    Operation = "Extracting file",
                    CurrentFile = sourcePath,
                    Status = "Validating path..."
                });

                // Check if the source path is a directory BEFORE attempting to read
                System.Diagnostics.Debug.WriteLine($"ExtractFileAsync: Validating path '{sourcePath}'");
                
                try
                {
                    var nodeInfo = await _ggpkService.GetNodeInfoAsync(sourcePath, cancellationToken);
                    if (nodeInfo == null)
                    {
                        // Try without trailing slash if path ends with /
                        if (sourcePath.EndsWith("/") && sourcePath.Length > 1)
                        {
                            var pathWithoutSlash = sourcePath.TrimEnd('/');
                            System.Diagnostics.Debug.WriteLine($"ExtractFileAsync: Trying path without trailing slash '{pathWithoutSlash}'");
                            nodeInfo = await _ggpkService.GetNodeInfoAsync(pathWithoutSlash, cancellationToken);
                        }
                        
                        if (nodeInfo == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"ExtractFileAsync: Path not found '{sourcePath}'");
                            throw new FileNotFoundException($"Path not found: {sourcePath}");
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"ExtractFileAsync: Found node '{nodeInfo.Name}' with type '{nodeInfo.Type}'");
                    if (nodeInfo.Type == NodeType.Directory)
                    {
                        System.Diagnostics.Debug.WriteLine($"ExtractFileAsync: Rejecting directory extraction for '{sourcePath}'");
                        throw new InvalidOperationException($"Cannot extract directory '{sourcePath}' as a file. Use ExtractDirectoryAsync() method or select 'Extract Directory' option in the UI.");
                    }
                }
                catch (InvalidOperationException)
                {
                    // Re-throw our directory validation error
                    throw;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ExtractFileAsync: Error during validation: {ex.Message}");
                    throw new InvalidOperationException($"Error validating path '{sourcePath}': {ex.Message}", ex);
                }

                progress?.Report(new ProgressInfo
                {
                    Percentage = 10,
                    Operation = "Extracting file",
                    CurrentFile = sourcePath,
                    Status = "Reading file data..."
                });

                _jsonLogger.LogFileOperation("ReadFileData_Start", sourcePath, context: new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId
                });

                // Read file data from GGPK
                var readStartTime = System.Diagnostics.Stopwatch.StartNew();
                var fileData = await _ggpkService.ReadFileAsync(sourcePath, cancellationToken);
                readStartTime.Stop();

                _jsonLogger.LogFileOperation("ReadFileData_Complete", sourcePath, fileData.Length, new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId,
                    ["ReadDuration"] = readStartTime.Elapsed.TotalMilliseconds,
                    ["ReadThroughput"] = fileData.Length > 0 ? fileData.Length / readStartTime.Elapsed.TotalSeconds : 0
                });
                
                progress?.Report(new ProgressInfo
                {
                    Percentage = 50,
                    Operation = "Extracting file",
                    CurrentFile = sourcePath,
                    Status = "Writing to destination..."
                });

                // Ensure destination directory exists
                var destinationDir = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDir) && !Directory.Exists(destinationDir))
                {
                    Directory.CreateDirectory(destinationDir);
                    _jsonLogger.LogFileOperation("CreateDirectory", destinationDir, context: new Dictionary<string, object>
                    {
                        ["CorrelationId"] = correlationId
                    });
                }

                // Write file to destination
                var writeStartTime = System.Diagnostics.Stopwatch.StartNew();
                await File.WriteAllBytesAsync(destinationPath, fileData, cancellationToken);
                writeStartTime.Stop();

                progress?.Report(new ProgressInfo
                {
                    Percentage = 100,
                    Operation = "Extracting file",
                    CurrentFile = sourcePath,
                    Status = "Complete"
                });

                stopwatch.Stop();

                _jsonLogger.LogExtractionOperation("ExtractFile_Complete", sourcePath, destinationPath, fileData.Length, stopwatch.Elapsed, new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId,
                    ["WriteDuration"] = writeStartTime.Elapsed.TotalMilliseconds,
                    ["WriteThroughput"] = fileData.Length > 0 ? fileData.Length / writeStartTime.Elapsed.TotalSeconds : 0,
                    ["TotalThroughput"] = fileData.Length > 0 ? fileData.Length / stopwatch.Elapsed.TotalSeconds : 0
                });

                _jsonLogger.EndOperationScope(correlationId, true, new Dictionary<string, object>
                {
                    ["FileSize"] = fileData.Length,
                    ["Duration"] = stopwatch.Elapsed.TotalMilliseconds
                });

                return true;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                
                _jsonLogger.LogError("ExtractFile_Error", ex, new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId,
                    ["SourcePath"] = sourcePath,
                    ["DestinationPath"] = destinationPath,
                    ["Duration"] = stopwatch.Elapsed.TotalMilliseconds
                });

                _jsonLogger.EndOperationScope(correlationId, false, new Dictionary<string, object>
                {
                    ["Error"] = ex.Message,
                    ["Duration"] = stopwatch.Elapsed.TotalMilliseconds
                });

                OnErrorOccurred(ex, false, $"Failed to extract file: {sourcePath}");
                return false;
            }
        }

        public async Task<bool> ExtractDirectoryAsync(string sourcePath, string destinationPath, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var correlationId = _jsonLogger.BeginOperationScope("ExtractDirectory", new Dictionary<string, object>
            {
                ["SourcePath"] = sourcePath,
                ["DestinationPath"] = destinationPath
            });

            try
            {
                if (!_ggpkService.IsLoaded)
                    throw new InvalidOperationException("No GGPK file is currently loaded");

                if (!ValidateDestinationPath(destinationPath))
                    throw new ArgumentException("Invalid destination path", nameof(destinationPath));

                progress?.Report(new ProgressInfo
                {
                    Percentage = 0,
                    Operation = "Extracting directory",
                    CurrentFile = sourcePath,
                    Status = "Starting directory extraction..."
                });

                // Use LibGGPK3's built-in directory extraction method with progress reporting
                // This is much more efficient than extracting files one by one
                var extractedCount = await _ggpkService.ExtractDirectoryAsync(sourcePath, destinationPath, progress, cancellationToken);

                stopwatch.Stop();

                _jsonLogger.LogExtractionOperation("ExtractDirectory_Complete", sourcePath, destinationPath, 0, stopwatch.Elapsed, new Dictionary<string, object>
                {
                    ["CorrelationId"] = correlationId,
                    ["ExtractedFiles"] = extractedCount
                });

                _jsonLogger.EndOperationScope(correlationId, true, new Dictionary<string, object>
                {
                    ["ExtractedFiles"] = extractedCount,
                    ["Duration"] = stopwatch.Elapsed.TotalMilliseconds
                });

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex, false, $"Failed to extract directory: {sourcePath}");
                return false;
            }
        }

        public async Task<bool> ExtractToAsync(IEnumerable<string> sourcePaths, string destinationPath, IProgress<ProgressInfo>? progress = null)
        {
            var results = await ExtractMultipleAsync(sourcePaths, destinationPath, progress, CancellationToken.None);
            return results.IsSuccess;
        }

        public async Task<ExtractionResults> ExtractMultipleAsync(IEnumerable<string> sourcePaths, string destinationPath, IProgress<ProgressInfo>? progress = null, CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new ExtractionResults();
            var errors = new List<ExtractionError>();

            try
            {
                if (!_ggpkService.IsLoaded)
                    throw new InvalidOperationException("No GGPK file is currently loaded");

                if (!ValidateDestinationPath(destinationPath))
                    throw new ArgumentException("Invalid destination path", nameof(destinationPath));

                var pathList = sourcePaths.ToList();
                var totalItems = pathList.Count;
                var processedItems = 0;

                foreach (var sourcePath in pathList)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    try
                    {
                        progress?.Report(new ProgressInfo
                        {
                            Percentage = (double)processedItems / totalItems * 100,
                            Operation = "Extracting multiple items",
                            CurrentFile = sourcePath,
                            FilesProcessed = processedItems,
                            TotalFiles = totalItems,
                            Status = $"Processing {Path.GetFileName(sourcePath)}..."
                        });

                        System.Diagnostics.Debug.WriteLine($"Looking up node info for path: '{sourcePath}'");
                        
                        // Log the node lookup for debugging
                        _jsonLogger.LogFileOperation("NodeLookup_Start", sourcePath, context: new Dictionary<string, object>
                        {
                            ["Operation"] = "ExtractMultiple_NodeLookup"
                        });
                        
                        var nodeInfo = await _ggpkService.GetNodeInfoAsync(sourcePath, cancellationToken);
                        if (nodeInfo == null)
                        {
                            System.Diagnostics.Debug.WriteLine($"Node not found for path: '{sourcePath}'");
                            
                            _jsonLogger.LogFileOperation("NodeLookup_Failed", sourcePath, context: new Dictionary<string, object>
                            {
                                ["Error"] = "Node not found"
                            });
                            
                            errors.Add(new ExtractionError
                            {
                                FilePath = sourcePath,
                                ErrorMessage = "File or directory not found"
                            });
                            results.FailedFiles++;
                            continue;
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Found node: {nodeInfo.Name} (Type: {nodeInfo.Type})");
                        
                        _jsonLogger.LogFileOperation("NodeLookup_Success", sourcePath, context: new Dictionary<string, object>
                        {
                            ["NodeName"] = nodeInfo.Name,
                            ["NodeType"] = nodeInfo.Type.ToString(),
                            ["NodeSize"] = nodeInfo.Size
                        });

                        // Preserve directory structure by using the relative path from root
                        var relativePath = GetRelativePathFromRoot(sourcePath);
                        var itemDestPath = Path.Combine(destinationPath, relativePath);

                        bool success;
                        int extractedFileCount = 0;
                        
                        if (nodeInfo.Type == NodeType.Directory)
                        {
                            _jsonLogger.LogFileOperation("ExtractMethod_Directory", sourcePath, context: new Dictionary<string, object>
                            {
                                ["Method"] = "ExtractDirectoryAsync",
                                ["DestinationPath"] = itemDestPath
                            });
                            
                            // Call the service directly to get the actual file count
                            extractedFileCount = await _ggpkService.ExtractDirectoryAsync(sourcePath, itemDestPath, progress, cancellationToken);
                            success = extractedFileCount > 0;
                        }
                        else
                        {
                            _jsonLogger.LogFileOperation("ExtractMethod_File", sourcePath, context: new Dictionary<string, object>
                            {
                                ["Method"] = "ExtractFileAsync",
                                ["DestinationPath"] = itemDestPath,
                                ["NodeType"] = nodeInfo.Type.ToString()
                            });
                            
                            success = await ExtractFileAsync(sourcePath, itemDestPath, null, cancellationToken);
                            extractedFileCount = success ? 1 : 0;
                        }

                        if (success)
                        {
                            results.SuccessfulFiles += extractedFileCount; // Use actual file count, not just 1
                            results.TotalBytesExtracted += nodeInfo.Size;
                            
                            System.Diagnostics.Debug.WriteLine($"ExtractMultipleAsync: Extracted {extractedFileCount} files from {(nodeInfo.Type == NodeType.Directory ? "directory" : "file")} '{sourcePath}'");
                        }
                        else
                        {
                            results.FailedFiles++;
                            errors.Add(new ExtractionError
                            {
                                FilePath = sourcePath,
                                ErrorMessage = "Extraction failed"
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        results.FailedFiles++;
                        errors.Add(new ExtractionError
                        {
                            FilePath = sourcePath,
                            ErrorMessage = ex.Message,
                            Exception = ex
                        });
                    }

                    processedItems++;
                }

                results.TotalFiles = results.SuccessfulFiles + results.FailedFiles;
                results.Duration = stopwatch.Elapsed;
                results.Errors = errors.ToArray();

                progress?.Report(new ProgressInfo
                {
                    Percentage = 100,
                    Operation = "Extracting multiple items",
                    FilesProcessed = processedItems,
                    TotalFiles = totalItems,
                    Status = $"Complete - {results.SuccessfulFiles} successful, {results.FailedFiles} failed"
                });

                return results;
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex, false, "Failed to extract multiple items");
                results.Duration = stopwatch.Elapsed;
                results.Errors = errors.ToArray();
                return results;
            }
        }

        public async Task<FileProperties> GetPropertiesAsync(string path, CancellationToken cancellationToken = default)
        {
            try
            {
                if (!_ggpkService.IsLoaded)
                    throw new InvalidOperationException("No GGPK file is currently loaded");

                return await _ggpkService.GetFilePropertiesAsync(path, cancellationToken);
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex, false, $"Failed to get properties for: {path}");
                
                // Return a basic properties object with error information
                return new FileProperties
                {
                    Name = Path.GetFileName(path),
                    FullPath = path,
                    Errors = new[] { ex.Message }
                };
            }
        }

        public bool CanExtract(string path)
        {
            try
            {
                if (!_ggpkService.IsLoaded || string.IsNullOrWhiteSpace(path))
                    return false;

                // Most files can be extracted, but we might want to exclude certain system files
                // For now, allow extraction of all files
                return true;
            }
            catch
            {
                return false;
            }
        }

        public string GetFileTypeDescription(string extension)
        {
            if (string.IsNullOrEmpty(extension))
                return "File";

            var ext = extension.ToLowerInvariant();
            if (!ext.StartsWith("."))
                ext = "." + ext;

            return _fileTypeDescriptions.TryGetValue(ext, out var description) 
                ? description 
                : $"{ext.TrimStart('.').ToUpperInvariant()} File";
        }

        public string GetIconPath(NodeType nodeType, string? extension = null)
        {
            // First check for node type specific icons
            if (_nodeTypeIcons.TryGetValue(nodeType, out var nodeIcon))
            {
                return nodeIcon;
            }

            // For files, check extension-specific icons
            if (nodeType == NodeType.File && !string.IsNullOrEmpty(extension))
            {
                var ext = extension.ToLowerInvariant();
                if (!ext.StartsWith("."))
                    ext = "." + ext;

                if (_extensionIcons.TryGetValue(ext, out var extIcon))
                {
                    return extIcon;
                }
            }

            // Default icons
            return nodeType switch
            {
                NodeType.Directory => "FolderRegular",
                NodeType.BundleFile => "ArchiveRegular",
                NodeType.CompressedFile => "DocumentZipRegular",
                _ => "DocumentRegular"
            };
        }

        public string FormatFileSize(long bytes)
        {
            if (bytes == 0) return "0 bytes";
            if (bytes == 1) return "1 byte";

            string[] suffixes = { "bytes", "KB", "MB", "GB", "TB", "PB" };
            int suffixIndex = 0;
            double size = bytes;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            return suffixIndex == 0 
                ? $"{size:N0} {suffixes[suffixIndex]}" 
                : $"{size:N2} {suffixes[suffixIndex]}";
        }

        public bool ValidateDestinationPath(string destinationPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationPath))
                    return false;

                // Check for invalid characters
                var invalidChars = Path.GetInvalidPathChars();
                if (destinationPath.Any(c => invalidChars.Contains(c)))
                    return false;

                // Check if path is rooted (absolute)
                if (!Path.IsPathRooted(destinationPath))
                    return false;

                // Try to get the full path to validate format
                Path.GetFullPath(destinationPath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<(List<TreeNodeInfo> matches, int totalSearched)> SearchInDirectoryAsync(
            TreeNodeInfo directory, 
            SearchOptions options, 
            Regex? searchRegex, 
            int currentMatchCount, 
            int totalSearched, 
            IProgress<ProgressInfo>? progress, 
            CancellationToken cancellationToken)
        {
            var matches = new List<TreeNodeInfo>();

            try
            {
                var children = await _ggpkService.GetChildrenAsync(directory.FullPath, cancellationToken);

                foreach (var child in children)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (currentMatchCount + matches.Count >= options.MaxResults)
                        break;

                    totalSearched++;

                    // Check if this item matches the search criteria
                    if (MatchesSearchCriteria(child, options, searchRegex))
                    {
                        matches.Add(child);
                    }

                    // Recursively search subdirectories
                    if (child.Type == NodeType.Directory)
                    {
                        var subResult = await SearchInDirectoryAsync(
                            child, 
                            options, 
                            searchRegex, 
                            currentMatchCount + matches.Count, 
                            totalSearched, 
                            progress, 
                            cancellationToken);

                        matches.AddRange(subResult.matches);
                        totalSearched = subResult.totalSearched;
                    }

                    // Update progress periodically
                    if (totalSearched % 100 == 0)
                    {
                        progress?.Report(new ProgressInfo
                        {
                            Operation = "Searching",
                            Status = $"Searched {totalSearched} items, found {currentMatchCount + matches.Count} matches"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex, true, $"Error searching directory: {directory.FullPath}");
            }

            return (matches, totalSearched);
        }

        private bool MatchesSearchCriteria(TreeNodeInfo node, SearchOptions options, Regex? searchRegex)
        {
            // Check file type filter
            if (!PassesTypeFilter(node, options.TypeFilter))
                return false;

            // Check size filter
            if (!PassesSizeFilter(node, options.TypeFilter))
                return false;

            // Check extension filter
            if (!PassesExtensionFilter(node, options.TypeFilter))
                return false;

            // Check name match
            var name = node.Name;
            if (searchRegex != null)
            {
                return searchRegex.IsMatch(name);
            }
            else
            {
                var comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                return name.Contains(options.Query, comparison);
            }
        }

        private bool PassesTypeFilter(TreeNodeInfo node, FileTypeFilter filter)
        {
            return node.Type switch
            {
                NodeType.Directory => filter.IncludeDirectories,
                NodeType.File => filter.IncludeFiles,
                NodeType.BundleFile => filter.IncludeBundleFiles,
                NodeType.CompressedFile => filter.IncludeCompressedFiles,
                _ => true
            };
        }

        private bool PassesSizeFilter(TreeNodeInfo node, FileTypeFilter filter)
        {
            if (filter.MinSize > 0 && node.Size < filter.MinSize)
                return false;

            if (filter.MaxSize > 0 && node.Size > filter.MaxSize)
                return false;

            return true;
        }

        private bool PassesExtensionFilter(TreeNodeInfo node, FileTypeFilter filter)
        {
            if (node.Type == NodeType.Directory)
                return true;

            var extension = Path.GetExtension(node.Name).ToLowerInvariant();

            // Check exclude list first
            if (filter.ExcludeExtensions.Length > 0)
            {
                if (filter.ExcludeExtensions.Any(ext => 
                    string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase)))
                {
                    return false;
                }
            }

            // Check include list
            if (filter.IncludeExtensions.Length > 0)
            {
                return filter.IncludeExtensions.Any(ext => 
                    string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase));
            }

            return true;
        }



        private async Task<List<TreeNodeInfo>> GetAllFilesRecursivelyAsync(string directoryPath, CancellationToken cancellationToken)
        {
            var allFiles = new List<TreeNodeInfo>();

            try
            {
                var children = await _ggpkService.GetChildrenAsync(directoryPath, cancellationToken);

                foreach (var child in children)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (child.Type == NodeType.Directory)
                    {
                        var subFiles = await GetAllFilesRecursivelyAsync(child.FullPath, cancellationToken);
                        allFiles.AddRange(subFiles);
                    }
                    else
                    {
                        allFiles.Add(child);
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred(ex, true, $"Error getting files from directory: {directoryPath}");
            }

            return allFiles;
        }

        private string GetRelativePath(string basePath, string fullPath)
        {
            if (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = fullPath.Substring(basePath.Length);
                return relativePath.TrimStart('/', '\\');
            }

            return Path.GetFileName(fullPath);
        }

        /// <summary>
        /// Gets the relative path from the GGPK root, preserving directory structure
        /// </summary>
        /// <param name="fullPath">Full path within the GGPK</param>
        /// <returns>Relative path suitable for extraction</returns>
        private string GetRelativePathFromRoot(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
                return "unknown_file";

            // Remove leading slash and normalize path separators
            var relativePath = fullPath.TrimStart('/', '\\');
            
            // Replace forward slashes with backslashes for Windows paths
            relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
            
            // If the path is empty after trimming, use just the filename
            if (string.IsNullOrEmpty(relativePath))
            {
                return "root_file";
            }

            return relativePath;
        }

        private void OnErrorOccurred(Exception exception, bool isRecoverable, string context)
        {
            // Use comprehensive error handling service
            var fileOpException = ConvertToFileOperationException(exception, context);
            
            // Log the error and handle it appropriately
            _ = Task.Run(async () => 
            {
                await _errorHandlingService.HandleExceptionAsync(fileOpException, context, showDialog: !isRecoverable);
            });
            
            // Still raise the event for any listeners
            ErrorOccurred?.Invoke(this, new Models.ErrorEventArgs(fileOpException, isRecoverable, context));
        }

        /// <summary>
        /// Converts a generic exception to a FileOperationException with appropriate operation type
        /// Reference: Requirements 8.2 - Specific error handling for file operations
        /// </summary>
        /// <param name="exception">The original exception</param>
        /// <param name="context">Context information about the operation</param>
        /// <returns>A FileOperationException with appropriate operation type</returns>
        private FileOperationException ConvertToFileOperationException(Exception exception, string context)
        {
            // Determine operation type from context
            var operationType = context.ToLowerInvariant() switch
            {
                var ctx when ctx.Contains("extract") => FileOperationType.Extract,
                var ctx when ctx.Contains("properties") => FileOperationType.GetProperties,
                var ctx when ctx.Contains("search") => FileOperationType.Search,
                _ => FileOperationType.Read
            };

            // Extract file path from context if possible
            var filePath = ExtractFilePathFromContext(context);

            // Return existing FileOperationException or create new one
            return exception as FileOperationException ?? 
                   new FileOperationException(filePath, operationType, exception.Message, exception);
        }

        /// <summary>
        /// Extracts file path from context string
        /// </summary>
        /// <param name="context">Context string that may contain a file path</param>
        /// <returns>Extracted file path or empty string</returns>
        private string ExtractFilePathFromContext(string context)
        {
            if (string.IsNullOrEmpty(context))
                return "";

            // Look for common patterns that indicate file paths
            var patterns = new[]
            {
                @"(?:file|path|directory):\s*(.+?)(?:\s|$)",
                @"Failed to .+?: (.+?)(?:\s|$)",
                @"Error .+?: (.+?)(?:\s|$)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(context, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value.Trim();
                }
            }

            return "";
        } 
       private Dictionary<string, string> InitializeFileTypeDescriptions()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Image formats
                { ".png", "PNG Image" },
                { ".jpg", "JPEG Image" },
                { ".jpeg", "JPEG Image" },
                { ".bmp", "Bitmap Image" },
                { ".gif", "GIF Image" },
                { ".tga", "Targa Image" },
                { ".dds", "DirectDraw Surface" },
                { ".hdr", "High Dynamic Range Image" },

                // Audio formats
                { ".wav", "Wave Audio" },
                { ".mp3", "MP3 Audio" },
                { ".ogg", "Ogg Vorbis Audio" },
                { ".flac", "FLAC Audio" },
                { ".wem", "Wwise Audio" },
                { ".bnk", "Wwise Sound Bank" },

                // Video formats
                { ".mp4", "MP4 Video" },
                { ".avi", "AVI Video" },
                { ".mov", "QuickTime Video" },
                { ".wmv", "Windows Media Video" },
                { ".bik", "Bink Video" },

                // 3D/Model formats
                { ".fbx", "FBX 3D Model" },
                { ".obj", "Wavefront OBJ Model" },
                { ".3ds", "3D Studio Model" },
                { ".dae", "COLLADA Model" },
                { ".blend", "Blender Model" },
                { ".max", "3ds Max Model" },
                { ".maya", "Maya Model" },

                // Texture/Material formats
                { ".mat", "Material File" },
                { ".mtl", "Material Template Library" },
                { ".shader", "Shader File" },
                { ".hlsl", "HLSL Shader" },
                { ".glsl", "GLSL Shader" },
                { ".cg", "Cg Shader" },

                // Data formats
                { ".xml", "XML Document" },
                { ".json", "JSON Data" },
                { ".csv", "Comma Separated Values" },
                { ".txt", "Text Document" },
                { ".ini", "Configuration File" },
                { ".cfg", "Configuration File" },
                { ".dat", "Data File" },
                { ".bin", "Binary Data" },

                // Archive formats
                { ".zip", "ZIP Archive" },
                { ".rar", "RAR Archive" },
                { ".7z", "7-Zip Archive" },
                { ".tar", "TAR Archive" },
                { ".gz", "GZip Archive" },

                // Script/Code formats
                { ".lua", "Lua Script" },
                { ".py", "Python Script" },
                { ".js", "JavaScript" },
                { ".cs", "C# Source Code" },
                { ".cpp", "C++ Source Code" },
                { ".h", "C/C++ Header" },
                { ".hpp", "C++ Header" },

                // Game-specific formats
                { ".ggpk", "GGPK Archive" },
                { ".bundle", "Bundle File" },
                { ".index", "Index File" },
                { ".cache", "Cache File" },
                { ".pak", "Package File" },
                { ".asset", "Asset File" },
                { ".prefab", "Prefab File" },
                { ".scene", "Scene File" },

                // Font formats
                { ".ttf", "TrueType Font" },
                { ".otf", "OpenType Font" },
                { ".woff", "Web Open Font Format" },
                { ".eot", "Embedded OpenType Font" },

                // Document formats
                { ".pdf", "PDF Document" },
                { ".doc", "Word Document" },
                { ".docx", "Word Document" },
                { ".rtf", "Rich Text Format" },
                { ".md", "Markdown Document" },

                // Executable formats
                { ".exe", "Executable File" },
                { ".dll", "Dynamic Link Library" },
                { ".so", "Shared Object Library" },
                { ".dylib", "Dynamic Library" }
            };
        }

        private Dictionary<NodeType, string> InitializeNodeTypeIcons()
        {
            return new Dictionary<NodeType, string>
            {
                { NodeType.Directory, "FolderRegular" },
                { NodeType.File, "DocumentRegular" },
                { NodeType.BundleFile, "ArchiveRegular" },
                { NodeType.CompressedFile, "DocumentZipRegular" }
            };
        }

        private Dictionary<string, string> InitializeExtensionIcons()
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Image files
                { ".png", "ImageRegular" },
                { ".jpg", "ImageRegular" },
                { ".jpeg", "ImageRegular" },
                { ".bmp", "ImageRegular" },
                { ".gif", "ImageRegular" },
                { ".tga", "ImageRegular" },
                { ".dds", "ImageRegular" },
                { ".hdr", "ImageRegular" },

                // Audio files
                { ".wav", "MusicNote2Regular" },
                { ".mp3", "MusicNote2Regular" },
                { ".ogg", "MusicNote2Regular" },
                { ".flac", "MusicNote2Regular" },
                { ".wem", "MusicNote2Regular" },
                { ".bnk", "MusicNote2Regular" },

                // Video files
                { ".mp4", "VideoRegular" },
                { ".avi", "VideoRegular" },
                { ".mov", "VideoRegular" },
                { ".wmv", "VideoRegular" },
                { ".bik", "VideoRegular" },

                // 3D/Model files
                { ".fbx", "Cube3dRegular" },
                { ".obj", "Cube3dRegular" },
                { ".3ds", "Cube3dRegular" },
                { ".dae", "Cube3dRegular" },
                { ".blend", "Cube3dRegular" },

                // Code/Script files
                { ".lua", "CodeRegular" },
                { ".py", "CodeRegular" },
                { ".js", "CodeRegular" },
                { ".cs", "CodeRegular" },
                { ".cpp", "CodeRegular" },
                { ".h", "CodeRegular" },
                { ".hpp", "CodeRegular" },

                // Data files
                { ".xml", "DocumentDataRegular" },
                { ".json", "DocumentDataRegular" },
                { ".csv", "DocumentTableRegular" },
                { ".txt", "DocumentTextRegular" },
                { ".ini", "SettingsRegular" },
                { ".cfg", "SettingsRegular" },

                // Archive files
                { ".zip", "FolderZipRegular" },
                { ".rar", "FolderZipRegular" },
                { ".7z", "FolderZipRegular" },
                { ".tar", "FolderZipRegular" },
                { ".gz", "FolderZipRegular" },

                // Game-specific files
                { ".ggpk", "ArchiveRegular" },
                { ".bundle", "PackageRegular" },
                { ".index", "DocumentBulletListRegular" },
                { ".cache", "DatabaseRegular" },
                { ".pak", "PackageRegular" },

                // Font files
                { ".ttf", "TextFontRegular" },
                { ".otf", "TextFontRegular" },
                { ".woff", "TextFontRegular" },

                // Executable files
                { ".exe", "AppGenericRegular" },
                { ".dll", "DocumentRegular" }
            };
        }
    }
}