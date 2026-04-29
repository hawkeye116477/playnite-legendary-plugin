using CommonPlugin;
using CommonPlugin.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using LegendaryLibraryNS.Enums;
using Playnite;
using Playnite.Common;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace LegendaryLibraryNS
{
    public partial class LegendaryLibrarySettings : ObservableObject
    {
        [ObservableProperty] public partial bool ImportInstalledGames { get; set; } = LegendaryLauncher.IsInstalled;

        [ObservableProperty] public partial bool ConnectAccount { get; set; } = false;

        [ObservableProperty] public partial bool ImportUninstalledGames { get; set; } = false;

        [ObservableProperty] public partial bool ImportUbisoftLauncherGames { get; set; } = false;

        [ObservableProperty] public partial bool ImportEaLauncherGames { get; set; } = false;

        [ObservableProperty] public partial string SelectedFullLauncherPath { get; set; } = "";

        [ObservableProperty] public partial string GamesInstallationPath { get; set; } = "";

        [ObservableProperty] public partial bool LaunchOffline { get; set; } = false;

        [ObservableProperty] public partial string PreferredCdn { get; set; } = "";

        [ObservableProperty] public partial bool NoHttps { get; set; } = false;

        [ObservableProperty] public partial bool SyncGameSaves { get; set; } = false;

        [ObservableProperty] public partial int MaxWorkers { get; set; } = 0;

        [ObservableProperty] public partial int MaxSharedMemory { get; set; } = 0;

        [ObservableProperty] public partial int ConnectionTimeout { get; set; } = 0;

        [ObservableProperty] public partial bool EnableReordering { get; set; } = false;

        [ObservableProperty] public partial ClearCacheTime AutoClearCache { get; set; } = ClearCacheTime.Never;

        [ObservableProperty] public partial long NextClearingTime { get; set; } = 0;

        [ObservableProperty] public partial bool UnattendedInstall { get; set; } = false;

        [ObservableProperty] public partial bool DownloadAllDlcs { get; set; } = false;

        [ObservableProperty]
        public partial bool SyncPlaytime { get; set; } = LegendaryLauncher.DefaultPlaytimeSyncEnabled;

        [ObservableProperty]
        public partial string SyncPlaytimeMachineId { get; set; } = System.Guid.NewGuid().ToString("N");

        [ObservableProperty] public partial UpdatePolicy GamesUpdatePolicy { get; set; } = UpdatePolicy.Month;

        [ObservableProperty] public partial long NextGamesUpdateTime { get; set; } = 0;

        [ObservableProperty] public partial bool AutoUpdateGames { get; set; } = false;

        [ObservableProperty] public partial UpdatePolicy LauncherUpdatePolicy { get; set; } = UpdatePolicy.Never;

        [ObservableProperty] public partial long NextLauncherUpdateTime { get; set; } = 0;

        [ObservableProperty]
        public partial string LauncherUpdateSource { get; set; } = LegendaryLauncher.DefaultUpdateSource;
    }

    [INotifyPropertyChanged]
    public partial class LegendaryLibrarySettingsViewModel(LegendaryLibrary plugin) : PluginSettingsHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        [ObservableProperty] public partial LegendaryLibrarySettings? Settings { get; set; } = null;

        public override FrameworkElement GetEditView(GetSettingsViewArgs args)
        {
            return new LegendaryLibrarySettingsView { DataContext = this };
        }

        public static LegendaryLibrarySettings LoadPluginSettings(string dataDir)
        {
            LegendaryLibrarySettings? settings = null;
            var settingsFile = Path.Combine(dataDir, "settings.json");
            if (File.Exists(settingsFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(settingsFile);
                if (!Serialization.TryFromJson(content, out settings))
                {
                    Logger.Error("Failed to load plugin settings.");
                }
            }

            return settings ?? new LegendaryLibrarySettings();
        }

        public override async Task BeginEditAsync(BeginEditArgs args)
        {
            Settings = plugin.Settings.GetClone();
            await Task.CompletedTask;
        }

        public override async Task EndEditAsync(EndEditArgs args)
        {
            if (plugin.Settings!.AutoClearCache != Settings!.AutoClearCache)
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

            if (plugin.Settings.GamesUpdatePolicy != Settings.GamesUpdatePolicy)
            {
                if (Settings.GamesUpdatePolicy != UpdatePolicy.Never)
                {
                    Settings.NextGamesUpdateTime = LegendaryLibrary.GetNextUpdateCheckTime(Settings.GamesUpdatePolicy);
                }
                else
                {
                    Settings.NextGamesUpdateTime = 0;
                }
            }

            if (plugin.Settings.LauncherUpdatePolicy != Settings.LauncherUpdatePolicy)
            {
                if (Settings.LauncherUpdatePolicy != UpdatePolicy.Never)
                {
                    Settings.NextLauncherUpdateTime =
                        LegendaryLibrary.GetNextUpdateCheckTime(Settings.LauncherUpdatePolicy);
                }
                else
                {
                    Settings.NextLauncherUpdateTime = 0;
                }
            }

            plugin.Settings = Settings;
            plugin.SavePluginSettings(Settings);
            await Task.CompletedTask;
        }
    }
}