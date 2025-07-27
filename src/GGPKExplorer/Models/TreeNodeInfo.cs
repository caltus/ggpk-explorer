using System;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Represents information about a node in the GGPK tree structure
    /// </summary>
    public class TreeNodeInfo
    {
        /// <summary>
        /// Display name of the node
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the node within the GGPK structure
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Type of the node (Directory, File, Bundle, etc.)
        /// </summary>
        public NodeType Type { get; set; }

        /// <summary>
        /// Size of the node in bytes (0 for directories)
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Last modified date if available
        /// </summary>
        public DateTime? ModifiedDate { get; set; }

        /// <summary>
        /// Compression information for the node
        /// </summary>
        public CompressionInfo? Compression { get; set; }

        /// <summary>
        /// Indicates whether this node has child nodes
        /// </summary>
        public bool HasChildren { get; set; }

        /// <summary>
        /// Path to the icon resource for this node type
        /// </summary>
        public string IconPath { get; set; } = string.Empty;

        /// <summary>
        /// Hash value of the file content (if applicable)
        /// </summary>
        public string? Hash { get; set; }

        /// <summary>
        /// Offset within the GGPK file (if applicable)
        /// </summary>
        public long? Offset { get; set; }
    }
}