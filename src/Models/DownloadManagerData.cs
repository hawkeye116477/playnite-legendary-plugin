using CommonPlugin.Enums;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace LegendaryLibraryNS.Models
{
    public partial class DownloadManagerData  : ObservableObject
    {
        public ObservableCollection<Download>? Downloads { get; set; }

        public partial class Download : ObservableObject
        {
            public string GameId { get; set; } = "";
            public string Name { get; set; } = "";
            public string FullInstallPath { get; set; } = "";

            [ObservableProperty]
            [field: JsonIgnore]
            public partial double DownloadSizeNumber { get; set; }

            [ObservableProperty]
            [field: JsonIgnore]
            public partial double InstallSizeNumber { get; set; }

            [ObservableProperty]
            [field: JsonIgnore]
            public partial long AddedTime { get; set; }

            [ObservableProperty]
            [field: JsonIgnore]
            public partial long CompletedTime { get; set; }

            [ObservableProperty]
            [field: JsonIgnore]
            public partial DownloadStatus Status { get; set; }

            [ObservableProperty]
            [field: JsonIgnore]
            public partial double Progress { get; set; }

            [ObservableProperty]
            [field: JsonIgnore]
            public partial double DownloadedNumber { get; set; }

            public DownloadProperties DownloadProperties { get; set; } = new();
            public bool? ExtraContentAvailable { get; set; }
        }
    }

    public class DownloadProperties : ObservableObject
    {
        public string InstallPath { get; set; } = "";
        public DownloadAction DownloadAction { get; set; }
        public bool InstallPrerequisites { get; set; } = false;
        public string PrerequisitesName { get; set; } = "";
        public bool IgnoreFreeSpace { get; set; } = false;
        public bool EnableReordering { get; set; }
        public int MaxWorkers { get; set; }
        public int MaxSharedMemory { get; set; }
        public List<string>? ExtraContent { get; set; }
        public Dictionary<string, DownloadManagerData.Download>? SelectedDlcs { get; set; }
    }
}
