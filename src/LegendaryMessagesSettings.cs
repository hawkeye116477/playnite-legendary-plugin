using Playnite.Common;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "messages.json");
            if (!Directory.Exists(dataDir))
            {
                Directory.CreateDirectory(dataDir);
            }
            var strConf = Serialization.ToJson(messagesSettings, true);
            if (!strConf.IsNullOrEmpty())
            {
                File.WriteAllText(dataFile, strConf);
            }
        }
    }
}
