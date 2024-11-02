using CommonPlugin.Enums;
using Playnite.SDK;
using System;
using System.Globalization;
using System.Windows.Data;

namespace LegendaryLibraryNS.Converters
{
    public class DownloadStatusEnumToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch(value)
            {
                case DownloadStatus.Queued:
                    value = ResourceProvider.GetString(LOC.LegendaryDownloadQueued);
                    break;
                case DownloadStatus.Running:
                    value = ResourceProvider.GetString(LOC.LegendaryDownloadRunning);
                    break;
                case DownloadStatus.Canceled:
                    value = ResourceProvider.GetString(LOC.LegendaryDownloadCanceled);
                    break;
                case DownloadStatus.Paused:
                    value = ResourceProvider.GetString(LOC.LegendaryDownloadPaused);
                    break;
                case DownloadStatus.Completed:
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
