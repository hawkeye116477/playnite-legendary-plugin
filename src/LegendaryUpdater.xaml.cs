﻿using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryUpdater.xaml
    /// </summary>
    public partial class LegendaryUpdater : UserControl
    {
        public Dictionary<string, Installed> UpdatesList => (Dictionary<string, Installed>)DataContext;
        private IPlayniteAPI playniteAPI = API.Instance;
        public LegendaryUpdater()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var gameUpdate in UpdatesList)
            {
                gameUpdate.Value.Title_for_updater = $"{gameUpdate.Value.Title.RemoveTrademarks()} {gameUpdate.Value.Version}";
            }
            UpdatesLB.ItemsSource = UpdatesList;
            UpdatesLB.SelectAll();
            var settings = LegendaryLibrary.GetSettings();
            MaxWorkersNI.Value = settings.MaxWorkers.ToString();
            MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
            ReorderingChk.IsChecked = settings.EnableReordering;
        }

        private async void UpdatesLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateBtn.IsEnabled = UpdatesLB.SelectedIndex != -1;
            double initialDownloadSizeNumber = 0;
            double initialInstallSizeNumber = 0;
            foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, Installed>>().ToList())
            {
                var manifest = await LegendaryLauncher.GetGameInfo(selectedOption.Key);
                bool correctJson = false;
                if (manifest != null && manifest.Manifest != null)
                {
                    correctJson = true;
                }
                if (correctJson)
                {
                    initialDownloadSizeNumber += manifest.Manifest.Download_size;
                    initialInstallSizeNumber += manifest.Manifest.Disk_size;
                }
            }
            var downloadSize = Helpers.FormatSize(initialDownloadSizeNumber);
            DownloadSizeTB.Text = downloadSize;
            var installSize = Helpers.FormatSize(initialInstallSizeNumber);
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
                bool enableReordering = Convert.ToBoolean(ReorderingChk.IsChecked);
                LegendaryUpdateController legendaryUpdateController = new LegendaryUpdateController();
                DownloadProperties downloadProperties = new DownloadProperties
                {
                    downloadAction = DownloadAction.Update,
                    maxWorkers = maxWorkers,
                    maxSharedMemory = maxSharedMemory,
                    enableReordering = enableReordering
                };
                var downloadTasks = new List<Task>();
                foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, Installed>>().ToList())
                {
                    downloadTasks.Add(legendaryUpdateController.UpdateGame(selectedOption.Value.Title, selectedOption.Key, true, downloadProperties));
                }
                var messagesSettings = LegendaryMessagesSettings.LoadSettings();
                if (!messagesSettings.DontShowDownloadManagerWhatsUpMsg)
                {
                    var result = MessageCheckBoxDialog.ShowMessage("", ResourceProvider.GetString(LOC.LegendaryDownloadManagerWhatsUp), ResourceProvider.GetString(LOC.Legendary3P_PlayniteDontShowAgainTitle), MessageBoxButton.OK, MessageBoxImage.Information);
                    if (result.CheckboxChecked)
                    {
                        messagesSettings.DontShowDownloadManagerWhatsUpMsg = true;
                        LegendaryMessagesSettings.SaveSettings(messagesSettings);
                    }
                }
                Window.GetWindow(this).Close();
                if (downloadTasks.Count > 0)
                {
                    await Task.WhenAll(downloadTasks);
                }
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }
    }
}