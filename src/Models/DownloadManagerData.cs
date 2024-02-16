using LegendaryLibraryNS.Enums;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LegendaryLibraryNS.Models
{
    public class DownloadManagerData
    {
        public class Rootobject
        {
            public ObservableCollection<Download> downloads { get; set; }
        }

        public class Download : ObservableObject
        {
            public string gameID { get; set; }
            public string name { get; set; }
            public string fullInstallPath { get; set; }

            private string _downloadSize;
            public string downloadSize
            {
                get => _downloadSize;
                set => SetValue(ref _downloadSize, value);
            }
            private string _installSize;
            public string installSize
            {
                get => _installSize;
                set => SetValue(ref _installSize, value);
            }

            public long addedTime { get; set; }

            private long _completedTime;
            public long completedTime
            {
                get => _completedTime;
                set => SetValue(ref _completedTime, value);
            }

            private DownloadStatus _status;
            public DownloadStatus status
            {
                get => _status;
                set => SetValue(ref _status, value);
            }

            public DownloadProperties downloadProperties { get; set; }
        }
    }

    public class DownloadProperties : ObservableObject
    {
        public string installPath { get; set; } = "";
        public DownloadAction downloadAction { get; set; }
        public bool installPrerequisites { get; set; }
        public string prerequisitesName { get; set; }
        public bool enableReordering { get; set; }
        public int maxWorkers { get; set; }
        public int maxSharedMemory { get; set; }
        public List<string> extraContent { get; set; } = default;
    }
}
