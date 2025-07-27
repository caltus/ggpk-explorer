namespace GGPKExplorer.Models
{
    /// <summary>
    /// Defines the different types of nodes in the GGPK structure
    /// </summary>
    public enum NodeType
    {
        /// <summary>
        /// A directory/folder node
        /// </summary>
        Directory,

        /// <summary>
        /// A regular file node
        /// </summary>
        File,

        /// <summary>
        /// A file within a bundle
        /// </summary>
        BundleFile,

        /// <summary>
        /// A compressed file
        /// </summary>
        CompressedFile
    }
}