using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
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
        public Dictionary<string, UpdateInfo> UpdatesList => (Dictionary<string, UpdateInfo>)DataContext;
        public LegendaryUpdater()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            CommonHelpers.SetControlBackground(this);
            foreach (var gameUpdate in UpdatesList)
            {
                gameUpdate.Value.Title_for_updater = $"{gameUpdate.Value.Title.RemoveTrademarks()} {gameUpdate.Value.Version}";
            }
            UpdatesLB.ItemsSource = UpdatesList;
            UpdatesLB.SelectAll();
            var settings = LegendaryLibrary.GetSettings();
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
            MaxWorkersNI.Value = settings.MaxWorkers.ToString();
            MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
            ReorderingChk.IsChecked = settings.EnableReordering;
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

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }
    }
}
