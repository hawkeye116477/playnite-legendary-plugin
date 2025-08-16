using CommonPlugin;
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
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadQueued);
                    break;
                case DownloadStatus.Running:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadRunning);
                    break;
                case DownloadStatus.Canceled:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadCanceled);
                    break;
                case DownloadStatus.Paused:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadPaused);
                    break;
                case DownloadStatus.Completed:
                    value = LocalizationManager.Instance.GetString(LOC.CommonDownloadCompleted);
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
