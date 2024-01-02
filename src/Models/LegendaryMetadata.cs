using System;

namespace LegendaryLibraryNS.Models
{
    class LegendaryMetadata
    {
        public class Rootobject
        {
            public string app_name { get; set; }
            public string app_title { get; set; }
            public Asset_Infos asset_infos { get; set; }
            public string[] base_urls { get; set; }
            public Metadata1 metadata { get; set; }
        }

        public class Asset_Infos
        {
            public Windows Windows { get; set; }
        }

        public class Windows
        {
            public string app_name { get; set; }
            public string asset_id { get; set; }
            public string build_version { get; set; }
            public string catalog_item_id { get; set; }
            public string label_name { get; set; }
            public Metadata metadata { get; set; }
            public string _namespace { get; set; }
        }

        public class Metadata
        {
        }

        public class Metadata1
        {
            public Agegatings ageGatings { get; set; }
            public Category[] categories { get; set; }
            public DateTime creationDate { get; set; }
            public Customattributes customAttributes { get; set; }
            public string description { get; set; }
            public string developer { get; set; }
            public string developerId { get; set; }
            public bool endOfSupport { get; set; }
            public string entitlementName { get; set; }
            public string entitlementType { get; set; }
            public string[] eulaIds { get; set; }
            public string id { get; set; }
            public string itemType { get; set; }
            public Keyimage[] keyImages { get; set; }
            public DateTime lastModifiedDate { get; set; }
            public string _namespace { get; set; }
            public Releaseinfo[] releaseInfo { get; set; }
            public string status { get; set; }
            public string title { get; set; }
            public bool unsearchable { get; set; }
        }

        public class Agegatings
        {
        }

        public class Customattributes
        {
            public Canrunoffline CanRunOffline { get; set; }
            public Canskipkoreanidverification CanSkipKoreanIdVerification { get; set; }
            public Cloudincludelist CloudIncludeList { get; set; }
            public Cloudsavefolder CloudSaveFolder { get; set; }
            public Foldername FolderName { get; set; }
            public Monitorpresence MonitorPresence { get; set; }
            public Presenceid PresenceId { get; set; }
            public Requirementsjson RequirementsJson { get; set; }
            public Useaccesscontrol UseAccessControl { get; set; }
        }

        public class Canrunoffline
        {
            public string type { get; set; }
            public string value { get; set; }
        }

        public class Canskipkoreanidverification
        {
            public string type { get; set; }
            public string value { get; set; }
        }

        public class Cloudincludelist
        {
            public string type { get; set; }
            public string value { get; set; }
        }

        public class Cloudsavefolder
        {
            public string type { get; set; }
            public string value { get; set; }
        }

        public class Foldername
        {
            public string type { get; set; }
            public string value { get; set; }
        }

        public class Monitorpresence
        {
            public string type { get; set; }
            public string value { get; set; }
        }

        public class Presenceid
        {
            public string type { get; set; }
            public string value { get; set; }
        }

        public class Requirementsjson
        {
            public string type { get; set; }
            public string value { get; set; }
        }

        public class Useaccesscontrol
        {
            public string type { get; set; }
            public string value { get; set; }
        }

        public class Category
        {
            public string path { get; set; }
        }

        public class Keyimage
        {
            public int height { get; set; }
            public string md5 { get; set; }
            public int size { get; set; }
            public string type { get; set; }
            public DateTime uploadedDate { get; set; }
            public string url { get; set; }
            public int width { get; set; }
        }

        public class Releaseinfo
        {
            public string appId { get; set; }
            public DateTime dateAdded { get; set; }
            public string id { get; set; }
            public string[] platform { get; set; }
        }

    }
}
