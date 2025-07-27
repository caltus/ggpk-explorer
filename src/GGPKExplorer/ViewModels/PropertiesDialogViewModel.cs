using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GGPKExplorer.Helpers;
using GGPKExplorer.Models;

namespace GGPKExplorer.ViewModels
{
    /// <summary>
    /// View model for the properties dialog displaying file and directory metadata
    /// </summary>
    public partial class PropertiesDialogViewModel : ObservableObject
    {
        private readonly TreeNodeInfo _nodeInfo;

        /// <summary>
        /// Initializes a new instance of the PropertiesDialogViewModel for a tree node
        /// </summary>
        /// <param name="nodeInfo">The tree node to display properties for</param>
        public PropertiesDialogViewModel(TreeNodeInfo nodeInfo)
        {
            _nodeInfo = nodeInfo ?? throw new ArgumentNullException(nameof(nodeInfo));
            InitializeProperties();
        }

        #region Properties

        [ObservableProperty]
        private string name = string.Empty;

        [ObservableProperty]
        private string fullPath = string.Empty;

        [ObservableProperty]
        private string typeDescription = string.Empty;

        [ObservableProperty]
        private string sizeFormatted = string.Empty;

        [ObservableProperty]
        private string modifiedDateFormatted = string.Empty;

        [ObservableProperty]
        private bool hasModifiedDate;

        [ObservableProperty]
        private string? hash;

        [ObservableProperty]
        private bool hasHash;

        [ObservableProperty]
        private string offsetFormatted = string.Empty;

        [ObservableProperty]
        private bool hasOffset;

        [ObservableProperty]
        private bool hasCompressionInfo;

        [ObservableProperty]
        private string compressionType = string.Empty;

        [ObservableProperty]
        private string compressedSizeFormatted = string.Empty;

        [ObservableProperty]
        private string uncompressedSizeFormatted = string.Empty;

        [ObservableProperty]
        private string compressionRatioFormatted = string.Empty;

        [ObservableProperty]
        private string additionalCompressionInfo = string.Empty;

        [ObservableProperty]
        private bool hasAdditionalCompressionInfo;

        [ObservableProperty]
        private bool isBundleFile;

        [ObservableProperty]
        private string bundleName = string.Empty;

        [ObservableProperty]
        private string bundleIndex = string.Empty;

        #endregion

        /// <summary>
        /// Command to copy a property value to the clipboard
        /// </summary>
        [RelayCommand]
        private void CopyToClipboard(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                try
                {
                    Clipboard.SetText(value);
                }
                catch (Exception)
                {
                    // Silently handle clipboard errors
                    // In a production app, you might want to show a notification
                }
            }
        }

        /// <summary>
        /// Initializes all properties based on the provided node info
        /// </summary>
        private void InitializeProperties()
        {
            InitializeFromNodeInfo(_nodeInfo);
        }



        /// <summary>
        /// Initializes properties from a TreeNodeInfo
        /// </summary>
        /// <param name="nodeInfo">The tree node to extract properties from</param>
        private void InitializeFromNodeInfo(TreeNodeInfo nodeInfo)
        {
            Name = nodeInfo.Name;
            FullPath = nodeInfo.FullPath;
            TypeDescription = GetTypeDescription(nodeInfo.Type);
            SizeFormatted = FileSizeHelper.FormatFileSize(nodeInfo.Size);

            // Modified date
            if (nodeInfo.ModifiedDate.HasValue)
            {
                ModifiedDateFormatted = nodeInfo.ModifiedDate.Value.ToString("F", CultureInfo.CurrentCulture);
                HasModifiedDate = true;
            }

            // Hash
            if (!string.IsNullOrEmpty(nodeInfo.Hash))
            {
                Hash = nodeInfo.Hash;
                HasHash = true;
            }

            // Offset
            if (nodeInfo.Offset.HasValue)
            {
                OffsetFormatted = $"0x{nodeInfo.Offset.Value:X8} ({nodeInfo.Offset.Value:N0} bytes)";
                HasOffset = true;
            }

            // Bundle file information
            IsBundleFile = nodeInfo.Type == NodeType.BundleFile;
            if (IsBundleFile)
            {
                // Extract bundle information from the full path
                var pathParts = nodeInfo.FullPath.Split('/');
                if (pathParts.Length > 1)
                {
                    BundleName = pathParts[0];
                    BundleIndex = string.Join("/", pathParts.Skip(1));
                }
            }

            // Compression information
            if (nodeInfo.Compression != null)
            {
                InitializeCompressionFromInfo(nodeInfo.Compression);
            }
        }

        /// <summary>
        /// Initializes compression information from a compression info string
        /// </summary>
        /// <param name="compressionInfo">The compression info string</param>
        private void InitializeCompressionInfo(string compressionInfo)
        {
            if (string.IsNullOrEmpty(compressionInfo) || compressionInfo == "None")
            {
                HasCompressionInfo = false;
                return;
            }

            HasCompressionInfo = true;
            CompressionType = compressionInfo;

            // For simple compression info strings, we can't extract detailed information
            // This would need to be enhanced based on the actual format of the compression info
            CompressedSizeFormatted = "N/A";
            UncompressedSizeFormatted = "N/A";
            CompressionRatioFormatted = "N/A";
        }

        /// <summary>
        /// Initializes compression information from a CompressionInfo object
        /// </summary>
        /// <param name="compression">The compression info object</param>
        private void InitializeCompressionFromInfo(CompressionInfo compression)
        {
            if (compression.Type == Models.CompressionType.None)
            {
                HasCompressionInfo = false;
                return;
            }

            HasCompressionInfo = true;
            CompressionType = compression.Type.ToString();
            CompressedSizeFormatted = FileSizeHelper.FormatFileSize(compression.CompressedSize);
            UncompressedSizeFormatted = FileSizeHelper.FormatFileSize(compression.UncompressedSize);
            CompressionRatioFormatted = $"{compression.CompressionRatio:P1}";

            if (!string.IsNullOrEmpty(compression.AdditionalInfo))
            {
                AdditionalCompressionInfo = compression.AdditionalInfo;
                HasAdditionalCompressionInfo = true;
            }
        }

        /// <summary>
        /// Gets a human-readable description for a node type
        /// </summary>
        /// <param name="nodeType">The node type</param>
        /// <returns>Human-readable description</returns>
        private static string GetTypeDescription(NodeType nodeType)
        {
            return nodeType switch
            {
                NodeType.Directory => "Directory",
                NodeType.File => "File",
                NodeType.BundleFile => "Bundle File",
                NodeType.CompressedFile => "Compressed File",
                _ => "Unknown"
            };
        }
    }
}