using Playnite.Common;
using Playnite.SDK.Data;
using System;
using System.IO;

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
            var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
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
            var commonHelpers = LegendaryLibrary.Instance.commonHelpers;
            commonHelpers.SaveJsonSettingsToFile(messagesSettings, "", "messages", true);
        }
    }
}
