using Playnite.Common;
using Playnite.SDK.Data;
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
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(dataFile), out messagesSettings))
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
            Helpers.SaveJsonSettingsToFile(messagesSettings, "messages");
        }
    }
}
