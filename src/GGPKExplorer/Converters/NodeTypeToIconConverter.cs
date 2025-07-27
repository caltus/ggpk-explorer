using System;
using System.Globalization;
using System.Windows.Data;
using GGPKExplorer.Models;

namespace GGPKExplorer.Converters
{
    /// <summary>
    /// Converts NodeType to appropriate icon string for WPF-UI SymbolIcon
    /// </summary>
    public class NodeTypeToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not NodeType nodeType)
                return "Document24";

            return nodeType switch
            {
                NodeType.Directory => "Folder24",
                NodeType.File => "Document24",
                NodeType.BundleFile => "Archive24",
                NodeType.CompressedFile => "DocumentZip24",
                _ => "Document24"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


}