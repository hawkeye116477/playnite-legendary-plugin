using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Services;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class LegendaryLibrarySettings
    {
        public bool ImportInstalledGames { get; set; } = LegendaryLauncher.IsInstalled;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public string SelectedLauncherPath { get; set; } = "";
        public string GamesInstallationPath { get; set; } = "";
        public bool LaunchOffline { get; set; } = false;
        public List<string> OnlineList { get; set; } = new List<string>();
        public string PreferredCDN { get; set; } = LegendaryLauncher.DefaultPreferredCDN;
        public bool NoHttps { get; set; } = LegendaryLauncher.DefaultNoHttps;
        public int DoActionAfterDownloadComplete { get; set; } = (int)DownloadCompleteAction.Nothing;
        public bool SyncGameSaves { get; set; } = false;
        public int MaxWorkers { get; set; } = LegendaryLauncher.DefaultMaxWorkers;
        public int MaxSharedMemory { get; set; } = LegendaryLauncher.DefaultMaxSharedMemory;
        public bool EnableReordering { get; set; } = false;
        public int AutoClearCache { get; set; } = (int)ClearCacheTime.Never;
        public long NextClearingTime { get; set; } = 0;
        public bool DisableGameVersionCheck { get; set; } = false;
        public bool DisplayDownloadSpeedInBits { get; set; } = false;
        public bool SyncPlaytime { get; set; } = false;
    }

    public class LegendaryLibrarySettingsViewModel : PluginSettingsViewModel<LegendaryLibrarySettings, LegendaryLibrary>
    {
        public LegendaryLibrarySettingsViewModel(LegendaryLibrary library, IPlayniteAPI api) : base(library, api)
        {
            Settings = LoadSavedSettings() ?? new LegendaryLibrarySettings();
        }

        public override void EndEdit()
        {
            if (EditingClone.AutoClearCache != Settings.AutoClearCache)
            {
                if (Settings.AutoClearCache != (int)ClearCacheTime.Never)
                {
                    Settings.NextClearingTime = LegendaryLibrary.GetNextClearingTime(Settings.AutoClearCache);
                }
            }
            if (Settings.AutoClearCache == (int)ClearCacheTime.Never)
            {
                Settings.NextClearingTime = 0;
            }
            base.EndEdit();
        }
    }
}
