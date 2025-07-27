using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using LibGGPK3;
using LibGGPK3.Records;
using LibBundledGGPK3;
using GGPKExplorer.Models;
using GGPKExplorer.Services;

namespace GGPKExplorer.Wrappers
{
    /// <summary>
    /// Wrapper class to encapsulate LibGGPK3 operations and provide a unified interface
    /// 
    /// CRITICAL THREAD SAFETY: This wrapper uses lock-based synchronization to ensure thread-safe access.
    /// LibGGPK3 is NOT thread-safe and requires external synchronization to prevent corruption.
    /// All operations are protected by _lockObject to ensure sequential access.
    /// </summary>
    public sealed class GGPKWrapper : IDisposable
    {
        private BundledGGPK? _bundledGGPK;
        private GGPK? _ggpk;
        private readonly object _lockObject = new();
        private readonly ILogger<GGPKWrapper>? _logger;
        private readonly IEnhancedLoggingService? _enhancedLogging;
        private bool _disposed;

        /// <summary>
        /// Gets whether the GGPK file contains bundles
        /// </summary>
        public bool IsBundled { get; private set; }

        /// <summary>
        /// Gets the version of the GGPK file
        /// </summary>
        public uint Version { get; private set; }

        /// <summary>
        /// Gets the root directory record
        /// </summary>
        public DirectoryRecord? Root { get; private set; }

        /// <summary>
        /// Gets the path to the currently opened GGPK file
        /// </summary>
        public string? FilePath { get; private set; }

        /// <summary>
        /// Gets whether a GGPK file is currently open
        /// </summary>
        public bool IsOpen => Root != null && !_disposed;

        /// <summary>
        /// Initializes a new instance of the GGPKWrapper class
        /// </summary>
        /// <param name="logger">Optional logger for diagnostic information</param>
        /// <param name="enhancedLogging">Optional enhanced logging service for comprehensive operation logging</param>
        public GGPKWrapper(ILogger<GGPKWrapper>? logger = null, IEnhancedLoggingService? enhancedLogging = null)
        {
            _logger = logger;
            _enhancedLogging = enhancedLogging;
            _logger?.LogDebug("GGPKWrapper initialized");
            _enhancedLogging?.LogGGPKOperation("Initialize", null, new Dictionary<string, object>
            {
                ["HasLogger"] = _logger != null,
                ["HasEnhancedLogging"] = _enhancedLogging != null
            });
        }

        /// <summary>
        /// Opens a GGPK file for reading
        /// </summary>
        /// <param name="filePath">Path to the GGPK file</param>
        /// <returns>True if the file was opened successfully</returns>
        public bool Open(string filePath)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKWrapper));

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            _enhancedLogging?.LogGGPKOperation("Open", filePath, new Dictionary<string, object>
            {
                ["MemoryBefore"] = memoryBefore
            });

            _logger?.LogInformation("Opening GGPK file: {FilePath}", filePath);

            lock (_lockObject)
            {
                try
                {
                    Close();

                    var fileInfo = new FileInfo(filePath);
                    _logger?.LogDebug("File size: {FileSize:N0} bytes", fileInfo.Length);

                    _enhancedLogging?.LogFileOperation("AnalyzeFile", filePath, fileInfo.Length, 
                        $"LastModified: {fileInfo.LastWriteTime}");

                    // Try to open as bundled GGPK first
                    var bundledAttemptTime = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        _logger?.LogDebug("Attempting to open as bundled GGPK");
                        _bundledGGPK = new BundledGGPK(filePath, parsePathsInIndex: true);
                        _ggpk = _bundledGGPK;
                        IsBundled = true;
                        bundledAttemptTime.Stop();
                        
                        _enhancedLogging?.LogPerformanceMetric("BundledGGPKOpenTime", bundledAttemptTime.Elapsed.TotalMilliseconds, "ms", 
                            new Dictionary<string, object>
                            {
                                ["Success"] = true,
                                ["FilePath"] = filePath
                            });
                        
                        _logger?.LogInformation("Successfully opened as bundled GGPK");
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        bundledAttemptTime.Stop();
                        _enhancedLogging?.LogPerformanceMetric("BundledGGPKOpenTime", bundledAttemptTime.Elapsed.TotalMilliseconds, "ms", 
                            new Dictionary<string, object>
                            {
                                ["Success"] = false,
                                ["Error"] = "No Bundles2 directory",
                                ["FilePath"] = filePath
                            });

                        // No Bundles2 directory, try regular GGPK
                        _logger?.LogDebug("No Bundles2 directory found, trying regular GGPK: {Message}", ex.Message);
                        _bundledGGPK?.Dispose();
                        _bundledGGPK = null;
                        
                        var regularAttemptTime = System.Diagnostics.Stopwatch.StartNew();
                        _ggpk = new GGPK(filePath);
                        IsBundled = false;
                        regularAttemptTime.Stop();
                        
                        _enhancedLogging?.LogPerformanceMetric("RegularGGPKOpenTime", regularAttemptTime.Elapsed.TotalMilliseconds, "ms", 
                            new Dictionary<string, object>
                            {
                                ["Success"] = true,
                                ["FilePath"] = filePath
                            });
                        
                        _logger?.LogInformation("Successfully opened as regular GGPK");
                    }
                    catch (FileNotFoundException ex)
                    {
                        bundledAttemptTime.Stop();
                        _enhancedLogging?.LogPerformanceMetric("BundledGGPKOpenTime", bundledAttemptTime.Elapsed.TotalMilliseconds, "ms", 
                            new Dictionary<string, object>
                            {
                                ["Success"] = false,
                                ["Error"] = "No _.index.bin file",
                                ["FilePath"] = filePath
                            });

                        // No _.index.bin file, try regular GGPK
                        _logger?.LogDebug("No _.index.bin file found, trying regular GGPK: {Message}", ex.Message);
                        _bundledGGPK?.Dispose();
                        _bundledGGPK = null;
                        
                        var regularAttemptTime = System.Diagnostics.Stopwatch.StartNew();
                        _ggpk = new GGPK(filePath);
                        IsBundled = false;
                        regularAttemptTime.Stop();
                        
                        _enhancedLogging?.LogPerformanceMetric("RegularGGPKOpenTime", regularAttemptTime.Elapsed.TotalMilliseconds, "ms", 
                            new Dictionary<string, object>
                            {
                                ["Success"] = true,
                                ["FilePath"] = filePath
                            });
                        
                        _logger?.LogInformation("Successfully opened as regular GGPK");
                    }

                    if (_ggpk != null)
                    {
                        Version = 0; // Version property not available in current LibGGPK3
                        Root = _ggpk.Root;
                        FilePath = filePath;

                        var memoryAfter = GC.GetTotalMemory(false);
                        _enhancedLogging?.LogMemoryOperation("GGPKOpened", memoryAfter - memoryBefore, Path.GetFileName(filePath));
                        
                        var rootChildCount = Root?.Count() ?? 0;
                        _logger?.LogInformation("GGPK file opened successfully - Version: {Version}, IsBundled: {IsBundled}, Root children: {ChildCount}", 
                            Version, IsBundled, rootChildCount);

                        stopwatch.Stop();
                        _enhancedLogging?.LogGGPKOperationComplete("Open", stopwatch.Elapsed, true, 
                            new Dictionary<string, object>
                            {
                                ["FilePath"] = filePath,
                                ["FileSize"] = fileInfo.Length,
                                ["Version"] = Version,
                                ["IsBundled"] = IsBundled,
                                ["RootChildren"] = rootChildCount,
                                ["MemoryDelta"] = memoryAfter - memoryBefore
                            });
                        
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    
                    // Gather comprehensive error context for GGPK file opening failure
                    var errorContext = new Dictionary<string, object>
                    {
                        ["FilePath"] = filePath,
                        ["Error"] = ex.Message,
                        ["ExceptionType"] = ex.GetType().Name,
                        ["StackTrace"] = ex.StackTrace ?? "No stack trace available",
                        ["ThreadId"] = Environment.CurrentManagedThreadId,
                        ["Timestamp"] = DateTime.UtcNow,
                        ["MemoryUsage"] = GC.GetTotalMemory(false),
                        ["AttemptedBundled"] = true,
                        ["FileExists"] = File.Exists(filePath)
                    };

                    // Add file system context
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            var fileInfo = new FileInfo(filePath);
                            errorContext["FileSize"] = fileInfo.Length;
                            errorContext["FileLastModified"] = fileInfo.LastWriteTime;
                            errorContext["FileAttributes"] = fileInfo.Attributes.ToString();
                            errorContext["DirectoryExists"] = fileInfo.Directory?.Exists ?? false;
                        }
                        catch (Exception fileEx)
                        {
                            errorContext["FileInfoError"] = fileEx.Message;
                        }
                    }

                    // Check for specific error patterns
                    if (ex.Message.Contains("oo2core") || ex.Message.Contains("decompression"))
                    {
                        errorContext["ErrorCategory"] = "BundleDecompression";
                        errorContext["SuggestedAction"] = "Check oo2core.dll availability and bundle file integrity";
                    }
                    else if (ex.Message.Contains("access") || ex.Message.Contains("permission"))
                    {
                        errorContext["ErrorCategory"] = "FileAccess";
                        errorContext["SuggestedAction"] = "Check file permissions and ensure file is not locked";
                    }
                    else if (ex.Message.Contains("corrupt") || ex.Message.Contains("invalid"))
                    {
                        errorContext["ErrorCategory"] = "FileCorruption";
                        errorContext["SuggestedAction"] = "Verify GGPK file integrity and consider re-downloading";
                    }
                    else
                    {
                        errorContext["ErrorCategory"] = "Unknown";
                        errorContext["SuggestedAction"] = "Check system resources and file accessibility";
                    }

                    _enhancedLogging?.LogGGPKOperationComplete("Open", stopwatch.Elapsed, false, errorContext);

                    // Log comprehensive error context
                    _enhancedLogging?.LogEntry(new GGPKLogEntry
                    {
                        Level = Microsoft.Extensions.Logging.LogLevel.Error,
                        Category = "GGPKWrapper",
                        Operation = "Open",
                        FilePath = filePath,
                        ErrorMessage = ex.Message,
                        Context = errorContext
                    });

                    _logger?.LogError(ex, "Failed to open GGPK file: {FilePath}, Context: {@ErrorContext}", filePath, errorContext);
                    Close();
                    throw new GGPKException($"Failed to open GGPK file: {filePath}", ex);
                }

                stopwatch.Stop();
                _enhancedLogging?.LogGGPKOperationComplete("Open", stopwatch.Elapsed, false, 
                    new Dictionary<string, object>
                    {
                        ["FilePath"] = filePath,
                        ["Error"] = "Unknown reason"
                    });

                _logger?.LogWarning("Failed to open GGPK file: {FilePath} - Unknown reason", filePath);
                return false;
            }
        }

        /// <summary>
        /// Closes the currently opened GGPK file
        /// </summary>
        public void Close()
        {
            lock (_lockObject)
            {
                if (FilePath != null)
                {
                    _logger?.LogInformation("Closing GGPK file: {FilePath}", FilePath);
                }

                try
                {
                    _bundledGGPK?.Dispose();
                    _bundledGGPK = null;

                    if (_ggpk != null && _ggpk != _bundledGGPK)
                    {
                        _ggpk.Dispose();
                    }
                    _ggpk = null;

                    Root = null;
                    FilePath = null;
                    IsBundled = false;
                    Version = 0;

                    _logger?.LogDebug("GGPK file closed successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error occurred while closing GGPK file");
                }
            }
        }

        /// <summary>
        /// Gets the children of a directory record
        /// </summary>
        /// <param name="directory">Directory record to get children from</param>
        /// <returns>Enumerable of child tree nodes</returns>
        public IEnumerable<TreeNode> GetChildren(DirectoryRecord directory)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKWrapper));

            if (directory == null)
                throw new ArgumentNullException(nameof(directory));

            lock (_lockObject)
            {
                if (_ggpk == null)
                    throw new InvalidOperationException("No GGPK file is currently open");

                try
                {
                    var children = directory.ToList(); // Create a copy to avoid enumeration issues
                    _logger?.LogDebug("Retrieved {ChildCount} children from directory: {DirectoryName}", 
                        children.Count, directory.Name);
                    return children;
                }
                catch (Exception ex)
                {
                    // Gather comprehensive error context for directory traversal failure
                    var errorContext = new Dictionary<string, object>
                    {
                        ["DirectoryName"] = directory.Name,
                        ["DirectoryPath"] = directory.GetPath(),
                        ["Error"] = ex.Message,
                        ["ExceptionType"] = ex.GetType().Name,
                        ["StackTrace"] = ex.StackTrace ?? "No stack trace available",
                        ["ThreadId"] = Environment.CurrentManagedThreadId,
                        ["Timestamp"] = DateTime.UtcNow,
                        ["MemoryUsage"] = GC.GetTotalMemory(false),
                        ["IsBundled"] = IsBundled,
                        ["GGPKVersion"] = Version,
                        ["ErrorCategory"] = "DirectoryTraversal",
                        ["SuggestedAction"] = "Directory structure may be corrupted or inaccessible"
                    };

                    // Log comprehensive error context
                    _enhancedLogging?.LogEntry(new GGPKLogEntry
                    {
                        Level = Microsoft.Extensions.Logging.LogLevel.Error,
                        Category = "GGPKWrapper",
                        Operation = "GetChildren",
                        FilePath = directory.GetPath(),
                        ErrorMessage = ex.Message,
                        Context = errorContext
                    });

                    _logger?.LogError(ex, "Error getting children from directory: {DirectoryName}, Context: {@ErrorContext}", 
                        directory.Name, errorContext);
                    throw;
                }
            }
        }

        /// <summary>
        /// Reads the content of a file record
        /// </summary>
        /// <param name="file">File record to read</param>
        /// <returns>File content as byte array</returns>
        public byte[] ReadFile(FileRecord file)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKWrapper));

            if (file == null)
                throw new ArgumentNullException(nameof(file));

            lock (_lockObject)
            {
                if (_ggpk == null)
                    throw new InvalidOperationException("No GGPK file is currently open");

                try
                {
                    _logger?.LogDebug("Reading file: {FileName}, Size: {FileSize:N0} bytes", 
                        file.Name, file.DataLength);
                    
                    var data = file.Read();
                    
                    _logger?.LogDebug("Successfully read file: {FileName}, Actual size: {ActualSize:N0} bytes", 
                        file.Name, data.Length);
                    
                    return data;
                }
                catch (Exception ex)
                {
                    // Gather comprehensive error context for file reading failure
                    var errorContext = new Dictionary<string, object>
                    {
                        ["FileName"] = file.Name,
                        ["FilePath"] = file.GetPath(),
                        ["FileSize"] = file.DataLength,
                        ["FileOffset"] = file.Offset,
                        ["FileHash"] = file.Hash,
                        ["Error"] = ex.Message,
                        ["ExceptionType"] = ex.GetType().Name,
                        ["StackTrace"] = ex.StackTrace ?? "No stack trace available",
                        ["ThreadId"] = Environment.CurrentManagedThreadId,
                        ["Timestamp"] = DateTime.UtcNow,
                        ["MemoryUsage"] = GC.GetTotalMemory(false),
                        ["IsBundled"] = IsBundled,
                        ["GGPKVersion"] = Version
                    };

                    // Determine error category and recovery suggestions
                    if (ex.Message.Contains("memory") || ex is OutOfMemoryException)
                    {
                        errorContext["ErrorCategory"] = "Memory";
                        errorContext["SuggestedAction"] = "File too large for available memory, try closing other applications";
                    }
                    else if (ex.Message.Contains("corrupt") || ex.Message.Contains("invalid"))
                    {
                        errorContext["ErrorCategory"] = "Corruption";
                        errorContext["SuggestedAction"] = "File data may be corrupted, verify GGPK integrity";
                    }
                    else if (ex.Message.Contains("bundle") || ex.Message.Contains("decompression"))
                    {
                        errorContext["ErrorCategory"] = "BundleDecompression";
                        errorContext["SuggestedAction"] = "Bundle decompression failed, check oo2core.dll availability";
                    }
                    else
                    {
                        errorContext["ErrorCategory"] = "Unknown";
                        errorContext["SuggestedAction"] = "Unexpected file reading error";
                    }

                    // Log comprehensive error context
                    _enhancedLogging?.LogEntry(new GGPKLogEntry
                    {
                        Level = Microsoft.Extensions.Logging.LogLevel.Error,
                        Category = "GGPKWrapper",
                        Operation = "ReadFile",
                        FilePath = file.GetPath(),
                        FileSize = file.DataLength,
                        ErrorMessage = ex.Message,
                        Context = errorContext
                    });

                    _logger?.LogError(ex, "Failed to read file: {FileName}, Context: {@ErrorContext}", file.Name, errorContext);
                    throw new FileOperationException(file.GetPath(), FileOperationType.Read, 
                        $"Failed to read file: {file.Name}", ex);
                }
            }
        }

        /// <summary>
        /// Finds a file by its path
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <returns>File record if found, null otherwise</returns>
        public FileRecord? FindFile(string path)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKWrapper));

            if (string.IsNullOrEmpty(path))
                return null;

            lock (_lockObject)
            {
                if (Root == null)
                    return null;

                try
                {
                    _logger?.LogTrace("Searching for file: {Path}", path);
                    
                    var node = FindNodeByPath(Root, path);
                    if (node is FileRecord fileRecord)
                    {
                        _logger?.LogTrace("Found file: {Path}, Size: {Size:N0} bytes", path, fileRecord.DataLength);
                        return fileRecord;
                    }
                    
                    _logger?.LogTrace("File not found: {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Exception during file search: {Path}", path);
                }

                return null;
            }
        }

        /// <summary>
        /// Finds a directory by its path
        /// </summary>
        /// <param name="path">Path to the directory</param>
        /// <returns>Directory record if found, null otherwise</returns>
        public DirectoryRecord? FindDirectory(string path)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKWrapper));

            if (string.IsNullOrEmpty(path))
                return Root;

            lock (_lockObject)
            {
                if (Root == null)
                    return null;

                try
                {
                    _logger?.LogTrace("Searching for directory: {Path}", path);
                    
                    var node = FindNodeByPath(Root, path);
                    if (node is DirectoryRecord directoryRecord)
                    {
                        _logger?.LogTrace("Found directory: {Path}, Children: {ChildCount}", path, directoryRecord.Count());
                        return directoryRecord;
                    }
                    
                    _logger?.LogTrace("Directory not found: {Path}", path);
                }
                catch (Exception ex)
                {
                    _logger?.LogDebug(ex, "Exception during directory search: {Path}", path);
                }

                return null;
            }
        }

        /// <summary>
        /// Gets the bundled GGPK instance if available
        /// </summary>
        /// <returns>BundledGGPK instance or null if not bundled</returns>
        public BundledGGPK? GetBundledGGPK()
        {
            lock (_lockObject)
            {
                return _bundledGGPK;
            }
        }

        /// <summary>
        /// Helper method to find a node by path in the directory tree
        /// </summary>
        private TreeNode? FindNodeByPath(DirectoryRecord root, string path)
        {
            if (string.IsNullOrEmpty(path) || path == "/")
                return root;

            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            TreeNode current = root;

            foreach (var part in parts)
            {
                if (current is DirectoryRecord directory)
                {
                    TreeNode? found = null;
                    foreach (var child in directory)
                    {
                        if (string.Equals(child.Name, part, StringComparison.OrdinalIgnoreCase))
                        {
                            found = child;
                            break;
                        }
                    }
                    
                    if (found == null)
                        return null;
                        
                    current = found;
                }
                else
                {
                    return null;
                }
            }

            return current;
        }

        /// <summary>
        /// Disposes the wrapper and releases all resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _logger?.LogDebug("Disposing GGPKWrapper");
            Close();
            _disposed = true;
            _logger?.LogDebug("GGPKWrapper disposed");
        }
    }
}