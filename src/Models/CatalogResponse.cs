using System;
using System.Collections.Generic;

// ReSharper disable UnassignedField.Global
// ReSharper disable ClassNeverInstantiated.Global

namespace LegendaryLibraryNS.Models;

public class CatalogItem
{
    public class CustomAttribute
    {
        public string Type { get; set; } = "";
        public string Value { get; set; } = "";
    }

    public class Image
    {
        public int Height;
        public string? Md5;
        public int Size;
        public string? Type;
        public DateTime UploadedDate;
        public string? Url;
        public int Width;
    }

    public class Category
    {
        public string? Path;
    }

    public class ReleaseInfoModel
    {
        public string? AppId;
        public List<string?> Platform = [];
        public DateTime? DateAdded;
    }

    public class CustomAttributeType
    {
        public CustomAttribute? CanRunOffline;
        public CustomAttribute? CanSkipKoreanIdVerification;
        public CustomAttribute? CloudIncludeList;
        public CustomAttribute? CloudSaveFolder;
        public CustomAttribute? FolderName;
        public CustomAttribute? PartnerLinkType;
        public CustomAttribute? ThirdPartyManagedApp;
    }

    public class MainGameItemModel
    {
        public string? Id;
    }

    public string? Id;
    public string? Title;
    public string? Description;
    public List<Image>? KeyImages;
    public List<Category>? Categories;
    public string? Namespace;
    public string? Status;
    public DateTime? CreationDate;
    public DateTime? LastModifiedDate;
    public CustomAttributeType? CustomAttributes;
    public string? EntitlementName;
    public string? EntitlementType;
    public string? ItemType;
    public List<ReleaseInfoModel>? ReleaseInfo;
    public string? Developer;
    public string? DeveloperId;
    public bool EndOfSupport;
    public MainGameItemModel? MainGameItem;
}