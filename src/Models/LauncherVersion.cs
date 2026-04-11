using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class LauncherVersion
    {
        public string Tag_name { get; set; }
        public string Html_url { get; set; }
        public List<Asset> Assets { get; set; }
        public class Asset
        {
            public long Size { get; set; }
            public string Digest { get; set; }
            public string Browser_download_url { get; set; }
        }
    }
}
