using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LegendaryLibraryNS.Models
{
    public class LegendarySdlInfo : ObservableObject
    {
        public string? Description { get; set; }
        public string? Name { get; set; }
        public List<string> Tags { get; set; } = [];
        public bool Is_dlc { get; set; } = false;
        public string BaseGameID { get; set; } = "";
    }
}
