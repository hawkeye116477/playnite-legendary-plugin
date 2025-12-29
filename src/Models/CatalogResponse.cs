using System;
using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class CatalogItem
    {
        public class CustomAttribute
        {
            public string type;
            public string value;
        }

        public class Image
        {
            public int height { get; set; }
            public string md5 { get; set; }
            public int size { get; set; }
            public string type { get; set; }
            public DateTime uploadedDate { get; set; }
            public string url { get; set; }
            public int width { get; set; }
        }

        public class Category
        {
            public string path;
        }

        public class ReleaseInfo
        {
            public string appId;
            public List<string> platform;
            public DateTime? dateAdded;
        }

        public class CustomAttributeType
        {
            public CustomAttribute CanRunOffline;
            public CustomAttribute CanSkipKoreanIdVerification;
            public CustomAttribute CloudIncludeList;
            public CustomAttribute CloudSaveFolder;
            public CustomAttribute FolderName;
            public CustomAttribute PartnerLinkType;
            public CustomAttribute ThirdPartyManagedApp;
        }

        public class MainGameItem
        {
            public string id;
        }

        public string id;
        public string title;
        public string description;
        public List<Image> keyImages;
        public List<Category> categories;
        public string @namespace;
        public string status;
        public DateTime? creationDate;
        public DateTime? lastModifiedDate;
        public CustomAttributeType customAttributes;
        public string entitlementName;
        public string entitlementType;
        public string itemType;
        public List<ReleaseInfo> releaseInfo;
        public string developer;
        public string developerId;
        public bool endOfSupport;
        public MainGameItem mainGameItem;
    }
}
