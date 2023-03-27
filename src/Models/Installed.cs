using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryLibraryNS.Models
{
    public class Installed
    {
        public string Key_name { get; set; }
        public string App_name { get; set; }
        public bool Can_run_offline { get; set; }
        public string Executable { get; set; }
        public string Install_path { get; set; }
        public long Install_size { get; set; }
        public bool Is_dlc { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
    }
}
