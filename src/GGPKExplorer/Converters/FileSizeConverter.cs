using System;
using System.Globalization;
using System.Windows.Data;

namespace GGPKExplorer.Converters
{
    /// <summary>
    /// Converts file size in bytes to human-readable format
    /// </summary>
    public class FileSizeConverter : IValueConverter
    {
        private static readonly string[] SizeSuffixes = { "B", "KB", "MB", "GB", "TB" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            long size;
            
            // Handle different numeric types
            switch (value)
            {
                case long longValue:
                    size = longValue;
                    break;
                case int intValue:
                    size = intValue;
                    break;
                case string stringValue when long.TryParse(stringValue, out var parsedValue):
                    size = parsedValue;
                    break;
                default:
                    return System.Windows.DependencyProperty.UnsetValue;
            }

            // Handle negative values
            if (size < 0)
                return "0 B";

            if (size == 0)
                return "0 B";

            int suffixIndex = 0;
            double doubleSize = size;

            while (doubleSize >= 1024 && suffixIndex < SizeSuffixes.Length - 1)
            {
                doubleSize /= 1024;
                suffixIndex++;
            }

            // Format based on size - show no decimals for bytes, 1 decimal for larger units
            if (suffixIndex == 0)
                return $"{(long)doubleSize} {SizeSuffixes[suffixIndex]}";
            else
                return $"{doubleSize:F1} {SizeSuffixes[suffixIndex]}";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}