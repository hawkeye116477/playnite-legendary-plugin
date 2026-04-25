using CommonPlugin;
using CommonPlugin.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using LegendaryLibraryNS.Enums;
using Playnite;
using Playnite.Common;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using UnifiedDownloadManagerApiNS.Interfaces;

namespace LegendaryLibraryNS
{
    public partial class LegendaryLibrarySettings : ObservableObject
    {
        [ObservableProperty] private bool _importInstalledGames = LegendaryLauncher.IsInstalled;
        [ObservableProperty] private bool _connectAccount = false;
        [ObservableProperty] private bool _importUninstalledGames = false;
        [ObservableProperty] private bool _importUbisoftLauncherGames = false;
        [ObservableProperty] private bool _importEALauncherGames = false;
        [ObservableProperty] private string _selectedFullLauncherPath = "";
        [ObservableProperty] private string _gamesInstallationPath = "";
        [ObservableProperty] private bool _launchOffline = false;
        [ObservableProperty] private string _preferredCDN = "";
        [ObservableProperty] private bool _noHttps = false;
        [ObservableProperty] private bool _syncGameSaves = false;
        [ObservableProperty] private int _maxWorkers = 0;
        [ObservableProperty] private int _maxSharedMemory = 0;
        [ObservableProperty] private int _connectionTimeout = 0;
        [ObservableProperty] private bool _enableReordering = false;
        [ObservableProperty] private ClearCacheTime _autoClearCache = ClearCacheTime.Never;
        [ObservableProperty] private long _nextClearingTime = 0;
        [ObservableProperty] private bool _unattendedInstall = false;
        [ObservableProperty] private bool _downloadAllDlcs = false;
        [ObservableProperty] private bool _syncPlaytime = LegendaryLauncher.DefaultPlaytimeSyncEnabled;
        [ObservableProperty] private string _syncPlaytimeMachineId = System.Guid.NewGuid().ToString("N");
        [ObservableProperty] private UpdatePolicy _gamesUpdatePolicy = UpdatePolicy.Month;
        [ObservableProperty] private long _nextGamesUpdateTime = 0;
        [ObservableProperty] private bool _autoUpdateGames = false;
        [ObservableProperty] private UpdatePolicy _launcherUpdatePolicy = UpdatePolicy.Never;
        [ObservableProperty] private long _nextLauncherUpdateTime = 0;
        [ObservableProperty] private string _launcherUpdateSource = LegendaryLauncher.DefaultUpdateSource;
    }

    [INotifyPropertyChanged]
    public partial class LegendaryLibrarySettingsViewModel(LegendaryLibrary plugin) : PluginSettingsHandler
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        [ObservableProperty] private LegendaryLibrarySettings _settings = new();

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
            if (plugin.Settings.AutoClearCache != Settings.AutoClearCache)
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
                    Settings.NextLauncherUpdateTime = LegendaryLibrary.GetNextUpdateCheckTime(Settings.LauncherUpdatePolicy);
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
