using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.SDK;
using System.Collections.Generic;

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
        public List<string> OnlineList { get; set; } = new List<string>();
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
        public bool DisableGameVersionCheck { get; set; } = false;
        public bool DisplayDownloadSpeedInBits { get; set; } = false;
        public bool SyncPlaytime { get; set; } = false;
        public string SyncPlaytimeMachineId { get; set; } = System.Guid.NewGuid().ToString("N");
        public UpdatePolicy GamesUpdatePolicy { get; set; } = UpdatePolicy.GameLaunch;
        public long NextGamesUpdateTime { get; set; } = 0;
        public bool AutoUpdateGames { get; set; } = false;
        public UpdatePolicy LauncherUpdatePolicy { get; set; } = UpdatePolicy.Never;
        public long NextLauncherUpdateTime { get; set; } = 0;
        public bool NotifyNewLauncherVersion { get; set; } = false;
    }

    public class LegendaryLibrarySettingsViewModel : PluginSettingsViewModel<LegendaryLibrarySettings, LegendaryLibrary>
    {
        public LegendaryLibrarySettingsViewModel(LegendaryLibrary library, IPlayniteAPI api) : base(library, api)
        {
            var savedSettings = LoadSavedSettings();
            if (savedSettings != null)
            {
                if (savedSettings.OnlineList.Count > 0)
                {
                    var gamesSettings = LegendaryGameSettingsView.LoadSavedGamesSettings();
                    foreach (var onlineGame in savedSettings.OnlineList)
                    {
                        if (!gamesSettings.ContainsKey(onlineGame))
                        {
                            gamesSettings.Add(onlineGame, new GameSettings());
                        }
                        gamesSettings[onlineGame].LaunchOffline = false;
                    }
                    savedSettings.OnlineList.Clear();
                }
                if (savedSettings.DisableGameVersionCheck)
                {
                    savedSettings.GamesUpdatePolicy = UpdatePolicy.Never;
                    savedSettings.DisableGameVersionCheck = false;
                }
                if (savedSettings.NotifyNewLauncherVersion)
                {
                    savedSettings.LauncherUpdatePolicy = UpdatePolicy.Month;
                    savedSettings.NextLauncherUpdateTime = LegendaryLibrary.GetNextUpdateCheckTime(UpdatePolicy.Month);
                }
                if (savedSettings.GamesUpdatePolicy == UpdatePolicy.Auto)
                {
                    savedSettings.GamesUpdatePolicy = UpdatePolicy.Month;
                    savedSettings.NextGamesUpdateTime = LegendaryLibrary.GetNextUpdateCheckTime(UpdatePolicy.Month);
                }
            }
            else
            {
                savedSettings = new LegendaryLibrarySettings();
            }
            Settings = savedSettings;
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
