using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System.Collections.Generic;
using System.IO;

namespace LegendaryLibraryNS
{
    public class LegendaryLibrarySettings
    {
        public bool ImportInstalledGames { get; set; } = LegendaryLauncher.IsInstalled;
        public bool ConnectAccount { get; set; } = false;
        public bool ImportUninstalledGames { get; set; } = false;
        public bool ImportUbisoftLauncherGames { get; set; } = false;
        public string SelectedLauncherPath { get; set; } = "";
        public string GamesInstallationPath { get; set; } = "";
        public bool LaunchOffline { get; set; } = false;
        public string PreferredCDN { get; set; } = "";
        public bool NoHttps { get; set; } = false;
        public DownloadCompleteAction DoActionAfterDownloadComplete { get; set; } = DownloadCompleteAction.Nothing;
        public bool SyncGameSaves { get; set; } = false;
        public int MaxWorkers { get; set; } = 0;
        public int MaxSharedMemory { get; set; } = 0;
        public int ConnectionTimeout { get; set; } = 0;
        public bool EnableReordering { get; set; } = false;
        public ClearCacheTime AutoClearCache { get; set; } = ClearCacheTime.Never;
        public long NextClearingTime { get; set; } = 0;
        public bool UnattendedInstall { get; set; } = false;
        public bool DownloadAllDlcs { get; set; } = false;
        public bool DisplayDownloadSpeedInBits { get; set; } = false;
        public bool DisplayDownloadTaskFinishedNotifications { get; set; } = true;
        public bool SyncPlaytime { get; set; } = false;
        public string SyncPlaytimeMachineId { get; set; } = System.Guid.NewGuid().ToString("N");
        public UpdatePolicy GamesUpdatePolicy { get; set; } = UpdatePolicy.GameLaunch;
        public long NextGamesUpdateTime { get; set; } = 0;
        public bool AutoUpdateGames { get; set; } = false;
        public UpdatePolicy LauncherUpdatePolicy { get; set; } = UpdatePolicy.Never;
        public long NextLauncherUpdateTime { get; set; } = 0;
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
                if (Settings.AutoClearCache != ClearCacheTime.Never)
                {
                    Settings.NextClearingTime = LegendaryLibrary.GetNextClearingTime(Settings.AutoClearCache);
                }
                else
                {
                    Settings.NextClearingTime = 0;
                }
            }
            if (EditingClone.GamesUpdatePolicy != Settings.GamesUpdatePolicy)
            {
                if (Settings.GamesUpdatePolicy != UpdatePolicy.Never && Settings.GamesUpdatePolicy != UpdatePolicy.GameLaunch)
                {
                    Settings.NextGamesUpdateTime = LegendaryLibrary.GetNextUpdateCheckTime(Settings.GamesUpdatePolicy);
                }
                else
                {
                    Settings.NextGamesUpdateTime = 0;
                }
            }
            if (EditingClone.LauncherUpdatePolicy != Settings.LauncherUpdatePolicy)
            {
                if (Settings.LauncherUpdatePolicy != UpdatePolicy.Never)
                {
                    Settings.NextLauncherUpdateTime = LegendaryLibrary.GetNextUpdateCheckTime(Settings.LauncherUpdatePolicy);
                }
                else
                {
                    Settings.NextLauncherUpdateTime = 0;
                }
            }
            base.EndEdit();
        }
    }
}
