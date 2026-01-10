using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryUpdater.xaml
    /// </summary>
    public partial class LegendaryUpdater : UserControl
    {
        public Dictionary<string, UpdateInfo> UpdatesList;
        private IPlayniteAPI playniteAPI = API.Instance;
        public List<Playnite.SDK.Models.Game> checkedGames;

        public LegendaryUpdater()
        {
            InitializeComponent();
        }

        public LegendaryUpdater(List<Playnite.SDK.Models.Game> games)
        {
            InitializeComponent();
            checkedGames = games;
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            UpdatesList = (Dictionary<string, UpdateInfo>)DataContext;
            CommonHelpers.SetControlBackground(this);
            RefreshWindow();
            var settings = LegendaryLibrary.GetSettings();
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
            MaxWorkersNI.Value = settings.MaxWorkers.ToString();
            MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
            ReorderingChk.IsChecked = settings.EnableReordering;

            var successUpdates = UpdatesList.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);

            if (UpdatesList.Count > 0 && successUpdates.Count == 0)
            {
                playniteAPI.Dialogs.ShowErrorMessage(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage), LegendaryLibrary.Instance.Name);
                Window.GetWindow(this).Close();
                return;
            }

            if (checkedGames.Count > 0 && (UpdatesList.Count == 0))
            {
                var options = new List<MessageBoxOption>
                {
                    new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.CommonReload), false),
                    new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel), true, true),
                };
                var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable), LegendaryLibrary.Instance.Name, MessageBoxImage.Information, options);
                if (result == options[0])
                {
                    var checkedGamesIds = checkedGames.Select(g => g.GameId).ToList();
                    GlobalProgressOptions updateCheckProgressOptions = new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false) { IsIndeterminate = true };
                    playniteAPI.Dialogs.ActivateGlobalProgress(async (a) =>
                    {
                        LegendaryLauncher.ClearSpecificGamesCache(checkedGamesIds);
                        LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                        if (checkedGamesIds.Count > 1)
                        {
                            UpdatesList = await legendaryUpdateController.CheckAllGamesUpdates();
                        }
                        else
                        {
                            UpdatesList = await legendaryUpdateController.CheckGameUpdates(checkedGames[0].Name, checkedGames[0].GameId);
                        }
                    }, updateCheckProgressOptions);
                    if (UpdatesList.Count == 0)
                    {
                        playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable), LegendaryLibrary.Instance.Name);
                        Window.GetWindow(this).Close();
                        return;
                    }
                    RefreshWindow();
                }
                else
                {
                    Window.GetWindow(this).Close();
                }
            }
        }

        private void RefreshWindow()
        {
            UpdateBtn.IsEnabled = false;
            DownloadSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
            InstallSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);

            var successUpdates = UpdatesList.Where(i => i.Value.Success).ToDictionary(i => i.Key, i => i.Value);
            foreach (var gameUpdate in successUpdates)
            {
                gameUpdate.Value.Title_for_updater = $"{gameUpdate.Value.Title.RemoveTrademarks()} {gameUpdate.Value.Version}";
            }
            UpdatesLB.ItemsSource = successUpdates;
            UpdatesLB.SelectAll();
            if (UpdatesList.Count > 0)
            {
                UpdateBtn.IsEnabled = true;
            }
        }

        private void UpdatesLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBtn.IsEnabled = UpdatesLB.SelectedIndex != -1;
            double initialDownloadSizeNumber = 0;
            double initialInstallSizeNumber = 0;
            foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, UpdateInfo>>().ToList())
            {
                initialDownloadSizeNumber += selectedOption.Value.Download_size;
                initialInstallSizeNumber += selectedOption.Value.Disk_size;
            }
            var downloadSize = CommonHelpers.FormatSize(initialDownloadSizeNumber);
            DownloadSizeTB.Text = downloadSize;
            var installSize = CommonHelpers.FormatSize(initialInstallSizeNumber);
            InstallSizeTB.Text = installSize;
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (UpdatesLB.Items.Count == UpdatesLB.SelectedItems.Count)
            {
                UpdatesLB.UnselectAll();
            }
            else
            {
                UpdatesLB.SelectAll();
            }
        }

        private async void UpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (UpdatesLB.SelectedItems.Count > 0)
            {
                var settings = LegendaryLibrary.GetSettings();
                MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
                int maxWorkers = settings.MaxWorkers;
                if (MaxWorkersNI.Value != "")
                {
                    maxWorkers = int.Parse(MaxWorkersNI.Value);
                }
                int maxSharedMemory = settings.MaxSharedMemory;
                if (MaxSharedMemoryNI.Value != "")
                {
                    maxSharedMemory = int.Parse(MaxSharedMemoryNI.Value);
                }
                LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                DownloadProperties downloadProperties = new DownloadProperties
                {
                    downloadAction = DownloadAction.Update,
                    maxWorkers = maxWorkers,
                    maxSharedMemory = maxSharedMemory,
                    enableReordering = (bool)ReorderingChk.IsChecked,
                    ignoreFreeSpace = (bool)IgnoreFreeSpaceChk.IsChecked
                };
                Window.GetWindow(this).Close();
                var updatesList = new Dictionary<string, UpdateInfo>();
                foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, UpdateInfo>>().ToList())
                {
                    updatesList.Add(selectedOption.Key, selectedOption.Value);
                }
                await legendaryUpdateController.UpdateGame(updatesList, "", false, downloadProperties);
            }
        }
    }
}
