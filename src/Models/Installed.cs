using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class Installed
    {
        public string App_name { get; set; }
        public bool Can_run_offline { get; set; }
        public string Executable { get; set; }
        public string Install_path { get; set; }
        public long Install_size { get; set; }
        public bool Is_dlc { get; set; } = false;
        public string Title { get; set; }
        public string Version { get; set; }
        public string Save_path { get; set; }
        public string Title_for_updater { get; set; } = "";
        public Prerequisite Prereq_info { get; set; }
        public List<string> Install_tags { get; set; } = new List<string>();
    }
}
