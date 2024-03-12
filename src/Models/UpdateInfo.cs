namespace LegendaryLibraryNS.Models
{
    public class UpdateInfo
    {
        public string Title { get; set; }
        public string Version { get; set; }
        public double Disk_size { get; set; }
        public double Download_size { get; set; }
        public string Title_for_updater { get; set; }
        public bool Success { get; set; } = true;
    }
}
