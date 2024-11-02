using System;
using System.Globalization;
using System.Windows.Data;

namespace CommonPlugin.Converters
{
    public class NumericalSizeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is double bytes))
                return value;
            return CommonHelpers.FormatSize(bytes);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotImplementedException();
        }
    }
}
