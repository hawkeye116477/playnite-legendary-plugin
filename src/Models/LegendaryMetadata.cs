using System;
using System.Collections.Generic;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UnassignedField.Global

namespace LegendaryLibraryNS.Models;

public class LegendaryMetadata
{
    public string App_name { get; set; } = "";
    public string App_title { get; set; } = "";
    public Dictionary<string, AssetInfo>? Asset_infos;
    public string[]? Base_urls;
    public MetadataModel? Metadata;

    public class CustomAttribute
    {
        public string? Type;
        public string? Value;
    }

    public class AssetInfo
    {
        public string? App_name { get; set; }
        public string? Asset_id { get; set; }
        public string? Build_version { get; set; }
        public string? Catalog_item_id { get; set; }
        public string? Label_name { get; set; }
        public MetadataModel? MetadataModel { get; set; }
        public string? Namespace { get; set; }
    }

    public class CustomAttributeType
    {
        public CustomAttribute? AdditionalCommandline;
        public CustomAttribute? CanRunOffline;
        public CustomAttribute? CanSkipKoreanIdVerification;
        public CustomAttribute? CloudIncludeList;
        public CustomAttribute? CloudSaveFolder;
        public CustomAttribute? FolderName;
        public CustomAttribute? MonitorPresence;
        public CustomAttribute? PresenceId;
        public CustomAttribute? RequirementsJson;
        public CustomAttribute? ThirdPartyManagedApp;
        public CustomAttribute? UseAccessControl;
    }

    public class MetadataModel
    {
        public Agegatings? AgeGatings;
        public string? ApplicationId;
        public Category[]? Categories;
        public DateTime CreationDate;
        public CustomAttributeType? CustomAttributes;
        public string? Description;
        public string? Developer;
        public string? DeveloperId;
        public bool EndOfSupport;
        public string? EntitlementName;
        public string? EntitlementType;
        public string[]? EulaIds;
        public string? Id;
        public string? ItemType;
        public Keyimage[]? KeyImages;
        public DateTime LastModifiedDate;
        public MetadataModel? MainGameItem;
        public string? Namespace;
        public Releaseinfo[]? ReleaseInfo;
        public string? Status;
        public string? Title;
        public bool Unsearchable;
    }

    public class Agegatings
    {
    }

    public class Category
    {
        public string? Path;
    }

    public class Keyimage
    {
        public int Height;
        public string? Md5;
        public int Size;
        public string? Type;
        public DateTime UploadedDate;
        public string? Url;
        public int Width;
    }

    public class Releaseinfo
    {
        public string? AppId;
        public DateTime DateAdded;
        public string? Id;
        public string[]? Platform;
    }
}