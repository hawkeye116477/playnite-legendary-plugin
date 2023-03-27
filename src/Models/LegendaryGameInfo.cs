using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryLibraryNS.Models
{
    public class LegendaryGameInfo
    {
        public class Rootobject
        {
            public Game Game { get; set; }
            public Manifest Manifest { get; set; }
        }

        public class Game
        {
            public string App_name { get; set; }
            public string Title { get; set; }
            public string Version { get; set; }
            public bool Cloud_saves_supported { get; set; }
            public bool Is_dlc { get; set; }
        }

        public class Manifest
        {
            public double Disk_size { get; set; }
            public double Download_size { get; set; }
            public string Launch_exe { get; set; }
        }

    }
}
