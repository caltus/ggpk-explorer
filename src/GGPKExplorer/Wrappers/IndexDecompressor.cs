using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using LibBundle3;
using LibBundle3.Nodes;
using LibBundledGGPK3;
using GGPKExplorer.Models;
using GGPKExplorer.Services;

namespace GGPKExplorer.Wrappers
{
    /// <summary>
    /// Handles decompression and processing of _.index.bin files using TreeNode structures
    /// </summary>
    public class IndexDecompressor
    {
        private readonly GGPKWrapper _ggpkWrapper;
        private readonly IEnhancedLoggingService? _enhancedLogging;

        /// <summary>
        /// Initializes a new instance of the IndexDecompressor class
        /// </summary>
        /// <param name="ggpkWrapper">GGPK wrapper instance</param>
        /// <param name="enhancedLogging">Optional enhanced logging service</param>
        public IndexDecompressor(GGPKWrapper ggpkWrapper, IEnhancedLoggingService? enhancedLogging = null)
        {
            _ggpkWrapper = ggpkWrapper ?? throw new ArgumentNullException(nameof(ggpkWrapper));
            _enhancedLogging = enhancedLogging;
        }

        /// <summary>
        /// Checks if the current GGPK file has an index file
        /// </summary>
        /// <returns>True if index file exists</returns>
        public bool HasIndexFile()
        {
            try
            {
                var bundledGGPK = _ggpkWrapper.GetBundledGGPK();
                return bundledGGPK?.Index != null;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Decompresses the index and returns the tree node collection
        /// </summary>
        /// <returns>Collection of tree nodes from the index</returns>
        public IEnumerable<TreeNodeInfo> DecompressIndex()
        {
            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);
            var jsonLogger = new JsonLoggingService(Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonLoggingService>.Instance);

            var correlationId = jsonLogger.BeginOperationScope("DecompressIndex", new Dictionary<string, object>
            {
                ["MemoryBefore"] = memoryBefore,
                ["ThreadId"] = Environment.CurrentManagedThreadId
            });

            _enhancedLogging?.LogGGPKOperation("DecompressIndex", null, new Dictionary<string, object>
            {
                ["MemoryBefore"] = memoryBefore,
                ["ThreadId"] = Environment.CurrentManagedThreadId,
                ["CorrelationId"] = correlationId
            });

            var bundledGGPK = _ggpkWrapper.GetBundledGGPK();
            if (bundledGGPK?.Index == null)
            {
                stopwatch.Stop();
                _enhancedLogging?.LogGGPKOperationComplete("DecompressIndex", stopwatch.Elapsed, false, 
                    new Dictionary<string, object>
                    {
                        ["Error"] = "No bundled GGPK or index available"
                    });
                throw new InvalidOperationException("No bundled GGPK or index available");
            }

            try
            {
                var indexAccessTime = Stopwatch.StartNew();
                var index = bundledGGPK.Index;
                var root = index.Root;
                indexAccessTime.Stop();

                _enhancedLogging?.LogPerformanceMetric("IndexAccessTime", indexAccessTime.Elapsed.TotalMilliseconds, "ms", 
                    new Dictionary<string, object>
                    {
                        ["HasRoot"] = root != null,
                        ["ThreadId"] = Environment.CurrentManagedThreadId
                    });

                var conversionTime = Stopwatch.StartNew();
                var result = root != null ? ConvertTreeNodesToNodeInfo(root).ToList() : new List<TreeNodeInfo>();
                conversionTime.Stop();

                var memoryAfter = GC.GetTotalMemory(false);
                var memoryDelta = memoryAfter - memoryBefore;

                _enhancedLogging?.LogPerformanceMetric("BundleDecompressionTime", conversionTime.Elapsed.TotalMilliseconds, "ms", 
                    new Dictionary<string, object>
                    {
                        ["NodesConverted"] = result.Count,
                        ["MemoryDelta"] = memoryDelta,
                        ["CompressionRatio"] = CalculateCompressionRatio(result),
                        ["ThreadId"] = Environment.CurrentManagedThreadId
                    });

                _enhancedLogging?.LogMemoryOperation("BundleDecompression", memoryDelta, "IndexDecompression");

                stopwatch.Stop();
                _enhancedLogging?.LogGGPKOperationComplete("DecompressIndex", stopwatch.Elapsed, true, 
                    new Dictionary<string, object>
                    {
                        ["NodesCount"] = result.Count,
                        ["MemoryDelta"] = memoryDelta,
                        ["IndexAccessTimeMs"] = indexAccessTime.Elapsed.TotalMilliseconds,
                        ["ConversionTimeMs"] = conversionTime.Elapsed.TotalMilliseconds
                    });

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _enhancedLogging?.LogGGPKOperationComplete("DecompressIndex", stopwatch.Elapsed, false, 
                    new Dictionary<string, object>
                    {
                        ["Error"] = ex.Message,
                        ["ExceptionType"] = ex.GetType().Name
                    });

                throw new BundleDecompressionException("_.index.bin", 
                    "Failed to decompress index file", ex);
            }
        }

        /// <summary>
        /// Gets tree nodes for a specific path in the index
        /// </summary>
        /// <param name="path">Path to get nodes for</param>
        /// <returns>Collection of tree node info</returns>
        public IEnumerable<TreeNodeInfo> GetIndexNodesForPath(string path)
        {
            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            _enhancedLogging?.LogGGPKOperation("GetIndexNodesForPath", path, new Dictionary<string, object>
            {
                ["MemoryBefore"] = memoryBefore,
                ["ThreadId"] = Environment.CurrentManagedThreadId
            });

            var bundledGGPK = _ggpkWrapper.GetBundledGGPK();
            if (bundledGGPK?.Index == null)
            {
                stopwatch.Stop();
                _enhancedLogging?.LogGGPKOperationComplete("GetIndexNodesForPath", stopwatch.Elapsed, false, 
                    new Dictionary<string, object>
                    {
                        ["Path"] = path,
                        ["Error"] = "No bundled GGPK or index available"
                    });
                return Enumerable.Empty<TreeNodeInfo>();
            }

            try
            {
                var searchTime = Stopwatch.StartNew();
                var index = bundledGGPK.Index;
                
                // Handle root path specially - for root, use the index root directly
                bool found;
                ITreeNode? node;
                
                if (string.IsNullOrEmpty(path) || path == "/")
                {
                    node = index.Root;
                    found = node != null;
                    _enhancedLogging?.LogPerformanceMetric("IndexRootAccess", 1, "count", 
                        new Dictionary<string, object>
                        {
                            ["HasRoot"] = found,
                            ["RootType"] = node?.GetType().Name ?? "null"
                        });
                }
                else
                {
                    found = index.TryFindNode(path, out node);
                    _enhancedLogging?.LogPerformanceMetric("IndexNodeSearch", found ? 1 : 0, "found", 
                        new Dictionary<string, object>
                        {
                            ["Path"] = path,
                            ["NodeType"] = node?.GetType().Name ?? "null"
                        });
                }
                
                searchTime.Stop();

                _enhancedLogging?.LogPerformanceMetric("IndexNodeSearchTime", searchTime.Elapsed.TotalMilliseconds, "ms", 
                    new Dictionary<string, object>
                    {
                        ["Path"] = path,
                        ["Found"] = found,
                        ["NodeType"] = node?.GetType().Name ?? "null",
                        ["ThreadId"] = Environment.CurrentManagedThreadId
                    });

                IEnumerable<TreeNodeInfo> result = Enumerable.Empty<TreeNodeInfo>();
                int nodeCount = 0;

                if (found && node != null)
                {
                    var conversionTime = Stopwatch.StartNew();
                    
                    if (node is IDirectoryNode directoryNode)
                    {
                        result = ConvertChildrenToNodeInfo(directoryNode);
                        nodeCount = directoryNode.Children.Count;
                        
                        // Log child names for debugging
                        var childNames = directoryNode.Children.Select(c => c.Name).ToArray();
                        _enhancedLogging?.LogPerformanceMetric("DirectoryChildrenCount", nodeCount, "count", 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path,
                                ["DirectoryName"] = directoryNode.Name,
                                ["ChildNames"] = string.Join(", ", childNames)
                            });
                    }
                    else if (node is IFileNode fileNode)
                    {
                        result = new[] { ConvertFileNodeToNodeInfo(fileNode) };
                        nodeCount = 1;

                        var record = fileNode.Record;
                        _enhancedLogging?.LogPerformanceMetric("BundleFileSize", record.Size, "bytes", 
                            new Dictionary<string, object>
                            {
                                ["Path"] = path,
                                ["FileName"] = fileNode.Name,
                                ["HasBundleRecord"] = record.BundleRecord != null
                            });
                    }
                    
                    conversionTime.Stop();
                    _enhancedLogging?.LogPerformanceMetric("NodeConversionTime", conversionTime.Elapsed.TotalMilliseconds, "ms", 
                        new Dictionary<string, object>
                        {
                            ["Path"] = path,
                            ["NodesConverted"] = nodeCount
                        });
                }

                var memoryAfter = GC.GetTotalMemory(false);
                var memoryDelta = memoryAfter - memoryBefore;

                if (memoryDelta > 1024) // Log memory usage if > 1KB
                {
                    _enhancedLogging?.LogMemoryOperation("GetIndexNodes", memoryDelta, path);
                }

                stopwatch.Stop();
                _enhancedLogging?.LogGGPKOperationComplete("GetIndexNodesForPath", stopwatch.Elapsed, true, 
                    new Dictionary<string, object>
                    {
                        ["Path"] = path,
                        ["NodesFound"] = nodeCount,
                        ["MemoryDelta"] = memoryDelta,
                        ["SearchTimeMs"] = searchTime.Elapsed.TotalMilliseconds
                    });

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _enhancedLogging?.LogGGPKOperationComplete("GetIndexNodesForPath", stopwatch.Elapsed, false, 
                    new Dictionary<string, object>
                    {
                        ["Path"] = path,
                        ["Error"] = ex.Message,
                        ["ExceptionType"] = ex.GetType().Name
                    });

                throw new BundleDecompressionException("_.index.bin", 
                    $"Failed to get nodes for path: {path}", ex);
            }
        }

        /// <summary>
        /// Converts LibBundle3 tree nodes to TreeNodeInfo objects
        /// </summary>
        /// <param name="rootNode">Root directory node</param>
        /// <returns>Collection of TreeNodeInfo objects</returns>
        private IEnumerable<TreeNodeInfo> ConvertTreeNodesToNodeInfo(IDirectoryNode rootNode)
        {
            return ConvertChildrenToNodeInfo(rootNode);
        }

        /// <summary>
        /// Converts children of a directory node to TreeNodeInfo objects
        /// </summary>
        /// <param name="directoryNode">Directory node to convert children from</param>
        /// <returns>Collection of TreeNodeInfo objects</returns>
        private IEnumerable<TreeNodeInfo> ConvertChildrenToNodeInfo(IDirectoryNode directoryNode)
        {
            var result = new List<TreeNodeInfo>();

            foreach (var child in directoryNode.Children)
            {
                // Skip Bundles2 folder at root level when using bundle index
                // as the bundle content is already accessible through the index structure
                if (child.Name == "Bundles2" && IsRootDirectory(directoryNode))
                {
                    _enhancedLogging?.LogPerformanceMetric("SkippedBundles2Folder", 1, "count", 
                        new Dictionary<string, object>
                        {
                            ["Reason"] = "Redundant when using bundle index",
                            ["ParentPath"] = ITreeNode.GetPath(directoryNode)
                        });
                    continue;
                }

                if (child is IDirectoryNode childDir)
                {
                    result.Add(ConvertDirectoryNodeToNodeInfo(childDir));
                }
                else if (child is IFileNode childFile)
                {
                    result.Add(ConvertFileNodeToNodeInfo(childFile));
                }
            }

            return result;
        }

        /// <summary>
        /// Checks if a directory node is the root directory
        /// </summary>
        /// <param name="directoryNode">Directory node to check</param>
        /// <returns>True if this is the root directory</returns>
        private bool IsRootDirectory(IDirectoryNode directoryNode)
        {
            var path = ITreeNode.GetPath(directoryNode);
            return string.IsNullOrEmpty(path) || path == "/" || path == "\\";
        }

        /// <summary>
        /// Converts a directory node to TreeNodeInfo
        /// </summary>
        /// <param name="directoryNode">Directory node to convert</param>
        /// <returns>TreeNodeInfo object</returns>
        private TreeNodeInfo ConvertDirectoryNodeToNodeInfo(IDirectoryNode directoryNode)
        {
            var fullPath = ITreeNode.GetPath(directoryNode);
            
            return new TreeNodeInfo
            {
                Name = directoryNode.Name,
                FullPath = fullPath,
                Type = NodeType.Directory,
                Size = 0, // Directories don't have size
                HasChildren = directoryNode.Children.Count > 0,
                IconPath = "folder",
                ModifiedDate = null // Bundle directories don't have modification dates
            };
        }

        /// <summary>
        /// Converts a file node to TreeNodeInfo
        /// </summary>
        /// <param name="fileNode">File node to convert</param>
        /// <returns>TreeNodeInfo object</returns>
        private TreeNodeInfo ConvertFileNodeToNodeInfo(IFileNode fileNode)
        {
            var fullPath = ITreeNode.GetPath(fileNode);
            var record = fileNode.Record;
            
            var nodeInfo = new TreeNodeInfo
            {
                Name = fileNode.Name,
                FullPath = fullPath,
                Type = NodeType.BundleFile,
                Size = record.Size,
                HasChildren = false,
                IconPath = GetIconPathForFile(fileNode.Name),
                ModifiedDate = null // Bundle files don't have modification dates
            };

            // Add compression information if available
            if (record.BundleRecord != null)
            {
                nodeInfo.Compression = new CompressionInfo
                {
                    Type = CompressionType.Oodle,
                    CompressedSize = record.Size, // This is the compressed size in bundle
                    UncompressedSize = record.Size, // We don't have uncompressed size readily available
                    AdditionalInfo = $"Bundle: {record.BundleRecord.Path}"
                };
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

            var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            
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
        /// Parses an index entry from raw data
        /// </summary>
        /// <param name="data">Raw index entry data</param>
        /// <returns>TreeNodeInfo object</returns>
        public TreeNodeInfo ParseIndexEntry(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Invalid index entry data", nameof(data));

            var stopwatch = Stopwatch.StartNew();
            var memoryBefore = GC.GetTotalMemory(false);

            _enhancedLogging?.LogGGPKOperation("ParseIndexEntry", null, new Dictionary<string, object>
            {
                ["DataSize"] = data.Length,
                ["MemoryBefore"] = memoryBefore
            });

            try
            {
                // This is a simplified implementation
                // In a real scenario, you would parse the binary data according to the index format
                var result = new TreeNodeInfo
                {
                    Name = "ParsedEntry",
                    FullPath = "/ParsedEntry",
                    Type = NodeType.BundleFile,
                    Size = data.Length,
                    HasChildren = false,
                    IconPath = "file"
                };

                var memoryAfter = GC.GetTotalMemory(false);
                stopwatch.Stop();

                _enhancedLogging?.LogGGPKOperationComplete("ParseIndexEntry", stopwatch.Elapsed, true, 
                    new Dictionary<string, object>
                    {
                        ["DataSize"] = data.Length,
                        ["MemoryDelta"] = memoryAfter - memoryBefore,
                        ["ParsedSuccessfully"] = true
                    });

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _enhancedLogging?.LogGGPKOperationComplete("ParseIndexEntry", stopwatch.Elapsed, false, 
                    new Dictionary<string, object>
                    {
                        ["DataSize"] = data.Length,
                        ["Error"] = ex.Message,
                        ["ExceptionType"] = ex.GetType().Name
                    });

                throw new BundleDecompressionException("index_entry", 
                    "Failed to parse index entry", ex);
            }
        }

        /// <summary>
        /// Calculates compression ratio for bundle files
        /// </summary>
        /// <param name="nodes">Collection of tree node info</param>
        /// <returns>Compression ratio as a percentage</returns>
        private double CalculateCompressionRatio(IEnumerable<TreeNodeInfo> nodes)
        {
            var bundleFiles = nodes.Where(n => n.Type == NodeType.BundleFile && n.Compression != null);
            if (!bundleFiles.Any())
                return 0.0;

            var totalCompressed = bundleFiles.Sum(f => f.Compression?.CompressedSize ?? 0);
            var totalUncompressed = bundleFiles.Sum(f => f.Compression?.UncompressedSize ?? 0);

            if (totalUncompressed == 0)
                return 0.0;

            return (1.0 - (double)totalCompressed / totalUncompressed) * 100.0;
        }
    }
}