using System.Collections.Generic;

namespace LegendaryLibraryNS.Models;

public class LibraryItemsResponse
{
    public Responsemetadata? ResponseMetadata { get; set; }
    public List<Asset> Records { get; set; } = [];

    public class Responsemetadata
    {
        public string NextCursor { get; set; } = "";
        public string StateToken { get; set; } = "";
    }
}