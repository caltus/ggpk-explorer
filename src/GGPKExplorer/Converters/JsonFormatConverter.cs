using System;
using System.Globalization;
using System.Windows.Data;

namespace GGPKExplorer.Converters
{
    /// <summary>
    /// Converts boolean IsJsonFile value to format display text
    /// </summary>
    public class JsonFormatConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isJsonFile)
            {
                return isJsonFile ? "JSON" : "TEXT";
            }
            return "UNKNOWN";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}