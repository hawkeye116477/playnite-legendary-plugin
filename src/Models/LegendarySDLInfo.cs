using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class LegendarySDLInfo
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public List<string> Tags { get; set; }
        public bool Is_dlc { get; set; } = false;
    }
}
