using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class LibraryItemsResponse
    {
        public Responsemetadata responseMetadata { get; set; } = new Responsemetadata();
        public List<Asset> records { get; set; }

        public class Responsemetadata
        {
            public string nextCursor { get; set; }
            public string stateToken { get; set; }
        }
    }
}
