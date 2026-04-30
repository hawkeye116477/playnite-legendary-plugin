using LegendaryLibraryNS.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using Playnite;
using Playnite.Common;
using LegendaryLibraryNS.Enums;
using CommonPlugin;
using CommonPlugin.Enums;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace LegendaryLibraryNS;

/// <summary>
/// Interaction logic for LegendaryGameSettingsView.xaml
/// </summary>
public partial class LegendaryGameSettingsView
{
    private Game Game => (DataContext as Game)!;
    public string GameId => Game.LibraryGameId!;
    private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
    private string? cloudPath;
    public GameSettings? GameSettings;
    private CommonHelpers commonHelpers = LegendaryLibrary.Instance.CommonHelpers;


    public LegendaryGameSettingsView()
    {
        InitializeComponent();
    }

    public static GameSettings LoadGameSettings(string gameId)
    {
        var playniteApi = LegendaryLibrary.PlayniteApi;
        var gameSettings = new GameSettings();
        var gameSettingsFile = Path.Combine(playniteApi.UserDataDir, "GamesSettings", $"{gameId}.json");
        if (File.Exists(gameSettingsFile))
        {
            if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(gameSettingsFile), out GameSettings? savedGameSettings))
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
        if (EnableOfflineModeChk.IsChecked != globalSettings!.LaunchOffline)
        {
            newGameSettings.LaunchOffline = EnableOfflineModeChk.IsChecked;
        }

        var globalDisableUpdates = false;
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
            newGameSettings.StartupArguments = StartupArgumentsTxt.Text.SplitOutsideQuotes(' ')!.ToList();
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

        var gameSettingsFile = Path.Combine(playniteApi.UserDataDir, "GamesSettings", $"{GameId}.json");
        if (newGameSettings.GetType().GetProperties().Any(p => p.GetValue(newGameSettings) != null) || File.Exists(gameSettingsFile))
        {
            if (File.Exists(gameSettingsFile))
            {
                var oldGameSettings = LoadGameSettings(GameId);
                if (oldGameSettings.InstallPrerequisites)
                {
                    newGameSettings.InstallPrerequisites = true;
                }
            }

            commonHelpers.SaveJsonSettingsToFile(newGameSettings, "GamesSettings", GameId, true);
        }

        Window.GetWindow(this)?.Close();
    }

    private void LegendaryGameSettingsViewUC_Loaded(object sender, RoutedEventArgs e)
    {
        commonHelpers.SetControlBackground(this);
        var globalSettings = LegendaryLibrary.GetSettings();
        EnableOfflineModeChk.IsChecked = globalSettings!.LaunchOffline;
        if (globalSettings.GamesUpdatePolicy == UpdatePolicy.Never)
        {
            DisableGameUpdateCheckingChk.IsChecked = true;
        }

        AutoSyncSavesChk.IsChecked = globalSettings.SyncGameSaves;
        AutoSyncPlaytimeChk.IsChecked = globalSettings.SyncPlaytime;
        GameSettings = LoadGameSettings(GameId);
        if (GameSettings.LaunchOffline != null)
        {
            EnableOfflineModeChk.IsChecked = GameSettings.LaunchOffline;
        }

        if (GameSettings.DisableGameVersionCheck != null)
        {
            DisableGameUpdateCheckingChk.IsChecked = GameSettings.DisableGameVersionCheck;
        }

        if (GameSettings.StartupArguments != null)
        {
            StartupArgumentsTxt.Text = string.Join(" ", GameSettings.StartupArguments);
        }

        if (GameSettings.LanguageCode != null)
        {
            LanguageCodeTxt.Text = GameSettings.LanguageCode;
        }

        if (GameSettings.OverrideExe != null)
        {
            SelectedAlternativeExeTxt.Text = GameSettings.OverrideExe;
        }

        if (GameSettings.AutoSyncSaves != null)
        {
            AutoSyncSavesChk.IsChecked = GameSettings.AutoSyncSaves;
        }

        if (!GameSettings.CloudSaveFolder.IsNullOrEmpty())
        {
            SelectedSavePathTxt.Text = GameSettings.CloudSaveFolder;
        }

        if (!GameSettings.AutoSyncPlaytime != null)
        {
            AutoSyncPlaytimeChk.IsChecked = GameSettings.AutoSyncPlaytime;
        }

        // if (playniteApi.ApplicationSettings.PlaytimeImportMode == PlaytimeImportMode.Never)
        // {
        //     AutoSyncPlaytimeChk.IsEnabled = false;
        // }
        var appList = LegendaryLauncher.GetInstalledAppList();
        if (appList.ContainsKey(GameId))
        {
            if (appList[Game.LibraryGameId!].Can_run_offline)
            {
                EnableOfflineModeChk.IsEnabled = true;
            }
        }

        var cloudSyncActions = new Dictionary<CloudSyncAction, string>
        {
            { CloudSyncAction.Download, LocalizationManager.Instance.GetString(LOC.CommonDownload) },
            { CloudSyncAction.Upload, LocalizationManager.Instance.GetString(LOC.CommonUpload) }
        };
        ManualSyncSavesCBo.ItemsSource = cloudSyncActions;
        ManualSyncSavesCBo.SelectedIndex = 0;

        Dispatcher.BeginInvoke((Action)(async void () =>
        {
            cloudPath = await LegendaryCloud.CalculateGameSavesPath(Game.Name, Game.LibraryGameId!, Game.InstallDirectory!);
            if (cloudPath.IsNullOrEmpty())
            {
                CloudSavesSP.Visibility = Visibility.Collapsed;
                CloudSavesNotSupportedTB.Visibility = Visibility.Visible;
            }
        }));
    }

    private async void ChooseAlternativeExeBtn_Click(object sender, RoutedEventArgs e)
    {
        var fileTypes = new Dictionary<string, string[]>
        {
            { LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExecutableTitle), ["*.exe"] }
        };
        var files = await playniteApi.Dialogs.SelectFileAsync(fileTypes, initialDir: Game.InstallDirectory);
        if (files is { Count: > 0 })
        {
            if (!string.IsNullOrEmpty(Game.InstallDirectory))
            {
                SelectedAlternativeExeTxt.Text = RelativePath.Get(Game.InstallDirectory, files[0]);
            }
        }
    }

    private async void CalculatePathBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(cloudPath))
        {
            cloudPath = await LegendaryCloud.CalculateGameSavesPath(Game.Name, Game.LibraryGameId!, Game.InstallDirectory!, false);
        }

        if (!cloudPath.IsNullOrEmpty())
        {
            SelectedSavePathTxt.Text = cloudPath;
        }
    }

    private async void ChooseSavePathBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = await playniteApi.Dialogs.SelectFolderAsync();
        if (result is { Count: > 0 })
        {
            SelectedSavePathTxt.Text = result[0];
        }
    }

    private async void AutoSyncSavesChk_Click(object sender, RoutedEventArgs e)
    {
        if (AutoSyncSavesChk.IsChecked == true)
        {
            await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonSyncGameSavesWarn), "",
                MessageBoxButtons.OK, MessageBoxSeverity.Warning);
        }
    }

    private async void SyncSavesBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonCloudSaveConfirm),
            LocalizationManager.Instance.GetString(LOC.CommonCloudSaves), MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
        if (result == MessageBoxResult.Yes)
        {
            var forceCloudSync = (bool)ForceCloudActionChk.IsChecked!;
            var selectedCloudSyncAction = (CloudSyncAction)ManualSyncSavesCBo.SelectedValue;
            var selectedSavePath = SelectedSavePathTxt.Text;
            if (selectedSavePath != "")
            {
                await LegendaryCloud.SyncGameSaves(Game, selectedCloudSyncAction, forceCloudSync, true, true, selectedSavePath);
            }
            else
            {
                await LegendaryCloud.SyncGameSaves(Game, selectedCloudSyncAction, forceCloudSync, true);
            }
        }
    }
}