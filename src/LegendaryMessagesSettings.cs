using Playnite.Common;
using System;
using System.IO;
using CommonPlugin;

namespace LegendaryLibraryNS
{
    public class LegendaryMessagesSettingsModel
    {
        public bool DontShowDownloadManagerWhatsUpMsg { get; set; } = false;
    }

    public class LegendaryMessagesSettings
    {
        public static LegendaryMessagesSettingsModel LoadSettings()
        {
            LegendaryMessagesSettingsModel messagesSettings = null;
            var dataDir = LegendaryLibrary.PlayniteApi.UserDataDir;
            var dataFile = Path.Combine(dataDir, "messages.json");
            bool correctJson = false;
            if (File.Exists(dataFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(dataFile);
                if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out messagesSettings))
                {
                    correctJson = true;
                }
            }
            if (!correctJson)
            {
                messagesSettings = new LegendaryMessagesSettingsModel { };
            }
            return messagesSettings;
        }

        public static void SaveSettings(LegendaryMessagesSettingsModel messagesSettings)
        {
            var commonHelpers = LegendaryLibrary.Instance.CommonHelpers;
            commonHelpers.SaveJsonSettingsToFile(messagesSettings, "", "messages", true);
        }
    }
}
