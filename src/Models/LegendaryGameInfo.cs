using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class LegendaryGameInfo
    {
        public class Rootobject
        {
            public Game? Game { get; set; }
            public Manifest? Manifest { get; set; }
            public bool ErrorDisplayed = false;
        }

        public class Game
        {
            public string App_name { get; set; } = "";
            public string Title { get; set; } = "";
            public string Version { get; set; } = "";
            public bool Cloud_saves_supported { get; set; }
            public string? Cloud_save_folder { get; set; }
            public bool Is_dlc { get; set; }
            public List<OwnedDlc> Owned_dlc { get; set; } = [];
            public string? External_activation { get; set; }
        }

        public class OwnedDlc
        {
            public string App_name { get; set; } = "";
            public string Title { get; set; } = "";
            public string Id { get; set; } = "";
        }

        public class Manifest
        {
            public double Disk_size { get; set; }
            public double Download_size { get; set; }
            public string Launch_exe { get; set; } = "";
            public List<string>? Install_tags { get; set; } = [];
            public TagDiskSize[]? Tag_disk_size { get; set; }
            public TagDownloadSize[]? Tag_download_size { get; set; }
            public Prerequisite? Prerequisites { get; set; }
        }

        public class TagDiskSize
        {
            public string Tag { get; set; } = "";
            public double Size { get; set; }
            public int Count { get; set; }
        }

        public class TagDownloadSize
        {
            public string Tag { get; set; } = "";
            public double Size { get; set; }
            public int Count { get; set; }
        }
    }
}
