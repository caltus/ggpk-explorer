using System.ComponentModel;

namespace GGPKExplorer.Models
{
    /// <summary>
    /// Settings for controlling the view appearance and behavior
    /// </summary>
    public class ViewSettings
    {
        /// <summary>
        /// Current view mode for the file list
        /// </summary>
        public ViewMode ViewMode { get; set; } = ViewMode.Details;

        /// <summary>
        /// Whether to show hidden files
        /// </summary>
        public bool ShowHiddenFiles { get; set; } = false;

        /// <summary>
        /// Whether to show file extensions
        /// </summary>
        public bool ShowFileExtensions { get; set; } = true;

        /// <summary>
        /// Settings for sorting the file list
        /// </summary>
        public SortSettings SortSettings { get; set; } = new();

        /// <summary>
        /// Settings for column display and sizing
        /// </summary>
        public ColumnSettings ColumnSettings { get; set; } = new();

        /// <summary>
        /// Whether to show compression information
        /// </summary>
        public bool ShowCompressionInfo { get; set; } = true;

        /// <summary>
        /// Whether to show file hashes
        /// </summary>
        public bool ShowFileHashes { get; set; } = false;

        /// <summary>
        /// Whether to show file offsets
        /// </summary>
        public bool ShowFileOffsets { get; set; } = false;
    }

    /// <summary>
    /// Available view modes for the file list
    /// </summary>
    public enum ViewMode
    {
        /// <summary>
        /// Detailed list with columns
        /// </summary>
        Details,

        /// <summary>
        /// Large icons with file names
        /// </summary>
        LargeIcons,

        /// <summary>
        /// Small icons with file names
        /// </summary>
        SmallIcons,

        /// <summary>
        /// Simple list of file names
        /// </summary>
        List
    }

    /// <summary>
    /// Settings for sorting the file list
    /// </summary>
    public class SortSettings
    {
        /// <summary>
        /// Column to sort by
        /// </summary>
        public string SortColumn { get; set; } = "Name";

        /// <summary>
        /// Direction of sorting
        /// </summary>
        public ListSortDirection SortDirection { get; set; } = ListSortDirection.Ascending;

        /// <summary>
        /// Whether directories should be sorted before files
        /// </summary>
        public bool DirectoriesFirst { get; set; } = true;
    }

    /// <summary>
    /// Settings for column display and sizing
    /// </summary>
    public class ColumnSettings
    {
        /// <summary>
        /// Width of the Name column
        /// </summary>
        public double NameColumnWidth { get; set; } = 200;

        /// <summary>
        /// Width of the Size column
        /// </summary>
        public double SizeColumnWidth { get; set; } = 100;

        /// <summary>
        /// Width of the Type column
        /// </summary>
        public double TypeColumnWidth { get; set; } = 120;

        /// <summary>
        /// Width of the Modified column
        /// </summary>
        public double ModifiedColumnWidth { get; set; } = 150;

        /// <summary>
        /// Width of the Compression column
        /// </summary>
        public double CompressionColumnWidth { get; set; } = 100;

        /// <summary>
        /// Whether the Name column is visible
        /// </summary>
        public bool ShowNameColumn { get; set; } = true;

        /// <summary>
        /// Whether the Size column is visible
        /// </summary>
        public bool ShowSizeColumn { get; set; } = true;

        /// <summary>
        /// Whether the Type column is visible
        /// </summary>
        public bool ShowTypeColumn { get; set; } = true;

        /// <summary>
        /// Whether the Modified column is visible
        /// </summary>
        public bool ShowModifiedColumn { get; set; } = true;

        /// <summary>
        /// Whether the Compression column is visible
        /// </summary>
        public bool ShowCompressionColumn { get; set; } = true;
    }
}