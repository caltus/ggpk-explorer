namespace GGPKExplorer.Models
{
    /// <summary>
    /// Contains information about file compression
    /// </summary>
    public class CompressionInfo
    {
        /// <summary>
        /// Type of compression used
        /// </summary>
        public CompressionType Type { get; set; }

        /// <summary>
        /// Compressed size in bytes
        /// </summary>
        public long CompressedSize { get; set; }

        /// <summary>
        /// Uncompressed size in bytes
        /// </summary>
        public long UncompressedSize { get; set; }

        /// <summary>
        /// Compression ratio (0.0 to 1.0)
        /// </summary>
        public double CompressionRatio => UncompressedSize > 0 ? (double)CompressedSize / UncompressedSize : 0.0;

        /// <summary>
        /// Additional compression-specific data
        /// </summary>
        public string? AdditionalInfo { get; set; }
    }

    /// <summary>
    /// Types of compression supported
    /// </summary>
    public enum CompressionType
    {
        /// <summary>
        /// No compression
        /// </summary>
        None,

        /// <summary>
        /// Oodle compression (used in bundles)
        /// </summary>
        Oodle,

        /// <summary>
        /// Other compression types
        /// </summary>
        Other
    }
}