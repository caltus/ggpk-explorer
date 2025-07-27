using System;
using System.Globalization;
using System.Windows.Data;

namespace GGPKExplorer.Converters
{
    /// <summary>
    /// Converts boolean values to string representations
    /// </summary>
    public class BooleanToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not bool boolValue)
                return string.Empty;

            if (parameter is not string parameterString)
                return boolValue.ToString();

            // Parameter format: "TrueValue|FalseValue"
            var parts = parameterString.Split('|');
            if (parts.Length != 2)
                return boolValue.ToString();

            return boolValue ? parts[0] : parts[1];
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string stringValue)
                return false;

            if (parameter is not string parameterString)
                return bool.TryParse(stringValue, out var result) && result;

            // Parameter format: "TrueValue|FalseValue"
            var parts = parameterString.Split('|');
            if (parts.Length != 2)
                return bool.TryParse(stringValue, out var result) && result;

            return string.Equals(stringValue, parts[0], StringComparison.OrdinalIgnoreCase);
        }
    }
}