namespace LegendaryLibraryNS.Models
{
    public class UpdateInfo
    {
        public string Title { get; set; }
        public string Version { get; set; }
        public string Install_path { get; set; }
        public double Disk_size { get; set; } = 0;
        public double Download_size { get; set; } = 0;
        public string Title_for_updater { get; set; }
        public bool Success { get; set; } = true;
    }
}
