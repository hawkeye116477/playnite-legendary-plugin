using System;
using System.Globalization;
using System.Windows.Data;

namespace LegendaryLibraryNS.Converters
{
    public class LongToDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is long seconds))
                return value;
            DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            if (seconds == 0)
            {
                return "";
            }
            return dateTime.AddSeconds(seconds).ToLocalTime();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return new NotImplementedException();
        }
    }
}
