using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using Playnite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Models;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryDownloadProperties.xaml
    /// </summary>
    public partial class LegendaryDownloadProperties : UserControl
    {
        private DownloadManagerData.Download SelectedDownload => (DownloadManagerData.Download)DataContext;
        private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
        public List<string> RequiredThings;
        private bool uncheckedByUser = true;
        private bool checkedByUser = true;
        private List<string> selectedSdls = [];
        private readonly CommonHelpers commonHelpers = LegendaryLibrary.Instance.CommonHelpers;

        private LegendaryGameInfo.Game GameData => new LegendaryGameInfo.Game
        {
            App_name = SelectedDownload.GameId,
            Title = SelectedDownload.Name
        };

        public LegendaryDownloadProperties()
        {
            InitializeComponent();
        }

        private async void LegendaryDownloadPropertiesUC_Loaded(object sender, RoutedEventArgs e)
        {
            commonHelpers.SetControlBackground(this);
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
            var wantedItem = SelectedDownload;
            SelectedGamePathTxt.Text = wantedItem.DownloadProperties.InstallPath;
            ReorderingChk.IsChecked = wantedItem.DownloadProperties.EnableReordering;
            IgnoreFreeSpaceChk.IsChecked = wantedItem.DownloadProperties.IgnoreFreeSpace;
            MaxWorkersNI.Value = wantedItem.DownloadProperties.MaxWorkers.ToString();
            MaxSharedMemoryNI.Value = wantedItem.DownloadProperties.MaxSharedMemory.ToString();
            TaskCBo.SelectedValue = wantedItem.DownloadProperties.DownloadAction;
            var downloadActionOptions = new Dictionary<DownloadAction, string>
            {
                { DownloadAction.Install, LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame) },
                { DownloadAction.Repair, LocalizationManager.Instance.GetString(LOC.CommonRepair) },
                {
                    DownloadAction.Update,
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdaterInstallUpdate)
                }
            };
            TaskCBo.ItemsSource = downloadActionOptions;

            var extraContentInfo = await LegendaryLauncher.GetExtraContentInfo(SelectedDownload);
            if (extraContentInfo.Count > 0)
            {
                var sdls = extraContentInfo.Where(i => i.Value.Is_dlc == false);
                if (sdls.Count() > 0)
                {
                    SizeGrd.Visibility = Visibility.Visible;
                }

                if (sdls.Count() > 1)
                {
                    AllOrNothingChk.Visibility = Visibility.Visible;
                }

                ExtraContentLB.ItemsSource = sdls;
                if (wantedItem.DownloadProperties.ExtraContent.Count > 0)
                {
                    foreach (var sdl in wantedItem.DownloadProperties.ExtraContent)
                    {
                        var selectedItem = extraContentInfo.FirstOrDefault(i => i.Key == sdl);
                        if (selectedItem.Key != null)
                        {
                            ExtraContentLB.SelectedItems.Add(selectedItem);
                        }
                    }
                }

                ExtraContentTbI.Visibility = Visibility.Visible;
            }

            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi(playniteApi);
            var wantedUnifiedTask =
                unifiedDownloadManagerApi.GetTask(wantedItem.GameId, LegendaryLibrary.PluginId);
            if (wantedUnifiedTask?.Status == UnifiedDownloadStatus.Canceled)
            {
                AllOrNothingChk.IsEnabled = true;
                ExtraContentLB.IsEnabled = true;
            }

            if (!wantedItem.DownloadProperties.PrerequisitesName.IsNullOrEmpty())
            {
                PrerequisitesChk.IsChecked = wantedItem.DownloadProperties.InstallPrerequisites;
                PrerequisitesChk.Visibility = Visibility.Visible;
                PrerequisitesChk.Content = PrerequisitesChk.Content.ToString()
                                                          ?.Replace("$prerequisiteName",
                                                                wantedItem.DownloadProperties.PrerequisitesName);
            }

            var gameSize = await LegendaryLauncher.CalculateGameSize(GameData, selectedSdls);
            DownloadSizeTB.Text = CommonHelpers.FormatSize(gameSize.Download_size);
            InstallSizeTB.Text = CommonHelpers.FormatSize(gameSize.Disk_size);
            UpdateSpaceInfo(SelectedDownload.DownloadProperties.InstallPath, gameSize.Disk_size);
        }

        private async void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var folders = await playniteApi.Dialogs.SelectFolderAsync();
            if (folders is { Count: > 0 } && folders[0] != "")
            {
                SelectedGamePathTxt.Text = folders[0];
            }
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var wantedItem =
                LegendaryLibrary.Instance.PluginDownloadData.Downloads.FirstOrDefault(item =>
                    item.GameId == SelectedDownload.GameId);
            var installPath = SelectedGamePathTxt.Text;
            var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            if (installPath.Contains(playniteDirectoryVariable))
            {
                installPath = installPath.Replace(playniteDirectoryVariable, playniteApi.AppInfo.ApplicationDirectory);
            }

            if (!await commonHelpers.IsDirectoryWritable(installPath, LOC.CommonPermissionError))
            {
                return;
            }

            wantedItem.DownloadProperties.InstallPath = installPath;
            wantedItem.DownloadProperties.DownloadAction = (DownloadAction)TaskCBo.SelectedValue;
            wantedItem.DownloadProperties.EnableReordering = (bool)ReorderingChk.IsChecked;
            wantedItem.DownloadProperties.MaxWorkers = int.Parse(MaxWorkersNI.Value);
            wantedItem.DownloadProperties.MaxSharedMemory = int.Parse(MaxSharedMemoryNI.Value);
            if (PrerequisitesChk.IsEnabled)
            {
                wantedItem.DownloadProperties.InstallPrerequisites = (bool)PrerequisitesChk.IsChecked;
            }

            wantedItem.DownloadProperties.IgnoreFreeSpace = (bool)IgnoreFreeSpaceChk.IsChecked;
            wantedItem.DownloadProperties.ExtraContent = selectedSdls;
            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi(playniteApi);
            var wantedUnifiedTask =
                unifiedDownloadManagerApi.GetTask(wantedItem.GameId, LegendaryLibrary.PluginId);
            if (wantedUnifiedTask.Status == UnifiedDownloadStatus.Canceled)
            {
                var gameSize = await LegendaryLauncher.CalculateGameSize(GameData, selectedSdls);
                wantedUnifiedTask.DownloadSizeBytes = gameSize.Download_size;
                wantedUnifiedTask.InstallSizeBytes = gameSize.Download_size;
            }

            LegendaryLibrary.Instance.SaveDownloadData();
            Window.GetWindow(this).Close();
        }

        private void AllOrNothingChk_Checked(object sender, RoutedEventArgs e)
        {
            if (checkedByUser)
            {
                ExtraContentLB.SelectAll();
            }
        }

        private void AllOrNothingChk_Unchecked(object sender, RoutedEventArgs e)
        {
            if (uncheckedByUser)
            {
                ExtraContentLB.SelectedItems.Clear();
            }
        }

        private void UpdateSpaceInfo(string path, double installSizeNumber)
        {
            DriveInfo dDrive = new DriveInfo(path);
            if (dDrive.IsReady)
            {
                long availableFreeSpace = dDrive.AvailableFreeSpace;
                SpaceTB.Text = CommonHelpers.FormatSize(availableFreeSpace);
                UpdateAfterInstallingSize(availableFreeSpace, installSizeNumber);
            }
        }

        private void UpdateAfterInstallingSize(long availableFreeSpace, double installSizeNumber)
        {
            double afterInstallSizeNumber = (double)(availableFreeSpace - installSizeNumber);
            if (afterInstallSizeNumber < 0)
            {
                afterInstallSizeNumber = 0;
            }

            AfterInstallingTB.Text = CommonHelpers.FormatSize(afterInstallSizeNumber);
        }

        private async void ExtraContentLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            selectedSdls = new List<string>();
            var requiredTags = await LegendaryLauncher.GetRequiredSdlsTags(SelectedDownload);
            if (requiredTags.Count > 0)
            {
                foreach (var requiredTag in requiredTags)
                {
                    selectedSdls.AddMissing(requiredTag);
                }
            }

            var selectedExtraContent =
                ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySdlInfo>>().ToList();
            foreach (var selectedSdl in selectedExtraContent)
            {
                selectedSdls.Add(selectedSdl.Key);
            }

            var gameSize = await LegendaryLauncher.CalculateGameSize(GameData, selectedSdls);
            DownloadSizeTB.Text = CommonHelpers.FormatSize(gameSize.Download_size);
            InstallSizeTB.Text = CommonHelpers.FormatSize(gameSize.Disk_size);
            UpdateSpaceInfo(SelectedDownload.DownloadProperties.InstallPath, gameSize.Disk_size);
            if (AllOrNothingChk.IsChecked == true && selectedExtraContent.Count() != ExtraContentLB.Items.Count)
            {
                uncheckedByUser = false;
                AllOrNothingChk.IsChecked = false;
                uncheckedByUser = true;
            }

            if (AllOrNothingChk.IsChecked == false && selectedExtraContent.Count() == ExtraContentLB.Items.Count)
            {
                checkedByUser = false;
                AllOrNothingChk.IsChecked = true;
                checkedByUser = true;
            }
        }
    }
}