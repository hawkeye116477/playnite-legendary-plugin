using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryLibraryNS.Models
{
    public class GameSettings
    {
        public bool? LaunchOffline { get; set; }
        public bool? DisableGameVersionCheck { get; set; }
        public List<string> StartupArguments { get; set; }
        public string LanguageCode { get; set; }
        public string OverrideExe { get; set; }
    }
}
