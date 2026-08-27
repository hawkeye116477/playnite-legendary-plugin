using System;
using CommunityToolkit.Mvvm.Input;

namespace Playnite.Commands
{
    public static class Commands
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public static RelayCommand<object> OpenUrlCommand { get; } = new RelayCommand<object>(OpenUrl);

        public static void OpenUrl(object? url)
        {
            try
            {
                if (url is WebLink link)
                {
                    ProcessStarter.StartUrl(link)?.Dispose();
                }
                else if (url is ImportableWebLink importLink)
                {
                    ProcessStarter.StartUrl(importLink.Url)?.Dispose();
                }
                else if (url is Uri uri)
                {
                    ProcessStarter.StartUrl(uri)?.Dispose();
                }
                else if (url is string strUrl)
                {
                    ProcessStarter.StartUrl(strUrl)?.Dispose();
                }
                else
                {
                    throw new NotSupportedException("Unsupported URL type.");
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Failed to open url.");
            }
        }
    }
}
