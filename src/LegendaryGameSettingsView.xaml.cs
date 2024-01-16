using LegendaryLibraryNS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Playnite.SDK.Data;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Models;
using LegendaryLibraryNS.Enums;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryGameSettingsView.xaml
    /// </summary>
    public partial class LegendaryGameSettingsView : UserControl
    {
        public Dictionary<string, GameSettings> gamesSettings;
        private Game Game => DataContext as Game;
        public string GameID => Game.GameId;
        private IPlayniteAPI playniteAPI = API.Instance;
        private string cloudPath;

        public LegendaryGameSettingsView()
        {
            InitializeComponent();
            gamesSettings = LoadSavedGamesSettings();
        }

        public static Dictionary<string, GameSettings> LoadSavedGamesSettings()
        {
            var savedGamesSettings = new Dictionary<string, GameSettings>();
            var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "gamesSettings.json");
            bool correctJson = false;
            if (File.Exists(dataFile))
            {
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(dataFile), out savedGamesSettings))
                {
                    if (savedGamesSettings != null)
                    {
                        correctJson = true;
                    }
                }
            }
            if (!correctJson)
            {
                savedGamesSettings = new Dictionary<string, GameSettings>();
            }
            return savedGamesSettings;
        }

        public static GameSettings LoadGameSettings(string gameID)
        {
            var gamesSettings = LoadSavedGamesSettings();
            var gameSettings = new GameSettings();
            if (gamesSettings.ContainsKey(gameID))
            {
                gameSettings = gamesSettings[gameID];
            }
            return gameSettings;
        }


        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var globalSettings = LegendaryLibrary.GetSettings();
            var newGameSettings = new GameSettings();
            if (EnableOfflineModeChk.IsChecked != globalSettings.LaunchOffline)
            {
                newGameSettings.LaunchOffline = EnableOfflineModeChk.IsChecked;
            }
            bool globalDisableUpdates = false;
            if (globalSettings.GamesUpdatePolicy == UpdatePolicy.Never)
            {
                globalDisableUpdates = true;
            }
            if (DisableGameUpdateCheckingChk.IsChecked != globalDisableUpdates)
            {
                newGameSettings.DisableGameVersionCheck = DisableGameUpdateCheckingChk.IsChecked;
            }
            if (StartupArgumentsTxt.Text != "")
            {
                newGameSettings.StartupArguments = StartupArgumentsTxt.Text.Split().ToList();
            }
            if (LanguageCodeTxt.Text != "")
            {
                newGameSettings.LanguageCode = LanguageCodeTxt.Text;
            }
            if (SelectedAlternativeExeTxt.Text != "")
            {
                newGameSettings.OverrideExe = SelectedAlternativeExeTxt.Text;
            }
            if (AutoSyncSavesChk.IsChecked != globalSettings.SyncGameSaves)
            {
                newGameSettings.AutoSyncSaves = AutoSyncSavesChk.IsChecked;
            }
            if (SelectedSavePathTxt.Text != "")
            {
                newGameSettings.CloudSaveFolder = SelectedSavePathTxt.Text;
            }
            if (AutoSyncPlaytimeChk.IsChecked != globalSettings.SyncPlaytime)
            {
                newGameSettings.AutoSyncPlaytime = AutoSyncPlaytimeChk.IsChecked;
            }
            if (newGameSettings.GetType().GetProperties().Any(p => p.GetValue(newGameSettings) != null) || gamesSettings.ContainsKey(GameID))
            {
                if (gamesSettings.ContainsKey(GameID))
                {
                    gamesSettings.Remove(GameID);
                }
                gamesSettings.Add(GameID, newGameSettings);
                var strConf = Serialization.ToJson(gamesSettings, true);
                var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
                var dataFile = Path.Combine(dataDir, "gamesSettings.json");
                File.WriteAllText(dataFile, strConf);
            }
            Window.GetWindow(this).Close();
        }

        private void LegendaryGameSettingsViewUC_Loaded(object sender, RoutedEventArgs e)
        {
            var globalSettings = LegendaryLibrary.GetSettings();
            EnableOfflineModeChk.IsChecked = globalSettings.LaunchOffline;
            if (globalSettings.GamesUpdatePolicy == UpdatePolicy.Never)
            {
                DisableGameUpdateCheckingChk.IsChecked = true;
            }
            AutoSyncSavesChk.IsChecked = globalSettings.SyncGameSaves;
            AutoSyncPlaytimeChk.IsChecked = globalSettings.SyncPlaytime;
            if (gamesSettings.ContainsKey(GameID))
            {
                var gameSettings = gamesSettings[GameID];
                if (gameSettings.LaunchOffline != null)
                {
                    EnableOfflineModeChk.IsChecked = gamesSettings[GameID].LaunchOffline;
                }
                if (gameSettings.DisableGameVersionCheck != null)
                {
                    DisableGameUpdateCheckingChk.IsChecked = gamesSettings[GameID].DisableGameVersionCheck;
                }
                if (gameSettings.StartupArguments != null)
                {
                    StartupArgumentsTxt.Text = string.Join(" ", gamesSettings[GameID].StartupArguments);
                }
                if (gameSettings.LanguageCode != null)
                {
                    LanguageCodeTxt.Text = gamesSettings[GameID].LanguageCode;
                }
                if (gameSettings.OverrideExe != null)
                {
                    SelectedAlternativeExeTxt.Text = gamesSettings[GameID].OverrideExe;
                }
                if (gameSettings.AutoSyncSaves != null)
                {
                    AutoSyncSavesChk.IsChecked = gameSettings.AutoSyncSaves;
                }
                if (!gameSettings.CloudSaveFolder.IsNullOrEmpty())
                {
                    SelectedSavePathTxt.Text = gamesSettings[GameID].CloudSaveFolder;
                }
                if (!gameSettings.AutoSyncPlaytime != null) 
                {
                    AutoSyncPlaytimeChk.IsChecked = gameSettings.AutoSyncPlaytime;
                }
            }
            if (playniteAPI.ApplicationSettings.PlaytimeImportMode == PlaytimeImportMode.Never)
            {
                AutoSyncPlaytimeChk.IsEnabled = false;
            }
            var appList = LegendaryLauncher.GetInstalledAppList();
            if (appList.ContainsKey(GameID))
            {
                if (appList[Game.GameId].Can_run_offline)
                {
                    EnableOfflineModeChk.IsEnabled = true;
                }
            }
            var cloudSyncActions = new Dictionary<CloudSyncAction, string>
            {
                { CloudSyncAction.Download, ResourceProvider.GetString(LOC.LegendaryDownload) },
                { CloudSyncAction.Upload, ResourceProvider.GetString(LOC.LegendaryUpload) },
                { CloudSyncAction.ForceDownload, ResourceProvider.GetString(LOC.LegendaryForceDownload) },
                { CloudSyncAction.ForceUpload, ResourceProvider.GetString(LOC.LegendaryForceUpload) }
            };
            ManualSyncSavesCBo.ItemsSource = cloudSyncActions;
            ManualSyncSavesCBo.SelectedIndex = 0;

            cloudPath = LegendaryCloud.CalculateGameSavesPath(Game.Name, Game.GameId, Game.InstallDirectory);
            if (cloudPath.IsNullOrEmpty())
            {
                CloudSavesSP.Visibility = Visibility.Collapsed;
                CloudSavesNotSupportedTB.Visibility = Visibility.Visible;
            }
        }

        private void ChooseAlternativeExeBtn_Click(object sender, RoutedEventArgs e)
        {
            var file = playniteAPI.Dialogs.SelectFile($"{ResourceProvider.GetString(LOC.Legendary3P_PlayniteExecutableTitle)}|*.exe");
            if (file != "")
            {
                if (!Game.InstallDirectory.IsNullOrEmpty())
                {
                    SelectedAlternativeExeTxt.Text = Helpers.GetRelativePath(Game.InstallDirectory, file);
                }
            }
        }

        private void CalculatePathBtn_Click(object sender, RoutedEventArgs e)
        {
            if (cloudPath.IsNullOrEmpty())
            {
                cloudPath = LegendaryCloud.CalculateGameSavesPath(Game.Name, Game.GameId, Game.InstallDirectory);
            }
            if (!cloudPath.IsNullOrEmpty())
            {
                SelectedSavePathTxt.Text = cloudPath;
            }
        }

        private void ChooseSavePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedCloudPath = playniteAPI.Dialogs.SelectFolder();
            if (selectedCloudPath != "")
            {
                SelectedSavePathTxt.Text = selectedCloudPath;
            }
        }

        private void AutoSyncSavesChk_Click(object sender, RoutedEventArgs e)
        {
            if (AutoSyncSavesChk.IsChecked == true)
            {
                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendarySyncGameSavesWarn), "", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SyncSavesBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryCloudSaveConfirm), ResourceProvider.GetString(LOC.LegendaryCloudSaves), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                CloudSyncAction selectedCloudSyncAction = (CloudSyncAction)ManualSyncSavesCBo.SelectedValue;
                LegendaryCloud.SyncGameSaves(Game.Name, GameID, Game.InstallDirectory, selectedCloudSyncAction, true);
            }
        }
    }
}
