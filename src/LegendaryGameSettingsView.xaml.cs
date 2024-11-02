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
using CommonPlugin;
using CommonPlugin.Enums;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryGameSettingsView.xaml
    /// </summary>
    public partial class LegendaryGameSettingsView : UserControl
    {
        private Game Game => DataContext as Game;
        public string GameID => Game.GameId;
        private IPlayniteAPI playniteAPI = API.Instance;
        private string cloudPath;
        public GameSettings gameSettings;


        public LegendaryGameSettingsView()
        {
            InitializeComponent();
        }

        public static GameSettings LoadGameSettings(string gameID)
        {
            var gameSettings = new GameSettings();
            var gameSettingsFile = Path.Combine(LegendaryLibrary.Instance.GetPluginUserDataPath(), "GamesSettings", $"{gameID}.json");
            if (File.Exists(gameSettingsFile))
            {
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(gameSettingsFile), out GameSettings savedGameSettings))
                {
                    if (savedGameSettings != null)
                    {
                        gameSettings = savedGameSettings;
                    }
                }
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
            var gameSettingsFile = Path.Combine(LegendaryLibrary.Instance.GetPluginUserDataPath(), "GamesSettings", $"{GameID}.json");
            if (newGameSettings.GetType().GetProperties().Any(p => p.GetValue(newGameSettings) != null) || File.Exists(gameSettingsFile))
            {
                if (File.Exists(gameSettingsFile))
                {
                    var oldGameSettings = LoadGameSettings(GameID);
                    if (oldGameSettings.InstallPrerequisites)
                    {
                        newGameSettings.InstallPrerequisites = true;
                    }
                }
                var commonHelpers = LegendaryLibrary.Instance.commonHelpers;
                commonHelpers.SaveJsonSettingsToFile(newGameSettings, "GamesSettings", GameID, true);
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
            gameSettings = LoadGameSettings(GameID);
            if (gameSettings.LaunchOffline != null)
            {
                EnableOfflineModeChk.IsChecked = gameSettings.LaunchOffline;
            }
            if (gameSettings.DisableGameVersionCheck != null)
            {
                DisableGameUpdateCheckingChk.IsChecked = gameSettings.DisableGameVersionCheck;
            }
            if (gameSettings.StartupArguments != null)
            {
                StartupArgumentsTxt.Text = string.Join(" ", gameSettings.StartupArguments);
            }
            if (gameSettings.LanguageCode != null)
            {
                LanguageCodeTxt.Text = gameSettings.LanguageCode;
            }
            if (gameSettings.OverrideExe != null)
            {
                SelectedAlternativeExeTxt.Text = gameSettings.OverrideExe;
            }
            if (gameSettings.AutoSyncSaves != null)
            {
                AutoSyncSavesChk.IsChecked = gameSettings.AutoSyncSaves;
            }
            if (!gameSettings.CloudSaveFolder.IsNullOrEmpty())
            {
                SelectedSavePathTxt.Text = gameSettings.CloudSaveFolder;
            }
            if (!gameSettings.AutoSyncPlaytime != null)
            {
                AutoSyncPlaytimeChk.IsChecked = gameSettings.AutoSyncPlaytime;
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
            };
            ManualSyncSavesCBo.ItemsSource = cloudSyncActions;
            ManualSyncSavesCBo.SelectedIndex = 0;

            Dispatcher.BeginInvoke((Action)(() =>
            {
                cloudPath = LegendaryCloud.CalculateGameSavesPath(Game.Name, Game.GameId, Game.InstallDirectory);
                if (cloudPath.IsNullOrEmpty())
                {
                    CloudSavesSP.Visibility = Visibility.Collapsed;
                    CloudSavesNotSupportedTB.Visibility = Visibility.Visible;
                }
            }));
        }

        private void ChooseAlternativeExeBtn_Click(object sender, RoutedEventArgs e)
        {
            var file = playniteAPI.Dialogs.SelectFile($"{ResourceProvider.GetString(LOC.Legendary3P_PlayniteExecutableTitle)}|*.exe");
            if (file != "")
            {
                if (!Game.InstallDirectory.IsNullOrEmpty())
                {
                    SelectedAlternativeExeTxt.Text = RelativePath.Get(Game.InstallDirectory, file);
                }
            }
        }

        private void CalculatePathBtn_Click(object sender, RoutedEventArgs e)
        {
            if (cloudPath.IsNullOrEmpty())
            {
                cloudPath = LegendaryCloud.CalculateGameSavesPath(Game.Name, Game.GameId, Game.InstallDirectory, false);
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
                bool forceCloudSync = (bool)ForceCloudActionChk.IsChecked;
                CloudSyncAction selectedCloudSyncAction = (CloudSyncAction)ManualSyncSavesCBo.SelectedValue;
                var selectedSavePath = SelectedSavePathTxt.Text;
                if (selectedSavePath != "")
                {
                    LegendaryCloud.SyncGameSaves(Game, selectedCloudSyncAction, forceCloudSync, true, true, selectedSavePath);
                }
                else
                {
                    LegendaryCloud.SyncGameSaves(Game, selectedCloudSyncAction, forceCloudSync, true);
                }
            }
        }
    }
}
