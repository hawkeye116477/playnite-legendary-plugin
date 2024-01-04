namespace LegendaryLibraryNS.Models
{
    class LauncherVersion
    {
        public class Rootobject
        {
            public Release_Info release_info { get; set; }
        }

        public class Release_Info
        {
            public string gh_url { get; set; }
            public string name { get; set; }
            public string summary { get; set; }
            public string version { get; set; }
        }
    }
}
