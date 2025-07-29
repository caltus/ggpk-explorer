using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using LibGGPK3.Records;
using GGPKExplorer.Models;
using GGPKExplorer.Wrappers;

namespace GGPKExplorer.Services
{
    /// <summary>
    /// Service for GGPK file operations with single-threaded access and enhanced performance optimizations
    /// 
    /// CRITICAL THREAD SAFETY: This service enforces single-threaded access to prevent GGPK corruption.
    /// LibGGPK3 is NOT thread-safe and concurrent access WILL corrupt the GGPK file.
    /// All operations are queued and executed sequentially on a dedicated thread.
    /// 
    /// Reference: Requirements 5.1, 5.2, 5.4, 5.5, 8.1, 8.2, 8.3, 8.4, 8.5, 12.1, 12.2, 12.3, 12.4, 12.5
    /// </summary>
    public sealed class GGPKService : IGGPKService
    {
        private readonly GGPKWrapper _ggpkWrapper;
        private readonly IndexDecompressor _indexDecompressor;
        private readonly IErrorHandlingService _errorHandlingService;
        private readonly IOperationQueueService _operationQueue;
        private readonly IPerformanceMonitorService _performanceMonitor;
        private readonly IUIThreadMarshalingService _uiThreadMarshaling;
        private readonly IMemoryManagementService _memoryManagement;
        private readonly ILogger<GGPKService> _logger;
        private readonly IEnhancedLoggingService _enhancedLogging;
        private readonly IJsonLoggingService _jsonLogger;
        
        private volatile bool _disposed;

        /// <summary>
        /// Event raised when a GGPK file is successfully loaded
        /// </summary>
        public event EventHandler<GGPKLoadedEventArgs>? GGPKLoaded;

        /// <summary>
        /// Event raised when progress is made on long-running operations
        /// </summary>
        public event EventHandler<ProgressEventArgs>? ProgressChanged;

        /// <summary>
        /// Event raised when an error occurs
        /// </summary>
        public event EventHandler<Models.ErrorEventArgs>? ErrorOccurred;

        /// <summary>
        /// Gets whether a GGPK file is currently loaded
        /// </summary>
        public bool IsLoaded => _ggpkWrapper.IsOpen;

        /// <summary>
        /// Gets the path of the currently loaded GGPK file
        /// </summary>
        public string? CurrentFilePath => _ggpkWrapper.FilePath;

        /// <summary>
        /// Gets the version of the currently loaded GGPK file
        /// </summary>
        public uint? Version => _ggpkWrapper.IsOpen ? _ggpkWrapper.Version : null;

        /// <summary>
        /// Gets whether the current GGPK file contains bundles
        /// </summary>
        public bool IsBundled => _ggpkWrapper.IsBundled;

        /// <summary>
        /// Initializes a new instance of the GGPKService class
        /// </summary>
        /// <param name="errorHandlingService">Error handling service for comprehensive error management</param>
        /// <param name="operationQueue">Operation queue service for enhanced threading</param>
        /// <param name="performanceMonitor">Performance monitoring service</param>
        /// <param name="uiThreadMarshaling">UI thread marshaling service</param>
        /// <param name="memoryManagement">Memory management service</param>
        /// <param name="logger">Logger for diagnostic information</param>
        /// <param name="enhancedLogging">Enhanced logging service for comprehensive GGPK operation logging</param>
        public GGPKService(
            IErrorHandlingService errorHandlingService,
            IOperationQueueService operationQueue,
            IPerformanceMonitorService performanceMonitor,
            IUIThreadMarshalingService uiThreadMarshaling,
            IMemoryManagementService memoryManagement,
            ILogger<GGPKService> logger,
            IEnhancedLoggingService enhancedLogging)
        {
            _errorHandlingService = errorHandlingService ?? throw new ArgumentNullException(nameof(errorHandlingService));
            _operationQueue = operationQueue ?? throw new ArgumentNullException(nameof(operationQueue));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            _uiThreadMarshaling = uiThreadMarshaling ?? throw new ArgumentNullException(nameof(uiThreadMarshaling));
            _memoryManagement = memoryManagement ?? throw new ArgumentNullException(nameof(memoryManagement));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _enhancedLogging = enhancedLogging ?? throw new ArgumentNullException(nameof(enhancedLogging));
            _jsonLogger = new JsonLoggingService(Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonLoggingService>.Instance);
            
            // Create GGPKWrapper with logger for comprehensive logging
            var wrapperLoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => 
            {
                var loggingConfig = new GGPKExplorer.Models.EnhancedLoggingConfiguration
                {
                    LogDirectory = "logs"
                };
                builder.AddProvider(new FileLoggerProvider(loggingConfig));
            });
            var wrapperLogger = wrapperLoggerFactory.CreateLogger<GGPKWrapper>();
            _ggpkWrapper = new GGPKWrapper(wrapperLogger, _enhancedLogging);
            
            _indexDecompressor = new IndexDecompressor(_ggpkWrapper, _enhancedLogging);
            
            // Start performance monitoring
            _performanceMonitor.StartMonitoring();
            
            // Start operation queue
            _operationQueue.Start();
            
            // Subscribe to memory pressure events
            _performanceMonitor.MemoryPressureDetected += OnMemoryPressureDetected;
            
            _logger.LogInformation("GGPK service initialized with enhanced performance optimizations");
        }

        /// <summary>
        /// Opens a GGPK file for reading
        /// </summary>
        /// <param name="filePath">Path to the GGPK file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the file was opened successfully</returns>
        public async Task<bool> OpenGGPKAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKService));

            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be null or empty", nameof(filePath));

            if (!File.Exists(filePath))
                throw new FileNotFoundException($"GGPK file not found: {filePath}");

            using var scope = _enhancedLogging.BeginScope("OpenGGPK", new Dictionary<string, object>
            {
                ["FilePath"] = filePath,
                ["FileSize"] = new FileInfo(filePath).Length
            });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            var fileInfo = new FileInfo(filePath);

            var correlationId = _jsonLogger.BeginOperationScope("OpenGGPK", new Dictionary<string, object>
            {
                ["FilePath"] = filePath,
                ["FileSize"] = fileInfo.Length,
                ["LastModified"] = fileInfo.LastWriteTime
            });

            _enhancedLogging.LogGGPKOperation("OpenGGPK", filePath, new Dictionary<string, object>
            {
                ["FileSize"] = fileInfo.Length,
                ["MemoryBefore"] = memoryBefore,
                ["CorrelationId"] = correlationId
            });

            _logger.LogInformation("Starting GGPK file open operation: {FilePath}", filePath);

            return await _operationQueue.EnqueueOperationAsync(
                (ct) =>
                {
                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        _logger.LogDebug("File info - Size: {FileSize:N0} bytes, LastWrite: {LastWrite}", 
                            fileInfo.Length, fileInfo.LastWriteTime);

                        _enhancedLogging.LogFileOperation("AnalyzeFile", filePath, fileInfo.Length, 
                            $"LastModified: {fileInfo.LastWriteTime}");

                        // Register GGPK wrapper for memory management
                        _memoryManagement.RegisterResource(_ggpkWrapper, $"GGPK-{Path.GetFileName(filePath)}", fileInfo.Length);
                        
                        // Marshal progress updates to UI thread
                        _uiThreadMarshaling.PostToUIThread(() => OnProgressChanged(0, "Opening GGPK file...", $"Loading: {Path.GetFileName(filePath)}"));
                        
                        _logger.LogDebug("Calling GGPKWrapper.Open()");
                        var openStartTime = System.Diagnostics.Stopwatch.StartNew();
                        var success = _ggpkWrapper.Open(filePath);
                        openStartTime.Stop();

                        _enhancedLogging.LogPerformanceMetric("GGPKOpenTime", openStartTime.Elapsed.TotalMilliseconds, "ms", 
                            new Dictionary<string, object>
                            {
                                ["Success"] = success,
                                ["FilePath"] = filePath
                            });
                        
                        if (success)
                        {
                            var memoryAfter = GC.GetTotalMemory(false);
                            _enhancedLogging.LogMemoryOperation("GGPKLoaded", memoryAfter - memoryBefore, Path.GetFileName(filePath));

                            _logger.LogInformation("GGPK file opened successfully - Version: {Version}, IsBundled: {IsBundled}", 
                                _ggpkWrapper.Version, _ggpkWrapper.IsBundled);

                            _uiThreadMarshaling.PostToUIThread(() => OnProgressChanged(50, "Processing file structure..."));
                            
                            var rootInfo = ConvertTreeNodeToNodeInfo(_ggpkWrapper.Root!, "/");
                            _logger.LogDebug("Root directory processed - Children: {ChildCount}", rootInfo.HasChildren);
                            
                            _enhancedLogging.LogGGPKOperation("ProcessRootStructure", filePath, new Dictionary<string, object>
                            {
                                ["Version"] = _ggpkWrapper.Version,
                                ["IsBundled"] = _ggpkWrapper.IsBundled,
                                ["HasChildren"] = rootInfo.HasChildren
                            });
                            
                            _uiThreadMarshaling.PostToUIThread(() => 
                            {
                                OnProgressChanged(100, "GGPK file loaded successfully");
                                OnGGPKLoaded(new Models.GGPKLoadedEventArgs(filePath, _ggpkWrapper.IsBundled, _ggpkWrapper.Version));
                            });
                        }
                        else
                        {
                            _logger.LogWarning("GGPK file failed to open: {FilePath}", filePath);
                            _memoryManagement.UnregisterResource(_ggpkWrapper);
                        }

                        stopwatch.Stop();
                        _enhancedLogging.LogGGPKOperationComplete("OpenGGPK", stopwatch.Elapsed, success, 
                            new Dictionary<string, object>
                            {
                                ["FilePath"] = filePath,
                                ["FileSize"] = fileInfo.Length,
                                ["Version"] = success ? _ggpkWrapper.Version : 0,
                                ["IsBundled"] = success && _ggpkWrapper.IsBundled,
                                ["MemoryDelta"] = GC.GetTotalMemory(false) - memoryBefore
                            });
                        
                        return success;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Exception occurred while opening GGPK file: {FilePath}", filePath);
                        _memoryManagement.UnregisterResource(_ggpkWrapper);
                        
                        // Use comprehensive error handling
                        var ggpkException = ConvertToGGPKException(ex, filePath, "Opening GGPK file");
                        _ = Task.Run(async () => await _errorHandlingService.HandleGGPKExceptionAsync(ggpkException, $"Opening GGPK file: {filePath}"));
                        
                        _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ggpkException, false, "Opening GGPK file")));
                        return false;
                    }
                },
                "OpenGGPK",
                OperationPriority.High,
                cancellationToken
            );
        }

        /// <summary>
        /// Closes the currently opened GGPK file
        /// </summary>
        public void CloseGGPK()
        {
            if (_disposed)
                return;

            _ = _operationQueue.EnqueueOperationAsync(
                (ct) =>
                {
                    try
                    {
                        _ggpkWrapper.Close();
                        _memoryManagement.UnregisterResource(_ggpkWrapper);
                        
                        // Force memory cleanup after closing
                        _ = Task.Run(async () => await _memoryManagement.PerformMemoryCleanupAsync(false));
                        
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ex, true, "Closing GGPK file")));
                        return false;
                    }
                },
                "CloseGGPK",
                OperationPriority.High,
                CancellationToken.None
            );
        }

        /// <summary>
        /// Gets the children of a directory node
        /// </summary>
        /// <param name="path">Path to the directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of child nodes</returns>
        public async Task<IEnumerable<TreeNodeInfo>> GetChildrenAsync(string path, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKService));

            if (!IsLoaded)
                throw new InvalidOperationException("No GGPK file is currently loaded");

            using var scope = _enhancedLogging.BeginScope("GetChildren", new Dictionary<string, object>
            {
                ["Path"] = path ?? "/",
                ["IsBundled"] = _ggpkWrapper.IsBundled
            });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            var lockAcquisitionStart = System.Diagnostics.Stopwatch.StartNew();

            _enhancedLogging.LogGGPKOperation("GetChildren", path, new Dictionary<string, object>
            {
                ["IsBundled"] = _ggpkWrapper.IsBundled,
                ["MemoryBefore"] = memoryBefore
            });

            _logger.LogDebug("Getting children for path: {Path}", path ?? "/");

            return await _operationQueue.EnqueueOperationAsync(
                (ct) =>
                {
                    lockAcquisitionStart.Stop();
                    _enhancedLogging.LogPerformanceMetric("LockAcquisitionTime", lockAcquisitionStart.Elapsed.TotalMilliseconds, "ms", 
                        new Dictionary<string, object>
                        {
                            ["Operation"] = "GetChildren",
                            ["Path"] = path ?? "/",
                            ["ThreadId"] = Environment.CurrentManagedThreadId
                        });

                    try
                    {
                        var children = new List<TreeNodeInfo>();
                        var ggpkChildrenCount = 0;
                        var indexChildrenCount = 0;
                        
                        // For bundled GGPK files, prioritize bundle index structure to avoid duplication
                        if (_ggpkWrapper.IsBundled && _indexDecompressor.HasIndexFile())
                        {
                            try
                            {
                                _logger.LogTrace("Getting children from bundle index: {Path}", path ?? "/");
                                var indexChildren = _indexDecompressor.GetIndexNodesForPath(path ?? "/");
                                indexChildrenCount = indexChildren.Count();
                                children.AddRange(indexChildren);
                                _logger.LogTrace("Added {Count} children from bundle index", indexChildrenCount);
                                
                                _enhancedLogging.LogPerformanceMetric("IndexChildrenFound", indexChildrenCount, "count", 
                                    new Dictionary<string, object> { ["Path"] = path ?? "/" });
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(ex, "Error getting children from bundle index for path: {Path}, falling back to GGPK structure", path ?? "/");
                                _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ex, true, "Getting index children")));
                                
                                // Fall back to GGPK structure if bundle index fails
                                var directory = string.IsNullOrEmpty(path) || path == "/" 
                                    ? _ggpkWrapper.Root 
                                    : _ggpkWrapper.FindDirectory(path);
                                    
                                if (directory != null)
                                {
                                    _logger.LogTrace("Fallback: Found directory in GGPK structure: {Path}", path ?? "/");
                                    var ggpkChildren = _ggpkWrapper.GetChildren(directory);
                                    foreach (var child in ggpkChildren)
                                    {
                                        var childPath = string.IsNullOrEmpty(path) || path == "/" 
                                            ? $"/{child.Name}" 
                                            : $"{path.TrimEnd('/')}/{child.Name}";
                                        children.Add(ConvertTreeNodeToNodeInfo(child, childPath));
                                        ggpkChildrenCount++;
                                    }
                                    _logger.LogTrace("Fallback: Added {Count} children from GGPK structure", ggpkChildrenCount);
                                }
                            }
                        }
                        else
                        {
                            // For non-bundled GGPK files, use regular GGPK structure
                            var directory = string.IsNullOrEmpty(path) || path == "/" 
                                ? _ggpkWrapper.Root 
                                : _ggpkWrapper.FindDirectory(path);
                                
                            if (directory != null)
                            {
                                _logger.LogTrace("Found directory in GGPK structure: {Path}", path ?? "/");
                                var ggpkChildren = _ggpkWrapper.GetChildren(directory);
                                foreach (var child in ggpkChildren)
                                {
                                    var childPath = string.IsNullOrEmpty(path) || path == "/" 
                                        ? $"/{child.Name}" 
                                        : $"{path.TrimEnd('/')}/{child.Name}";
                                    children.Add(ConvertTreeNodeToNodeInfo(child, childPath));
                                    ggpkChildrenCount++;
                                }
                                _logger.LogTrace("Added {Count} children from GGPK structure", ggpkChildrenCount);
                                
                                _enhancedLogging.LogPerformanceMetric("GGPKChildrenFound", ggpkChildrenCount, "count", 
                                    new Dictionary<string, object> { ["Path"] = path ?? "/" });
                            }
                            else
                            {
                                _logger.LogTrace("Directory not found in GGPK structure: {Path}", path ?? "/");
                            }
                        }

                        var memoryAfter = GC.GetTotalMemory(false);
                        _enhancedLogging.LogMemoryOperation("GetChildren", memoryAfter - memoryBefore, path ?? "/");
                        
                        stopwatch.Stop();
                        _enhancedLogging.LogGGPKOperationComplete("GetChildren", stopwatch.Elapsed, true, 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path ?? "/",
                                ["TotalChildren"] = children.Count,
                                ["GGPKChildren"] = ggpkChildrenCount,
                                ["IndexChildren"] = indexChildrenCount,
                                ["MemoryDelta"] = memoryAfter - memoryBefore
                            });
                        
                        _logger.LogDebug("Total children found for path {Path}: {Count}", path ?? "/", children.Count);
                        return children.AsEnumerable();
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        _enhancedLogging.LogGGPKOperationComplete("GetChildren", stopwatch.Elapsed, false, 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path ?? "/",
                                ["Error"] = ex.Message
                            });

                        _logger.LogError(ex, "Error getting children for path: {Path}", path ?? "/");
                        _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ex, false, $"Getting children for path: {path}")));
                        return Enumerable.Empty<TreeNodeInfo>();
                    }
                },
                "GetChildren",
                OperationPriority.Normal,
                cancellationToken
            );
        }

        /// <summary>
        /// Gets information about a specific file or directory
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Node information</returns>
        public async Task<TreeNodeInfo?> GetNodeInfoAsync(string path, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKService));

            if (!IsLoaded)
                throw new InvalidOperationException("No GGPK file is currently loaded");

            return await _operationQueue.EnqueueOperationAsync(
                (ct) =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"GGPKService.GetNodeInfoAsync: Looking for path '{path}'");
                        
                        // For bundled GGPK files, handle both files and directories through bundle index
                        if (_ggpkWrapper.IsBundled && _indexDecompressor.HasIndexFile())
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"Searching in bundle index for path: {path}");
                                
                                // For directory paths, we need to check if the path represents a directory
                                // by looking at the parent directory and seeing if it contains a subdirectory with this name
                                if (path.EndsWith("/"))
                                {
                                    var pathWithoutSlash = path.TrimEnd('/');
                                    System.Diagnostics.Debug.WriteLine($"Looking for directory: {pathWithoutSlash}");
                                    
                                    // Get the parent path
                                    var parentPath = Path.GetDirectoryName(pathWithoutSlash)?.Replace('\\', '/') ?? "";
                                    var dirName = Path.GetFileName(pathWithoutSlash);
                                    
                                    System.Diagnostics.Debug.WriteLine($"Parent path: '{parentPath}', Directory name: '{dirName}'");
                                    
                                    // Get nodes from parent directory
                                    var parentNodes = _indexDecompressor.GetIndexNodesForPath(parentPath);
                                    var directoryNode = parentNodes.FirstOrDefault(n => 
                                        n.Type == NodeType.Directory && 
                                        string.Equals(n.Name, dirName, StringComparison.OrdinalIgnoreCase));
                                    
                                    if (directoryNode != null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Found directory in bundle index: {directoryNode.Name}");
                                        return directoryNode;
                                    }
                                }
                                else
                                {
                                    // For file paths, use the existing logic
                                    var indexNodes = _indexDecompressor.GetIndexNodesForPath(path);
                                    var result = indexNodes.FirstOrDefault();
                                    if (result != null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"Found in bundle index: {result.Name}");
                                        return result;
                                    }
                                }
                                
                                System.Diagnostics.Debug.WriteLine($"Not found in bundle index, trying GGPK structure");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error searching bundle index: {ex.Message}");
                                _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ex, true, "Getting node from index")));
                            }
                        }
                        
                        // Try to find in regular GGPK structure (fallback or non-bundled)
                        // Always try both file and directory lookups with different path formats
                        
                        System.Diagnostics.Debug.WriteLine($"Looking for path in GGPK structure: {path}");
                        
                        // Try as file first (for non-directory paths)
                        if (!path.EndsWith("/"))
                        {
                            var file = _ggpkWrapper.FindFile(path);
                            if (file != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found file in GGPK structure: {file.Name}");
                                return ConvertTreeNodeToNodeInfo(file, path);
                            }
                        }
                        
                        // Try as directory with multiple path formats
                        var directoryPaths = new List<string>();
                        
                        if (path.EndsWith("/"))
                        {
                            // For paths ending with /, try both with and without slash
                            directoryPaths.Add(path);
                            directoryPaths.Add(path.TrimEnd('/'));
                        }
                        else
                        {
                            // For paths not ending with /, try both without and with slash
                            directoryPaths.Add(path);
                            directoryPaths.Add(path + "/");
                        }
                        
                        foreach (var dirPath in directoryPaths)
                        {
                            System.Diagnostics.Debug.WriteLine($"Trying directory path: '{dirPath}'");
                            var directory = _ggpkWrapper.FindDirectory(dirPath);
                            if (directory != null)
                            {
                                System.Diagnostics.Debug.WriteLine($"Found directory in GGPK structure: {directory.Name} (using path: '{dirPath}')");
                                return ConvertTreeNodeToNodeInfo(directory, path);
                            }
                        }
                        
                        System.Diagnostics.Debug.WriteLine($"Path not found anywhere: {path}");
                        
                        // Debug: List root directory contents to see what's available
                        try
                        {
                            System.Diagnostics.Debug.WriteLine("Debug: Listing root directory contents...");
                            var rootChildren = _ggpkWrapper.GetChildren(_ggpkWrapper.Root!);
                            foreach (var child in rootChildren.Take(10)) // Limit to first 10 for debugging
                            {
                                var childType = child is LibGGPK3.Records.DirectoryRecord ? "DIR" : "FILE";
                                System.Diagnostics.Debug.WriteLine($"  {childType}: {child.Name}");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error listing root contents: {ex.Message}");
                        }
                        
                        return null;
                    }
                    catch (Exception ex)
                    {
                        _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ex, false, $"Getting node info for path: {path}")));
                        return null;
                    }
                },
                "GetNodeInfo",
                OperationPriority.Normal,
                cancellationToken
            );
        }

        /// <summary>
        /// Reads the content of a file
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>File content as byte array</returns>
        public async Task<byte[]> ReadFileAsync(string path, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKService));

            if (!IsLoaded)
                throw new InvalidOperationException("No GGPK file is currently loaded");

            using var scope = _enhancedLogging.BeginScope("ReadFile", new Dictionary<string, object>
            {
                ["FilePath"] = path
            });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            _enhancedLogging.LogGGPKOperation("ReadFile", path, new Dictionary<string, object>
            {
                ["MemoryBefore"] = memoryBefore
            });

            return await _operationQueue.EnqueueOperationAsync(
                (ct) =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"ReadFileAsync: Looking for file '{path}'");
                        
                        byte[] data;
                        TreeNodeInfo? nodeInfo = null;
                        
                        // For bundled GGPK files, try bundle system first
                        if (_ggpkWrapper.IsBundled && _indexDecompressor.HasIndexFile())
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine($"Trying to read as bundle file: {path}");
                                data = ReadBundleFile(path);
                                System.Diagnostics.Debug.WriteLine($"Successfully read bundle file: {data.Length} bytes");
                                
                                // Create a basic node info for logging
                                nodeInfo = new TreeNodeInfo
                                {
                                    Name = System.IO.Path.GetFileName(path),
                                    FullPath = path,
                                    Type = NodeType.BundleFile,
                                    Size = data.Length
                                };
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Failed to read as bundle file: {ex.Message}");
                                // Fall back to GGPK structure
                                var ggpkFile = _ggpkWrapper.FindFile(path);
                                if (ggpkFile == null)
                                {
                                    // Check if this might be a directory instead of a file
                                    System.Diagnostics.Debug.WriteLine($"ReadFileAsync: Checking if '{path}' is a directory");
                                    var directory = _ggpkWrapper.FindDirectory(path);
                                    System.Diagnostics.Debug.WriteLine($"ReadFileAsync: FindDirectory('{path}') returned: {(directory != null ? directory.Name : "null")}");
                                    
                                    if (directory == null && path.EndsWith("/"))
                                    {
                                        var pathWithoutSlash = path.TrimEnd('/');
                                        System.Diagnostics.Debug.WriteLine($"ReadFileAsync: Trying without slash: '{pathWithoutSlash}'");
                                        directory = _ggpkWrapper.FindDirectory(pathWithoutSlash);
                                        System.Diagnostics.Debug.WriteLine($"ReadFileAsync: FindDirectory('{pathWithoutSlash}') returned: {(directory != null ? directory.Name : "null")}");
                                    }
                                    
                                    if (directory != null)
                                    {
                                        System.Diagnostics.Debug.WriteLine($"ReadFileAsync: Found directory '{directory.Name}', throwing InvalidOperationException");
                                        stopwatch.Stop();
                                        _enhancedLogging.LogGGPKOperationComplete("ReadFile", stopwatch.Elapsed, false, 
                                            new Dictionary<string, object>
                                            {
                                                ["FilePath"] = path,
                                                ["Error"] = "Attempted to read directory as file"
                                            });
                                        throw new InvalidOperationException($"Cannot read directory '{path}' as a file. Use directory extraction methods instead.");
                                    }
                                    
                                    stopwatch.Stop();
                                    _enhancedLogging.LogGGPKOperationComplete("ReadFile", stopwatch.Elapsed, false, 
                                        new Dictionary<string, object>
                                        {
                                            ["FilePath"] = path,
                                            ["Error"] = "File not found in bundle or GGPK structure"
                                        });
                                    throw new FileNotFoundException($"File not found: {path}");
                                }
                                
                                System.Diagnostics.Debug.WriteLine($"Reading from GGPK structure: {ggpkFile.Name}");
                                data = _ggpkWrapper.ReadFile(ggpkFile);
                                nodeInfo = ConvertTreeNodeToNodeInfo(ggpkFile, path);
                            }
                        }
                        else
                        {
                            // For regular GGPK files
                            System.Diagnostics.Debug.WriteLine($"Reading GGPK file: {path}");
                            var ggpkFile = _ggpkWrapper.FindFile(path);
                            if (ggpkFile == null)
                            {
                                // Check if this might be a directory instead of a file
                                System.Diagnostics.Debug.WriteLine($"ReadFileAsync: Checking if '{path}' is a directory (regular GGPK)");
                                var directory = _ggpkWrapper.FindDirectory(path);
                                System.Diagnostics.Debug.WriteLine($"ReadFileAsync: FindDirectory('{path}') returned: {(directory != null ? directory.Name : "null")}");
                                
                                if (directory == null && path.EndsWith("/"))
                                {
                                    var pathWithoutSlash = path.TrimEnd('/');
                                    System.Diagnostics.Debug.WriteLine($"ReadFileAsync: Trying without slash: '{pathWithoutSlash}'");
                                    directory = _ggpkWrapper.FindDirectory(pathWithoutSlash);
                                    System.Diagnostics.Debug.WriteLine($"ReadFileAsync: FindDirectory('{pathWithoutSlash}') returned: {(directory != null ? directory.Name : "null")}");
                                }
                                
                                if (directory != null)
                                {
                                    System.Diagnostics.Debug.WriteLine($"ReadFileAsync: Found directory '{directory.Name}', throwing InvalidOperationException");
                                    stopwatch.Stop();
                                    _enhancedLogging.LogGGPKOperationComplete("ReadFile", stopwatch.Elapsed, false, 
                                        new Dictionary<string, object>
                                        {
                                            ["FilePath"] = path,
                                            ["Error"] = "Attempted to read directory as file"
                                        });
                                    throw new InvalidOperationException($"Cannot read directory '{path}' as a file. Use directory extraction methods instead.");
                                }
                                
                                stopwatch.Stop();
                                _enhancedLogging.LogGGPKOperationComplete("ReadFile", stopwatch.Elapsed, false, 
                                    new Dictionary<string, object>
                                    {
                                        ["FilePath"] = path,
                                        ["Error"] = "File not found"
                                    });
                                throw new FileNotFoundException($"File not found: {path}");
                            }
                            data = _ggpkWrapper.ReadFile(ggpkFile);
                            nodeInfo = ConvertTreeNodeToNodeInfo(ggpkFile, path);
                        }

                        _enhancedLogging.LogFileOperation("ReadFile", path, nodeInfo.Size, 
                            $"Type: {nodeInfo.Type}, Hash: {nodeInfo.Hash}");
                        
                        var readStartTime = System.Diagnostics.Stopwatch.StartNew();
                        // data is already read above based on file type
                        readStartTime.Stop();

                        _enhancedLogging.LogPerformanceMetric("FileReadTime", readStartTime.Elapsed.TotalMilliseconds, "ms", 
                            new Dictionary<string, object>
                            {
                                ["FilePath"] = path,
                                ["FileSize"] = nodeInfo.Size,
                                ["ActualSize"] = data.Length
                            });
                        
                        // Register the data for memory tracking if it's large
                        if (data.Length > 1024 * 1024) // 1MB threshold
                        {
                            _memoryManagement.RegisterResource(new MemoryResource(data), $"FileData-{Path.GetFileName(path)}", data.Length);
                            _enhancedLogging.LogMemoryOperation("RegisterLargeFile", data.Length, Path.GetFileName(path));
                        }

                        var memoryAfter = GC.GetTotalMemory(false);
                        _enhancedLogging.LogMemoryOperation("ReadFile", memoryAfter - memoryBefore, path);

                        stopwatch.Stop();
                        _enhancedLogging.LogGGPKOperationComplete("ReadFile", stopwatch.Elapsed, true, 
                            new Dictionary<string, object>
                            {
                                ["FilePath"] = path,
                                ["FileSize"] = nodeInfo?.Size ?? data.Length,
                                ["ActualSize"] = data.Length,
                                ["MemoryDelta"] = memoryAfter - memoryBefore,
                                ["ReadTimeMs"] = readStartTime.Elapsed.TotalMilliseconds
                            });
                        
                        return data;
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        _enhancedLogging.LogGGPKOperationComplete("ReadFile", stopwatch.Elapsed, false, 
                            new Dictionary<string, object>
                            {
                                ["FilePath"] = path,
                                ["Error"] = ex.Message
                            });

                        _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ex, false, $"Reading file: {path}")));
                        throw;
                    }
                },
                "ReadFile",
                OperationPriority.Normal,
                cancellationToken
            );
        }

        /// <summary>
        /// Extracts a directory and all its contents to the specified destination path
        /// Uses LibGGPK3's built-in GGPK.Extract method for efficient directory extraction
        /// Reference: VisualGGPK3 example implementation
        /// </summary>
        /// <param name="directoryPath">Path to the directory to extract</param>
        /// <param name="destinationPath">Destination path for extraction</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Number of files extracted</returns>
        public async Task<int> ExtractDirectoryAsync(string directoryPath, string destinationPath, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKService));

            if (!IsLoaded)
                throw new InvalidOperationException("No GGPK file is currently loaded");

            return await _operationQueue.EnqueueOperationAsync(
                (ct) =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Extracting directory '{directoryPath}' to '{destinationPath}'");

                        // Handle bundled GGPK files differently
                        if (_ggpkWrapper.IsBundled && _indexDecompressor.HasIndexFile())
                        {
                            System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Using bundle index extraction for '{directoryPath}'");
                            
                            // For bundled GGPK files, we need to extract files individually using the bundle system
                            // Get all files recursively from the directory using the index decompressor directly
                            System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Getting all files in directory '{directoryPath}'");
                            
                            var allFiles = GetAllFilesInDirectoryFromIndex(directoryPath);
                            
                            if (!allFiles.Any())
                            {
                                System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: No files found in directory '{directoryPath}'");
                                return 0;
                            }

                            System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Found {allFiles.Count} files to extract from bundle index");
            
                            // Log to JSON for debugging
                            _enhancedLogging?.LogGGPKOperation("DirectoryExtraction_FileCount", directoryPath, new Dictionary<string, object>
                            {
                                ["FilesFound"] = allFiles.Count,
                                ["DirectoryPath"] = directoryPath
                            });

                            // Create destination directory
                            Directory.CreateDirectory(destinationPath);

                            var extractedCount = 0;
                            foreach (var fileInfo in allFiles)
                            {
                                try
                                {
                                    // Calculate relative path from the directory being extracted
                                    var relativePath = GetRelativePathFromDirectory(directoryPath, fileInfo.FullPath);
                                    var destFilePath = Path.Combine(destinationPath, relativePath);

                                    // Create subdirectories as needed
                                    var destDir = Path.GetDirectoryName(destFilePath);
                                    if (!string.IsNullOrEmpty(destDir))
                                    {
                                        Directory.CreateDirectory(destDir);
                                    }

                                    // Read file data and write to destination
                                    var fileData = ReadFileInternal(fileInfo.FullPath);
                                    File.WriteAllBytes(destFilePath, fileData);
                                    
                                    extractedCount++;
                                    System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Extracted file {extractedCount}/{allFiles.Count}: {fileInfo.FullPath}");
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Failed to extract file '{fileInfo.FullPath}': {ex.Message}");
                                    // Continue with other files
                                }
                            }

                            System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Successfully extracted {extractedCount} files from bundle index");
                            return extractedCount;
                        }
                        else
                        {
                            // For traditional GGPK files, use the original method
                            System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Using traditional GGPK extraction for '{directoryPath}'");
                            
                            var directory = _ggpkWrapper.FindDirectory(directoryPath);
                            if (directory == null)
                            {
                                // Try without trailing slash
                                if (directoryPath.EndsWith("/"))
                                {
                                    directory = _ggpkWrapper.FindDirectory(directoryPath.TrimEnd('/'));
                                }
                            }

                            if (directory == null)
                            {
                                throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
                            }

                            System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Found directory '{directory.Name}', calling GGPK.Extract");

                            // Use LibGGPK3's built-in directory extraction
                            var extractedCount = _ggpkWrapper.ExtractDirectory(directory, destinationPath);

                            System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Successfully extracted {extractedCount} files");
                            return extractedCount;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"GGPKService.ExtractDirectoryAsync: Error - {ex.Message}");
                        _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ex, false, $"Extracting directory: {directoryPath}")));
                        throw;
                    }
                },
                "ExtractDirectory",
                OperationPriority.Normal,
                cancellationToken
            );
        }

        /// <summary>
        /// Gets all files in a directory recursively from the bundle index
        /// </summary>
        /// <param name="directoryPath">Directory path to search</param>
        /// <returns>List of file information</returns>
        private List<TreeNodeInfo> GetAllFilesInDirectoryFromIndex(string directoryPath)
        {
            var result = new List<TreeNodeInfo>();
            var pathToSearch = directoryPath.TrimEnd('/');
            
            System.Diagnostics.Debug.WriteLine($"GetAllFilesInDirectoryFromIndex: Searching for files in '{pathToSearch}'");
            
            // Get nodes from the specific directory path
            // This returns the direct children of the directory
            var directoryNodes = _indexDecompressor.GetIndexNodesForPath(pathToSearch);
            
            System.Diagnostics.Debug.WriteLine($"GetAllFilesInDirectoryFromIndex: Found {directoryNodes.Count()} nodes in directory '{pathToSearch}'");
            
            // Log to JSON for debugging
            _enhancedLogging?.LogGGPKOperation("DirectorySearch_NodeCount", pathToSearch, new Dictionary<string, object>
            {
                ["NodesFound"] = directoryNodes.Count(),
                ["SearchPath"] = pathToSearch
            });
            
            foreach (var node in directoryNodes)
            {
                System.Diagnostics.Debug.WriteLine($"GetAllFilesInDirectoryFromIndex: Node - Path: '{node.FullPath}', Type: {node.Type}, Name: '{node.Name}'");
                
                // Log each node to JSON
                _enhancedLogging?.LogGGPKOperation("DirectorySearch_NodeFound", node.FullPath, new Dictionary<string, object>
                {
                    ["NodeName"] = node.Name,
                    ["NodeType"] = node.Type.ToString(),
                    ["NodePath"] = node.FullPath,
                    ["SearchPath"] = pathToSearch
                });
                
                if (node.Type == NodeType.File || node.Type == NodeType.BundleFile)
                {
                    result.Add(node);
                    System.Diagnostics.Debug.WriteLine($"GetAllFilesInDirectoryFromIndex: Added file: {node.FullPath}");
                }
                else if (node.Type == NodeType.Directory)
                {
                    // Recursively get files from subdirectories
                    // Make sure to use the full path for recursion
                    var subDirFiles = GetAllFilesInDirectoryFromIndex(node.FullPath);
                    result.AddRange(subDirFiles);
                    System.Diagnostics.Debug.WriteLine($"GetAllFilesInDirectoryFromIndex: Added {subDirFiles.Count} files from subdirectory: {node.FullPath}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"GetAllFilesInDirectoryFromIndex: Total found {result.Count} files in directory '{pathToSearch}'");
            return result;
        }

        /// <summary>
        /// Internal method to read file data synchronously for use within operation queue
        /// </summary>
        /// <param name="path">Path to the file</param>
        /// <returns>File content as byte array</returns>
        private byte[] ReadFileInternal(string path)
        {
            // This is called from within the operation queue, so we can't use the queue again
            // Instead, directly access the wrapper
            try
            {
                System.Diagnostics.Debug.WriteLine($"ReadFileInternal: Reading file '{path}'");
                
                // For bundled GGPK files, try bundle index first
                if (_ggpkWrapper.IsBundled && _indexDecompressor.HasIndexFile())
                {
                    System.Diagnostics.Debug.WriteLine($"ReadFileInternal: Using bundled GGPK for file '{path}'");
                    
                    var bundledGGPK = _ggpkWrapper.GetBundledGGPK();
                    if (bundledGGPK?.Index != null)
                    {
                        // Try to find the file in the bundle index
                        if (bundledGGPK.Index.TryFindNode(path, out var node) && node is LibBundle3.Nodes.IFileNode fileNode)
                        {
                            System.Diagnostics.Debug.WriteLine($"ReadFileInternal: Found file in bundle index: {fileNode.Name}");
                            
                            // Read the file data from the bundle
                            var fileData = fileNode.Record.Read();
                            System.Diagnostics.Debug.WriteLine($"ReadFileInternal: Successfully read {fileData.Length} bytes from bundle file");
                            return fileData.ToArray();
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"ReadFileInternal: File not found in bundle index: {path}");
                        }
                    }
                }
                
                // Fallback to regular GGPK file reading
                var file = _ggpkWrapper.FindFile(path);
                if (file != null)
                {
                    System.Diagnostics.Debug.WriteLine($"ReadFileInternal: Reading from GGPK structure: {file.Name}");
                    return _ggpkWrapper.ReadFile(file);
                }
                
                throw new FileNotFoundException($"File not found: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ReadFileInternal: Error reading file '{path}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gets the relative path of a file from a directory
        /// </summary>
        /// <param name="directoryPath">Base directory path</param>
        /// <param name="filePath">Full file path</param>
        /// <returns>Relative path</returns>
        private string GetRelativePathFromDirectory(string directoryPath, string filePath)
        {
            var dirPath = directoryPath.TrimEnd('/').Replace('\\', '/');
            var fullPath = filePath.Replace('\\', '/').TrimStart('/');
            
            if (string.IsNullOrEmpty(dirPath))
            {
                return fullPath;
            }
            
            if (fullPath.StartsWith(dirPath + "/", StringComparison.OrdinalIgnoreCase))
            {
                return fullPath.Substring(dirPath.Length + 1);
            }
            
            return Path.GetFileName(fullPath);
        }

        /// <summary>
        /// Gets detailed properties of a file or directory
        /// </summary>
        /// <param name="path">Path to the file or directory</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Detailed file properties</returns>
        public async Task<FileProperties> GetFilePropertiesAsync(string path, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKService));

            if (!IsLoaded)
                throw new InvalidOperationException("No GGPK file is currently loaded");

            using var scope = _enhancedLogging.BeginScope("GetFileProperties", new Dictionary<string, object>
            {
                ["Path"] = path
            });

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            _enhancedLogging.LogGGPKOperation("GetFileProperties", path, new Dictionary<string, object>
            {
                ["MemoryBefore"] = memoryBefore
            });

            return await _operationQueue.EnqueueOperationAsync(
                (ct) =>
                {
                    try
                    {
                        var lookupTime = System.Diagnostics.Stopwatch.StartNew();
                        var nodeInfo = GetNodeInfoSync(path);
                        lookupTime.Stop();

                        _enhancedLogging.LogPerformanceMetric("NodeLookupTime", lookupTime.Elapsed.TotalMilliseconds, "ms", 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path,
                                ["Found"] = nodeInfo != null,
                                ["ThreadId"] = Environment.CurrentManagedThreadId
                            });

                        if (nodeInfo == null)
                        {
                            stopwatch.Stop();
                            _enhancedLogging.LogGGPKOperationComplete("GetFileProperties", stopwatch.Elapsed, false, 
                                new Dictionary<string, object>
                                {
                                    ["Path"] = path,
                                    ["Error"] = "Path not found"
                                });
                            throw new FileNotFoundException($"Path not found: {path}");
                        }
                        
                        var result = new FileProperties
                        {
                            Name = nodeInfo.Name,
                            FullPath = nodeInfo.FullPath,
                            Type = nodeInfo.Type,
                            Size = nodeInfo.Size,
                            ModifiedDate = nodeInfo.ModifiedDate,
                            Compression = nodeInfo.Compression,
                            Hash = nodeInfo.Hash,
                            Offset = nodeInfo.Offset,
                            Metadata = new Dictionary<string, object>
                            {
                                ["GGPKVersion"] = _ggpkWrapper.Version,
                                ["IsBundled"] = _ggpkWrapper.IsBundled
                            }
                        };

                        var memoryAfter = GC.GetTotalMemory(false);
                        stopwatch.Stop();

                        _enhancedLogging.LogGGPKOperationComplete("GetFileProperties", stopwatch.Elapsed, true, 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path,
                                ["FileSize"] = nodeInfo.Size,
                                ["NodeType"] = nodeInfo.Type.ToString(),
                                ["MemoryDelta"] = memoryAfter - memoryBefore,
                                ["LookupTimeMs"] = lookupTime.Elapsed.TotalMilliseconds
                            });

                        return result;
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        _enhancedLogging.LogGGPKOperationComplete("GetFileProperties", stopwatch.Elapsed, false, 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path,
                                ["Error"] = ex.Message,
                                ["ExceptionType"] = ex.GetType().Name
                            });

                        _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ex, false, $"Getting file properties: {path}")));
                        throw;
                    }
                },
                "GetFileProperties",
                OperationPriority.Normal,
                cancellationToken
            );
        }

        /// <summary>
        /// Checks if a path exists in the GGPK file
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if the path exists</returns>
        public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKService));

            if (!IsLoaded)
                throw new InvalidOperationException("GGPK file must be loaded before checking if paths exist");

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _enhancedLogging.LogGGPKOperation("ExistsCheck", path, new Dictionary<string, object>
            {
                ["ThreadId"] = Environment.CurrentManagedThreadId
            });

            return await _operationQueue.EnqueueOperationAsync(
                (ct) =>
                {
                    try
                    {
                        var lookupTime = System.Diagnostics.Stopwatch.StartNew();
                        var exists = GetNodeInfoSync(path) != null;
                        lookupTime.Stop();

                        _enhancedLogging.LogPerformanceMetric("ExistenceCheckTime", lookupTime.Elapsed.TotalMilliseconds, "ms", 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path,
                                ["Exists"] = exists,
                                ["ThreadId"] = Environment.CurrentManagedThreadId
                            });

                        stopwatch.Stop();
                        _enhancedLogging.LogGGPKOperationComplete("ExistsCheck", stopwatch.Elapsed, true, 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path,
                                ["Exists"] = exists,
                                ["LookupTimeMs"] = lookupTime.Elapsed.TotalMilliseconds
                            });

                        return exists;
                    }
                    catch (Exception ex)
                    {
                        stopwatch.Stop();
                        _enhancedLogging.LogGGPKOperationComplete("ExistsCheck", stopwatch.Elapsed, false, 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path,
                                ["Error"] = ex.Message,
                                ["ExceptionType"] = ex.GetType().Name
                            });
                        return false;
                    }
                },
                "ExistsCheck",
                OperationPriority.Low,
                cancellationToken
            );
        }

        /// <summary>
        /// Gets the root directory information
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Root directory node</returns>
        public async Task<TreeNodeInfo> GetRootAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GGPKService));

            if (!IsLoaded)
                throw new InvalidOperationException("No GGPK file is currently loaded");

            return await _operationQueue.EnqueueOperationAsync(
                (ct) =>
                {
                    try
                    {
                        System.Diagnostics.Debug.WriteLine("GetRootAsync: Getting root directory");
                        
                        // For bundled GGPK files, create a virtual root that represents the bundle structure
                        if (_ggpkWrapper.IsBundled && _indexDecompressor.HasIndexFile())
                        {
                            try
                            {
                                System.Diagnostics.Debug.WriteLine("Creating virtual root for bundled GGPK");
                                
                                // Create a virtual root node that represents the bundle structure
                                var virtualRoot = new TreeNodeInfo
                                {
                                    Name = "Root",
                                    FullPath = "/",
                                    Type = NodeType.Directory,
                                    Size = 0,
                                    HasChildren = true,
                                    IconPath = "folder",
                                    ModifiedDate = null
                                };
                                
                                System.Diagnostics.Debug.WriteLine("Created virtual bundle root");
                                return virtualRoot;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error creating bundle root: {ex.Message}");
                            }
                        }
                        
                        // Fall back to GGPK root
                        System.Diagnostics.Debug.WriteLine("Using GGPK root");
                        return ConvertTreeNodeToNodeInfo(_ggpkWrapper.Root!, "/");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error getting root: {ex.Message}");
                        _uiThreadMarshaling.PostToUIThread(() => OnErrorOccurred(new Models.ErrorEventArgs(ex, false, "Getting root directory")));
                        throw;
                    }
                },
                "GetRoot",
                OperationPriority.High,
                cancellationToken
            );
        }

        /// <summary>
        /// Cancels the current operation if one is running
        /// </summary>
        public void CancelCurrentOperation()
        {
            _operationQueue.CancelCurrentOperation();
        }

        /// <summary>
        /// Reads a bundle file using the bundle system
        /// </summary>
        /// <param name="path">Path to the bundle file</param>
        /// <returns>File content as byte array</returns>
        private byte[] ReadBundleFile(string path)
        {
            try
            {
                var bundledGGPK = _ggpkWrapper.GetBundledGGPK();
                if (bundledGGPK?.Index == null)
                {
                    throw new InvalidOperationException("Bundle index not available");
                }

                // Try to find the file in the bundle index
                if (bundledGGPK.Index.TryFindNode(path, out var node) && node is LibBundle3.Nodes.IFileNode fileNode)
                {
                    System.Diagnostics.Debug.WriteLine($"Found bundle file node: {fileNode.Name}");
                    
                    // Read the file data from the bundle
                    var fileRecord = fileNode.Record;
                    var readOnlyMemory = fileRecord.Read();
                    
                    // Convert ReadOnlyMemory<byte> to byte[]
                    var data = readOnlyMemory.ToArray();
                    
                    System.Diagnostics.Debug.WriteLine($"Read {data.Length} bytes from bundle file");
                    return data;
                }
                else
                {
                    throw new FileNotFoundException($"Bundle file not found in index: {path}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error reading bundle file {path}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles memory pressure events from the performance monitor
        /// </summary>
        /// <param name="sender">Event sender</param>
        /// <param name="e">Memory pressure event arguments</param>
        private async void OnMemoryPressureDetected(object? sender, MemoryPressureEventArgs e)
        {
            _logger.LogWarning("Memory pressure detected: {PressureLevel}, Current memory: {CurrentMemory:N0} bytes", 
                e.PressureLevel, e.CurrentMemoryUsage);

            try
            {
                switch (e.PressureLevel)
                {
                    case MemoryPressureLevel.Moderate:
                        // Perform light cleanup
                        await _memoryManagement.PerformMemoryCleanupAsync(false);
                        break;

                    case MemoryPressureLevel.High:
                        // Cancel low priority operations and perform cleanup
                        _operationQueue.CancelOperationsByName("GetChildren");
                        await _memoryManagement.PerformMemoryCleanupAsync(true);
                        break;

                    case MemoryPressureLevel.Critical:
                        // Cancel all non-critical operations and force aggressive cleanup
                        _operationQueue.CancelAllOperations();
                        await _memoryManagement.CleanupAllResourcesAsync();
                        await _memoryManagement.PerformMemoryCleanupAsync(true);
                        
                        // Notify UI about critical memory situation
                        _uiThreadMarshaling.PostToUIThread(() => 
                        {
                            OnErrorOccurred(new Models.ErrorEventArgs(
                                new OutOfMemoryException("Critical memory pressure detected. Some operations have been canceled."),
                                true,
                                "Memory pressure handling"
                            ));
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling memory pressure");
            }
        }

        /// <summary>
        /// Converts a TreeNode to TreeNodeInfo (synchronous version for internal use)
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>TreeNodeInfo if found, null otherwise</returns>
        private TreeNodeInfo? GetNodeInfoSync(string path)
        {
            System.Diagnostics.Debug.WriteLine($"GetNodeInfoSync: Looking for path '{path}'");
            
            // For bundled GGPK files, prioritize bundle index lookup
            if (_ggpkWrapper.IsBundled && _indexDecompressor.HasIndexFile())
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Searching in bundle index for path: {path}");
                    var indexNodes = _indexDecompressor.GetIndexNodesForPath(path);
                    var result = indexNodes.FirstOrDefault();
                    if (result != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"Found in bundle index: {result.Name}");
                        return result;
                    }
                    System.Diagnostics.Debug.WriteLine($"Not found in bundle index, trying GGPK structure");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error searching bundle index: {ex.Message}");
                }
            }
            
            // Try to find in regular GGPK structure (fallback or non-bundled)
            var file = _ggpkWrapper.FindFile(path);
            if (file != null)
            {
                System.Diagnostics.Debug.WriteLine($"Found file in GGPK structure: {file.Name}");
                return ConvertTreeNodeToNodeInfo(file, path);
            }
            
            var directory = _ggpkWrapper.FindDirectory(path);
            if (directory != null)
            {
                System.Diagnostics.Debug.WriteLine($"Found directory in GGPK structure: {directory.Name}");
                return ConvertTreeNodeToNodeInfo(directory, path);
            }
            
            System.Diagnostics.Debug.WriteLine($"Path not found anywhere: {path}");
            return null;
        }

        /// <summary>
        /// Converts a TreeNode to TreeNodeInfo
        /// </summary>
        /// <param name="node">TreeNode to convert</param>
        /// <param name="fullPath">Full path of the node</param>
        /// <returns>TreeNodeInfo object</returns>
        private TreeNodeInfo ConvertTreeNodeToNodeInfo(TreeNode node, string fullPath)
        {
            var nodeInfo = new TreeNodeInfo
            {
                Name = node.Name,
                FullPath = fullPath,
                HasChildren = node is DirectoryRecord directory && directory.Any(),
                Hash = node.Hash.ToString(),
                Offset = node.Offset
            };

            if (node is FileRecord file)
            {
                nodeInfo.Type = NodeType.File;
                nodeInfo.Size = file.DataLength;
                nodeInfo.IconPath = GetIconPathForFile(file.Name);
            }
            else if (node is DirectoryRecord)
            {
                nodeInfo.Type = NodeType.Directory;
                nodeInfo.Size = 0;
                nodeInfo.IconPath = "folder";
            }

            return nodeInfo;
        }

        /// <summary>
        /// Gets the appropriate icon path for a file based on its extension
        /// </summary>
        /// <param name="fileName">Name of the file</param>
        /// <returns>Icon path string</returns>
        private string GetIconPathForFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "file";

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            
            return extension switch
            {
                ".dds" => "image",
                ".png" => "image",
                ".jpg" => "image",
                ".jpeg" => "image",
                ".bmp" => "image",
                ".tga" => "image",
                ".ogg" => "audio",
                ".wav" => "audio",
                ".mp3" => "audio",
                ".txt" => "text",
                ".xml" => "code",
                ".json" => "code",
                ".lua" => "code",
                ".hlsl" => "code",
                ".fx" => "code",
                ".dat" => "data",
                ".bin" => "data",
                _ => "file"
            };
        }

        /// <summary>
        /// Raises the GGPKLoaded event
        /// </summary>
        /// <param name="e">Event arguments</param>
        private void OnGGPKLoaded(GGPKLoadedEventArgs e)
        {
            GGPKLoaded?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the ProgressChanged event
        /// </summary>
        /// <param name="percentage">Progress percentage</param>
        /// <param name="operation">Operation description</param>
        /// <param name="status">Status information</param>
        private void OnProgressChanged(double percentage, string operation, string? status = null)
        {
            ProgressChanged?.Invoke(this, new ProgressEventArgs(percentage, operation, status));
        }

        /// <summary>
        /// Raises the ErrorOccurred event
        /// </summary>
        /// <param name="e">Error event arguments</param>
        private void OnErrorOccurred(Models.ErrorEventArgs e)
        {
            ErrorOccurred?.Invoke(this, e);
        }

        /// <summary>
        /// Converts a generic exception to a specific GGPK exception type for better error handling
        /// Reference: Requirements 8.2, 8.4 - Specific error handling for GGPK corruption and bundle failures
        /// </summary>
        /// <param name="exception">The original exception</param>
        /// <param name="filePath">The file path involved in the operation</param>
        /// <param name="context">Additional context about the operation</param>
        /// <returns>A specific GGPK exception type</returns>
        private GGPKException ConvertToGGPKException(Exception exception, string filePath, string context)
        {
            // Gather comprehensive error context
            var errorContext = new Dictionary<string, object>
            {
                ["OriginalExceptionType"] = exception.GetType().Name,
                ["OriginalMessage"] = exception.Message,
                ["Context"] = context,
                ["FilePath"] = filePath ?? "Unknown",
                ["ThreadId"] = Environment.CurrentManagedThreadId,
                ["Timestamp"] = DateTime.UtcNow,
                ["MemoryUsage"] = GC.GetTotalMemory(false),
                ["IsGGPKLoaded"] = IsLoaded,
                ["GGPKVersion"] = IsLoaded ? (object)_ggpkWrapper.Version : "Not loaded",
                ["IsBundled"] = IsLoaded && _ggpkWrapper.IsBundled,
                ["StackTrace"] = exception.StackTrace ?? "No stack trace available"
            };

            // Add file-specific context if available
            if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    errorContext["FileSize"] = fileInfo.Length;
                    errorContext["FileLastModified"] = fileInfo.LastWriteTime;
                    errorContext["FileAttributes"] = fileInfo.Attributes.ToString();
                }
                catch (Exception fileEx)
                {
                    errorContext["FileInfoError"] = fileEx.Message;
                }
            }

            // Add system state context
            errorContext["AvailableMemory"] = GC.GetTotalMemory(false);
            errorContext["ProcessorCount"] = Environment.ProcessorCount;
            errorContext["OSVersion"] = Environment.OSVersion.ToString();

            // Log comprehensive error context
            _enhancedLogging.LogEntry(new GGPKLogEntry
            {
                Level = Microsoft.Extensions.Logging.LogLevel.Error,
                Category = "ErrorContext",
                Operation = "ConvertToGGPKException",
                FilePath = filePath,
                ErrorMessage = exception.Message,
                Context = errorContext
            });

            // Log the original exception for diagnostic purposes
            _logger.LogError(exception, "Converting exception in context: {Context}, File: {FilePath}, ErrorContext: {@ErrorContext}", 
                context, filePath, errorContext);

            return exception switch
            {
                // File system related exceptions
                FileNotFoundException => new FileOperationException(filePath ?? "Unknown", FileOperationType.Read, 
                    $"GGPK file not found: {filePath}", exception),
                UnauthorizedAccessException => new FileOperationException(filePath ?? "Unknown", FileOperationType.Read, 
                    $"Access denied to GGPK file: {filePath}. Check file permissions.", exception),
                DirectoryNotFoundException => new FileOperationException(filePath ?? "Unknown", FileOperationType.Read, 
                    $"Directory not found for GGPK file: {filePath}", exception),
                
                // Memory related exceptions
                OutOfMemoryException => new GGPKException(
                    "Insufficient memory to process GGPK file. Try closing other applications or working with smaller files.", exception),
                
                // IO related exceptions that might indicate corruption
                IOException ioEx when ioEx.Message.Contains("corrupt") || ioEx.Message.Contains("invalid") => 
                    new GGPKCorruptedException($"GGPK file corruption detected: {ioEx.Message}", 0, ioEx),
                IOException ioEx => new GGPKException($"IO error while processing GGPK file: {ioEx.Message}", ioEx),
                
                // Format related exceptions that might indicate corruption
                FormatException formatEx => new GGPKCorruptedException(
                    $"Invalid GGPK file format: {formatEx.Message}", 0, formatEx),
                InvalidDataException dataEx => new GGPKCorruptedException(
                    $"Invalid data in GGPK file: {dataEx.Message}", 0, dataEx),
                
                // Bundle related exceptions
                Exception ex when ex.Message.Contains("bundle") || ex.Message.Contains("oo2core") || ex.Message.Contains("decompression") =>
                    new BundleDecompressionException(Path.GetFileName(filePath) ?? "Unknown", 
                        $"Bundle decompression failed: {ex.Message}", ex),
                
                // Threading related exceptions
                InvalidOperationException invOpEx when invOpEx.Message.Contains("thread") =>
                    new GGPKException($"Threading error in GGPK operation: {invOpEx.Message}", invOpEx),
                
                // Already a GGPK exception - just return it
                GGPKException ggpkEx => ggpkEx,
                
                // Generic fallback
                _ => new GGPKException($"Unexpected error in {context}: {exception.Message}", exception)
            };
        }

        /// <summary>
        /// Handles exceptions with comprehensive error handling and recovery attempts
        /// Reference: Requirements 8.3, 8.4, 8.5 - Error logging, recovery mechanisms, and graceful degradation
        /// </summary>
        /// <param name="exception">The exception to handle</param>
        /// <param name="context">Context information about the operation</param>
        /// <param name="filePath">File path if applicable</param>
        /// <param name="showDialog">Whether to show error dialog to user</param>
        /// <returns>True if recovery was successful, false otherwise</returns>
        private async Task<bool> HandleExceptionWithRecoveryAsync(Exception exception, string context, string filePath = "", bool showDialog = true)
        {
            var recoveryStartTime = System.Diagnostics.Stopwatch.StartNew();
            var ggpkException = ConvertToGGPKException(exception, filePath, context);
            
            // Log the error recovery attempt
            var recoveryContext = new Dictionary<string, object>
            {
                ["ExceptionType"] = ggpkException.GetType().Name,
                ["Context"] = context,
                ["FilePath"] = filePath,
                ["ShowDialog"] = showDialog,
                ["ThreadId"] = Environment.CurrentManagedThreadId,
                ["Timestamp"] = DateTime.UtcNow
            };

            _enhancedLogging.LogGGPKOperation("ErrorRecoveryAttempt", filePath, recoveryContext);
            _errorHandlingService.LogError(ggpkException, context);
            
            // Attempt recovery based on exception type
            bool recoverySuccessful = false;
            string recoveryMethod = "None";
            var recoveryMetrics = new Dictionary<string, object>();
            
            switch (ggpkException)
            {
                case GGPKCorruptedException corruptedException:
                    recoveryMethod = "CorruptionRecovery";
                    var corruptionRecoveryTime = System.Diagnostics.Stopwatch.StartNew();
                    recoverySuccessful = await _errorHandlingService.AttemptCorruptionRecoveryAsync(corruptedException);
                    corruptionRecoveryTime.Stop();
                    
                    recoveryMetrics["RecoveryTimeMs"] = corruptionRecoveryTime.Elapsed.TotalMilliseconds;
                    recoveryMetrics["CorruptionOffset"] = corruptedException.CorruptedOffset;
                    
                    _enhancedLogging.LogPerformanceMetric("CorruptionRecoveryTime", 
                        corruptionRecoveryTime.Elapsed.TotalMilliseconds, "ms", 
                        new Dictionary<string, object>
                        {
                            ["Success"] = recoverySuccessful,
                            ["FilePath"] = filePath,
                            ["CorruptionOffset"] = corruptedException.CorruptedOffset
                        });
                    break;
                    
                case BundleDecompressionException bundleException:
                    recoveryMethod = "BundleRecovery";
                    var bundleRecoveryTime = System.Diagnostics.Stopwatch.StartNew();
                    recoverySuccessful = await _errorHandlingService.AttemptBundleRecoveryAsync(bundleException);
                    bundleRecoveryTime.Stop();
                    
                    recoveryMetrics["RecoveryTimeMs"] = bundleRecoveryTime.Elapsed.TotalMilliseconds;
                    recoveryMetrics["BundleName"] = bundleException.BundleName;
                    
                    _enhancedLogging.LogPerformanceMetric("BundleRecoveryTime", 
                        bundleRecoveryTime.Elapsed.TotalMilliseconds, "ms", 
                        new Dictionary<string, object>
                        {
                            ["Success"] = recoverySuccessful,
                            ["FilePath"] = filePath,
                            ["BundleName"] = bundleException.BundleName
                        });
                    break;
                    
                case FileOperationException fileOpException when fileOpException.OperationType == FileOperationType.Read:
                    recoveryMethod = "FileOperationGuidance";
                    // For file operation errors, we can't really recover, but we can provide helpful guidance
                    _errorHandlingService.LogWarning($"File operation failed: {fileOpException.Message}", context);
                    
                    recoveryMetrics["OperationType"] = fileOpException.OperationType.ToString();
                    recoveryMetrics["GuidanceProvided"] = true;
                    break;
                    
                default:
                    recoveryMethod = "GenericHandling";
                    recoveryMetrics["FallbackHandling"] = true;
                    break;
            }
            
            recoveryStartTime.Stop();
            
            // Log comprehensive recovery results
            var recoveryResults = new Dictionary<string, object>
            {
                ["RecoveryMethod"] = recoveryMethod,
                ["RecoverySuccessful"] = recoverySuccessful,
                ["TotalRecoveryTimeMs"] = recoveryStartTime.Elapsed.TotalMilliseconds,
                ["ShowDialog"] = showDialog,
                ["ExceptionType"] = ggpkException.GetType().Name,
                ["Context"] = context,
                ["FilePath"] = filePath
            };
            
            // Add recovery-specific metrics
            foreach (var metric in recoveryMetrics)
            {
                recoveryResults[metric.Key] = metric.Value;
            }

            _enhancedLogging.LogGGPKOperationComplete("ErrorRecoveryAttempt", recoveryStartTime.Elapsed, recoverySuccessful, recoveryResults);
            
            // Log user-friendly error message creation
            if (showDialog)
            {
                var userMessageTime = System.Diagnostics.Stopwatch.StartNew();
                await _errorHandlingService.HandleGGPKExceptionAsync(ggpkException, context);
                userMessageTime.Stop();
                
                _enhancedLogging.LogPerformanceMetric("UserErrorMessageTime", userMessageTime.Elapsed.TotalMilliseconds, "ms", 
                    new Dictionary<string, object>
                    {
                        ["ExceptionType"] = ggpkException.GetType().Name,
                        ["Context"] = context
                    });
            }
            
            // Raise the error event for any listeners
            OnErrorOccurred(new Models.ErrorEventArgs(ggpkException, !recoverySuccessful, context));
            
            return recoverySuccessful;
        }

        /// <summary>
        /// Disposes the service and releases all resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            
            // Unsubscribe from events
            _performanceMonitor.MemoryPressureDetected -= OnMemoryPressureDetected;
            
            // Stop services
            _operationQueue.Stop();
            _performanceMonitor.StopMonitoring();
            
            // Clean up resources
            _ggpkWrapper.Dispose();
            _operationQueue.Dispose();
            _performanceMonitor.Dispose();
            _memoryManagement.Dispose();
            
            _logger.LogInformation("GGPK service disposed");
        }

        /// <summary>
        /// Validates GGPK file integrity and logs specific corruption details
        /// </summary>
        /// <param name="filePath">Path to the GGPK file to validate</param>
        /// <returns>True if file is valid, false if corrupted</returns>
        private async Task<bool> ValidateGGPKFileIntegrityAsync(string filePath)
        {
            var validationStartTime = System.Diagnostics.Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            
            var validationContext = new Dictionary<string, object>
            {
                ["FilePath"] = filePath,
                ["ValidationStartTime"] = DateTime.UtcNow,
                ["MemoryBefore"] = memoryBefore
            };

            _enhancedLogging.LogGGPKOperation("ValidateGGPKIntegrity", filePath, validationContext);

            try
            {
                if (!File.Exists(filePath))
                {
                    validationStartTime.Stop();
                    _enhancedLogging.LogGGPKOperationComplete("ValidateGGPKIntegrity", validationStartTime.Elapsed, false, 
                        new Dictionary<string, object>
                        {
                            ["FilePath"] = filePath,
                            ["ValidationError"] = "File does not exist",
                            ["CorruptionType"] = "FileNotFound"
                        });
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                validationContext["FileSize"] = fileInfo.Length;
                validationContext["FileLastModified"] = fileInfo.LastWriteTime;

                // Basic file size validation
                if (fileInfo.Length < 1024) // GGPK files should be at least 1KB
                {
                    validationStartTime.Stop();
                    _enhancedLogging.LogGGPKOperationComplete("ValidateGGPKIntegrity", validationStartTime.Elapsed, false, 
                        new Dictionary<string, object>
                        {
                            ["FilePath"] = filePath,
                            ["FileSize"] = fileInfo.Length,
                            ["ValidationError"] = "File too small to be valid GGPK",
                            ["CorruptionType"] = "InvalidFileSize",
                            ["MinimumExpectedSize"] = 1024
                        });
                    return false;
                }

                // Try to read GGPK header
                using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var headerBuffer = new byte[16];
                var bytesRead = await fileStream.ReadAsync(headerBuffer, 0, headerBuffer.Length);
                
                if (bytesRead < headerBuffer.Length)
                {
                    validationStartTime.Stop();
                    _enhancedLogging.LogGGPKOperationComplete("ValidateGGPKIntegrity", validationStartTime.Elapsed, false, 
                        new Dictionary<string, object>
                        {
                            ["FilePath"] = filePath,
                            ["ValidationError"] = "Cannot read GGPK header",
                            ["CorruptionType"] = "HeaderReadFailure",
                            ["BytesRead"] = bytesRead,
                            ["ExpectedBytes"] = headerBuffer.Length
                        });
                    return false;
                }

                // Check for GGPK signature (simplified validation)
                var headerString = System.Text.Encoding.ASCII.GetString(headerBuffer, 0, 4);
                if (headerString != "GGPK")
                {
                    validationStartTime.Stop();
                    _enhancedLogging.LogGGPKOperationComplete("ValidateGGPKIntegrity", validationStartTime.Elapsed, false, 
                        new Dictionary<string, object>
                        {
                            ["FilePath"] = filePath,
                            ["ValidationError"] = "Invalid GGPK signature",
                            ["CorruptionType"] = "InvalidSignature",
                            ["ExpectedSignature"] = "GGPK",
                            ["ActualSignature"] = headerString,
                            ["HeaderBytes"] = Convert.ToHexString(headerBuffer)
                        });
                    return false;
                }

                var memoryAfter = GC.GetTotalMemory(false);
                validationStartTime.Stop();

                _enhancedLogging.LogGGPKOperationComplete("ValidateGGPKIntegrity", validationStartTime.Elapsed, true, 
                    new Dictionary<string, object>
                    {
                        ["FilePath"] = filePath,
                        ["FileSize"] = fileInfo.Length,
                        ["ValidationSuccessful"] = true,
                        ["HeaderSignature"] = headerString,
                        ["MemoryDelta"] = memoryAfter - memoryBefore
                    });

                return true;
            }
            catch (Exception ex)
            {
                validationStartTime.Stop();
                
                var corruptionDetails = new Dictionary<string, object>
                {
                    ["FilePath"] = filePath,
                    ["ValidationError"] = ex.Message,
                    ["CorruptionType"] = "ValidationException",
                    ["ExceptionType"] = ex.GetType().Name,
                    ["StackTrace"] = ex.StackTrace ?? "No stack trace available"
                };

                _enhancedLogging.LogGGPKOperationComplete("ValidateGGPKIntegrity", validationStartTime.Elapsed, false, corruptionDetails);

                // Log detailed corruption information
                _enhancedLogging.LogEntry(new GGPKLogEntry
                {
                    Level = Microsoft.Extensions.Logging.LogLevel.Warning,
                    Category = "Validation",
                    Operation = "GGPKCorruptionDetected",
                    FilePath = filePath,
                    ErrorMessage = ex.Message,
                    Context = corruptionDetails
                });

                return false;
            }
        }

        /// <summary>
        /// Simple wrapper for tracking byte arrays in memory management
        /// </summary>
        private class MemoryResource : IDisposable
        {
            private byte[]? _data;
            private volatile bool _disposed;

            public MemoryResource(byte[] data)
            {
                _data = data;
            }

            public void Dispose()
            {
                if (_disposed)
                    return;

                _disposed = true;
                _data = null;
            }
        }
    }
}