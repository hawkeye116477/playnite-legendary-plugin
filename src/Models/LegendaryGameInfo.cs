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
            public string Cloud_save_folder { get; set; }
            public bool Is_dlc { get; set; }
            public Owned_Dlc[] Owned_dlc { get; set; }
        }

        public class Owned_Dlc
        {
            public string App_name { get; set; }
            public string Title { get; set; }
            public string Id { get; set; }
        }

        public class Manifest
        {
            public double Disk_size { get; set; }
            public double Download_size { get; set; }
            public string Launch_exe { get; set; }
            public string[] Install_tags { get; set; }
            public Tag_Disk_Size[] Tag_disk_size { get; set; }
            public Tag_Download_Size[] Tag_download_size { get; set; }
        }

        public class Tag_Disk_Size
        {
            public string Tag { get; set; }
            public double Size { get; set; }
            public int Count { get; set; }
        }

        public class Tag_Download_Size
        {
            public string Tag { get; set; }
            public double Size { get; set; }
            public int Count { get; set; }
        }
    }
}
