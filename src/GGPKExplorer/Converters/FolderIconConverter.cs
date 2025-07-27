using System;
using System.Globalization;
using System.Windows.Data;
using GGPKExplorer.Models;
using Wpf.Ui.Controls;

namespace GGPKExplorer.Converters
{
    /// <summary>
    /// Converter that returns appropriate folder icons based on node type and expansion state
    /// </summary>
    public class FolderIconConverter : IMultiValueConverter
    {
        public static readonly FolderIconConverter Instance = new();

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length >= 2 && values[0] is NodeType nodeType)
            {
                bool isExpanded = values[1] is bool expanded && expanded;

                return nodeType switch
                {
                    NodeType.Directory => isExpanded ? SymbolRegular.FolderOpen24 : SymbolRegular.Folder24,
                    NodeType.BundleFile => SymbolRegular.Archive24,
                    NodeType.CompressedFile => SymbolRegular.Archive24,
                    NodeType.File => SymbolRegular.Document24,
                    _ => SymbolRegular.Document24
                };
            }

            return SymbolRegular.Folder24;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}