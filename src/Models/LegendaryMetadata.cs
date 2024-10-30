using System;
using System.Collections.Generic;

namespace LegendaryLibraryNS.Models
{
    public class LegendaryMetadata
    {
        public string app_name { get; set; }
        public string app_title { get; set; }
        public Dictionary<string, AssetInfo> asset_infos;
        public string[] base_urls;
        public Metadata metadata;

        public class CustomAttribute
        {
            public string type;
            public string value;
        }

        public class AssetInfo
        {
            public string app_name;
            public string asset_id;
            public string build_version;
            public string catalog_item_id;
            public string label_name;
            public Metadata metadata;
            public string _namespace;
        }

        public class CustomAttributeType
        {
            public CustomAttribute AdditionalCommandline;
            public CustomAttribute CanRunOffline;
            public CustomAttribute CanSkipKoreanIdVerification;
            public CustomAttribute CloudIncludeList;
            public CustomAttribute CloudSaveFolder;
            public CustomAttribute FolderName;
            public CustomAttribute MonitorPresence;
            public CustomAttribute PresenceId;
            public CustomAttribute RequirementsJson;
            public CustomAttribute ThirdPartyManagedApp;
            public CustomAttribute UseAccessControl;
        }

        public class Metadata
        {
            public Agegatings ageGatings;
            public string applicationId;
            public Category[] categories;
            public DateTime creationDate;
            public CustomAttributeType customAttributes;
            public string description;
            public string developer;
            public string developerId;
            public bool endOfSupport;
            public string entitlementName;
            public string entitlementType;
            public string[] eulaIds;
            public string id;
            public string itemType;
            public Keyimage[] keyImages;
            public DateTime lastModifiedDate;
            public Metadata mainGameItem;
            public string _namespace;
            public Releaseinfo[] releaseInfo;
            public string status;
            public string title;
            public bool unsearchable;
        }

        public class Agegatings
        {
        }

        public class Category
        {
            public string path;
        }

        public class Keyimage
        {
            public int height;
            public string md5;
            public int size;
            public string type;
            public DateTime uploadedDate;
            public string url;
            public int width;
        }

        public class Releaseinfo
        {
            public string appId;
            public DateTime dateAdded;
            public string id;
            public string[] platform;
        }

    }
}
