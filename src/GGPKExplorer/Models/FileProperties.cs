using System;
using System.Collections.Generic;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Detailed properties of a file or directory
    /// </summary>
    public class FileProperties
    {
        /// <summary>
        /// Name of the file or directory
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full path within the GGPK structure
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Type of the node
        /// </summary>
        public NodeType Type { get; set; }

        /// <summary>
        /// Size in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Formatted size string
        /// </summary>
        public string FormattedSize { get; set; } = string.Empty;

        /// <summary>
        /// Last modified date if available
        /// </summary>
        public DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// File extension
        /// </summary>
        public string Extension { get; set; } = string.Empty;

        /// <summary>
        /// MIME type or file type description
        /// </summary>
        public string TypeDescription { get; set; } = string.Empty;

        /// <summary>
        /// Hash value of the file content
        /// </summary>
        public string? Hash { get; set; }

        /// <summary>
        /// Hash algorithm used
        /// </summary>
        public string? HashAlgorithm { get; set; }

        /// <summary>
        /// Offset within the GGPK file
        /// </summary>
        public long? Offset { get; set; }

        /// <summary>
        /// Compression information
        /// </summary>
        public CompressionInfo? Compression { get; set; }

        /// <summary>
        /// Bundle information if this is a bundle file
        /// </summary>
        public BundleInfo? Bundle { get; set; }

        /// <summary>
        /// Additional metadata specific to the file type
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();

        /// <summary>
        /// For directories, the number of child items
        /// </summary>
        public int? ChildCount { get; set; }

        /// <summary>
        /// For directories, the total size of all children
        /// </summary>
        public long? TotalChildSize { get; set; }

        /// <summary>
        /// Whether the file can be extracted
        /// </summary>
        public bool CanExtract { get; set; } = true;

        /// <summary>
        /// Whether the file can be previewed
        /// </summary>
        public bool CanPreview { get; set; } = false;

        /// <summary>
        /// Any errors encountered while getting properties
        /// </summary>
        public string[] Errors { get; set; } = [];
    }

    /// <summary>
    /// Information about bundle files
    /// </summary>
    public class BundleInfo
    {
        /// <summary>
        /// Name of the bundle
        /// </summary>
        public string BundleName { get; set; } = string.Empty;

        /// <summary>
        /// Index within the bundle
        /// </summary>
        public int BundleIndex { get; set; }

        /// <summary>
        /// Compression method used in the bundle
        /// </summary>
        public string CompressionMethod { get; set; } = string.Empty;

        /// <summary>
        /// Bundle format version
        /// </summary>
        public int FormatVersion { get; set; }

        /// <summary>
        /// Additional bundle-specific metadata
        /// </summary>
        public Dictionary<string, object> BundleMetadata { get; set; } = new();
    }
}