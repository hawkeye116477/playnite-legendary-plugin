using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryDownloadProperties.xaml
    /// </summary>
    public partial class LegendaryDownloadProperties : UserControl
    {
        private DownloadManagerData.Download SelectedDownload => (DownloadManagerData.Download)DataContext;
        public DownloadManagerData downloadManagerData;
        private IPlayniteAPI playniteAPI = API.Instance;
        public List<string> requiredThings;
        public bool uncheckedByUser = true;
        private bool checkedByUser = true;
        public List<string> selectedSdls = new List<string>();

        public LegendaryGameInfo.Game GameData => new LegendaryGameInfo.Game
        {
            App_name = SelectedDownload.gameID,
            Title = SelectedDownload.name
        };

        public LegendaryDownloadProperties()
        {
            InitializeComponent();
            LoadSavedData();
        }

        private DownloadManagerData LoadSavedData()
        {
            var downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
            downloadManagerData = downloadManager.downloadManagerData;
            return downloadManagerData;
        }

        private async void LegendaryDownloadPropertiesUC_Loaded(object sender, RoutedEventArgs e)
        {
            MaxWorkersNI.MaxValue = Helpers.CpuThreadsNumber;
            var wantedItem = SelectedDownload;
            if (wantedItem.downloadProperties != null)
            {
                SelectedGamePathTxt.Text = wantedItem.downloadProperties.installPath;
                ReorderingChk.IsChecked = wantedItem.downloadProperties.enableReordering;
                MaxWorkersNI.Value = wantedItem.downloadProperties.maxWorkers.ToString();
                MaxSharedMemoryNI.Value = wantedItem.downloadProperties.maxSharedMemory.ToString();
                TaskCBo.SelectedValue = wantedItem.downloadProperties.downloadAction;
            }
            var downloadActionOptions = new Dictionary<DownloadAction, string>
            {
                { DownloadAction.Install, ResourceProvider.GetString(LOC.Legendary3P_PlayniteInstallGame) },
                { DownloadAction.Repair, ResourceProvider.GetString(LOC.LegendaryRepair) },
                { DownloadAction.Update, ResourceProvider.GetString(LOC.Legendary3P_PlayniteUpdaterInstallUpdate) }
            };
            TaskCBo.ItemsSource = downloadActionOptions;

            Dictionary<string, LegendarySDLInfo> extraContentInfo = await LegendaryLauncher.GetExtraContentInfo(SelectedDownload);
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
                if (wantedItem.downloadProperties.extraContent.Count > 0)
                {
                    foreach (var sdl in wantedItem.downloadProperties.extraContent)
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

            if (wantedItem.status == DownloadStatus.Canceled)
            {
                AllOrNothingChk.IsEnabled = true;
                ExtraContentLB.IsEnabled = true;
            }

            if (!wantedItem.downloadProperties.prerequisitesName.IsNullOrEmpty())
            {
                PrerequisitesChk.IsChecked = wantedItem.downloadProperties.installPrerequisites;
                PrerequisitesChk.Visibility = Visibility.Visible;
                PrerequisitesChk.Content = string.Format(PrerequisitesChk.Content.ToString(), wantedItem.downloadProperties.prerequisitesName);
            }

            var gameSize = await LegendaryLauncher.CalculateGameSize(GameData, selectedSdls);
            DownloadSizeTB.Text = Helpers.FormatSize(gameSize.Download_size);
            InstallSizeTB.Text = Helpers.FormatSize(gameSize.Disk_size);
            UpdateSpaceInfo(SelectedDownload.downloadProperties.installPath, gameSize.Disk_size);
        }

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
            }
        }

        private async void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == SelectedDownload.gameID);
            var installPath = SelectedGamePathTxt.Text;
            var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            if (installPath.Contains(playniteDirectoryVariable))
            {
                installPath = installPath.Replace(playniteDirectoryVariable, playniteAPI.Paths.ApplicationPath);
            }
            if (!Helpers.IsDirectoryWritable(installPath))
            {
                return;
            }
            wantedItem.downloadProperties.installPath = installPath;
            wantedItem.downloadProperties.downloadAction = (DownloadAction)TaskCBo.SelectedValue;
            wantedItem.downloadProperties.enableReordering = (bool)ReorderingChk.IsChecked;
            wantedItem.downloadProperties.maxWorkers = int.Parse(MaxWorkersNI.Value);
            wantedItem.downloadProperties.maxSharedMemory = int.Parse(MaxSharedMemoryNI.Value);
            if (PrerequisitesChk.IsEnabled)
            {
                wantedItem.downloadProperties.installPrerequisites = (bool)PrerequisitesChk.IsChecked;
            }
            wantedItem.downloadProperties.ignoreFreeSpace = (bool)IgnoreFreeSpaceChk.IsChecked;
            wantedItem.downloadProperties.extraContent = selectedSdls;
            if (wantedItem.status == DownloadStatus.Canceled)
            {
                var gameSize = await LegendaryLauncher.CalculateGameSize(GameData, selectedSdls);
                wantedItem.downloadSizeNumber = gameSize.Download_size;
                wantedItem.installSizeNumber = gameSize.Disk_size;
            }
            var downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
            var previouslySelected = downloadManager.DownloadsDG.SelectedIndex;
            for (int i = 0; i < downloadManager.downloadManagerData.downloads.Count; i++)
            {
                if (downloadManager.downloadManagerData.downloads[i].gameID == SelectedDownload.gameID)
                {
                    downloadManager.downloadManagerData.downloads[i] = wantedItem;
                    break;
                }
            }
            downloadManager.DownloadsDG.SelectedIndex = previouslySelected;
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
                SpaceTB.Text = Helpers.FormatSize(availableFreeSpace);
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
            AfterInstallingTB.Text = Helpers.FormatSize(afterInstallSizeNumber);
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
            var selectedExtraContent = ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList();
            foreach (var selectedSdl in selectedExtraContent)
            {
                selectedSdls.Add(selectedSdl.Key);
            }
            var gameSize = await LegendaryLauncher.CalculateGameSize(GameData, selectedSdls);
            DownloadSizeTB.Text = Helpers.FormatSize(gameSize.Download_size);
            InstallSizeTB.Text = Helpers.FormatSize(gameSize.Disk_size);
            UpdateSpaceInfo(SelectedDownload.downloadProperties.installPath, gameSize.Disk_size);
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
