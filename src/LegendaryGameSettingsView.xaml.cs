using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using Playnite;
using System;
using System.Collections.Generic;
using System.Windows;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace LegendaryLibraryNS;

/// <summary>
/// Interaction logic for LegendaryGameSettingsView.xaml
/// </summary>
public partial class LegendaryGameSettingsView
{
    private LegendaryGameSettingsViewModel Vm => (DataContext as LegendaryGameSettingsViewModel)!;
    private Game Game => Vm.Game;
    private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
    private string? cloudPath;
    public GameSettings? GameSettings;
    private CommonHelpers commonHelpers = LegendaryLibrary.Instance.CommonHelpers;


    public LegendaryGameSettingsView()
    {
        InitializeComponent();
    }

    private void LegendaryGameSettingsViewUC_Loaded(object sender, RoutedEventArgs e)
    {
        commonHelpers.SetControlBackground(this);
        var appList = LegendaryLauncher.GetInstalledAppList();
        if (appList.ContainsKey(Game.LibraryGameId!))
        {
            if (appList[Game.LibraryGameId!].Can_run_offline)
            {
                EnableOfflineModeChk.IsEnabled = true;
            }

            GameVersionTxt.Text = appList[Game.LibraryGameId!].Version;
        }
        else
        {
            VersionSP.Visibility = Visibility.Collapsed;
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
                CloudSavesSP.IsEnabled = false;
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