using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class GameSettings
    {
        public bool? LaunchOffline { get; set; }
        public bool? DisableGameVersionCheck { get; set; }
        public List<string> StartupArguments { get; set; }
        public string LanguageCode { get; set; }
        public string OverrideExe { get; set; }
        public bool? AutoSyncSaves { get; set; }
        public string CloudSaveFolder { get; set; }
    }
}
