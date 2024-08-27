using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class LegendarySDLInfo : ObservableObject
    {
        public string Description { get; set; }
        public string Name { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public bool Is_dlc { get; set; } = false;
        public string BaseGameID { get; set; } = "";
    }
}
