using LegendaryLibraryNS.Enums;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace LegendaryLibraryNS.Converters
{
    public class DownloadStatusEnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch((int)value)
            {
                case (int)DownloadStatus.Queued:
                    value = ResourceProvider.GetString(LOC.LegendaryDownloadQueued);
                    break;
                case (int)DownloadStatus.Running:
                    value = ResourceProvider.GetString(LOC.LegendaryDownloadRunning);
                    break;
                case (int)DownloadStatus.Canceled:
                    value = ResourceProvider.GetString(LOC.LegendaryDownloadCanceled);
                    break;
                case (int)DownloadStatus.Paused:
                    value = ResourceProvider.GetString(LOC.LegendaryDownloadPaused);
                    break;
                case (int)DownloadStatus.Completed:
                    value = ResourceProvider.GetString(LOC.LegendaryDownloadCompleted);
                    break;
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
