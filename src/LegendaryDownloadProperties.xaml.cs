﻿using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryDownloadProperties.xaml
    /// </summary>
    public partial class LegendaryDownloadProperties : UserControl
    {
        private DownloadManagerData.Download SelectedDownload => (DownloadManagerData.Download)DataContext;
        public DownloadManagerData.Rootobject downloadManagerData;
        private IPlayniteAPI playniteAPI = API.Instance;
        public List<string> requiredThings;

        public LegendaryDownloadProperties()
        {
            InitializeComponent();
            LoadSavedData();
        }

        private DownloadManagerData.Rootobject LoadSavedData()
        {
            var downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
            downloadManagerData = downloadManager.downloadManagerData;
            return downloadManagerData;
        }

        private void LegendaryDownloadPropertiesUC_Loaded(object sender, RoutedEventArgs e)
        {
            var wantedItem = SelectedDownload;
            if (wantedItem.downloadProperties != null)
            {
                SelectedGamePathTxt.Text = wantedItem.downloadProperties.installPath;
                ReorderingChk.IsChecked = wantedItem.downloadProperties.enableReordering;
                MaxWorkersNI.Value = wantedItem.downloadProperties.maxWorkers.ToString();
                MaxSharedMemoryNI.Value = wantedItem.downloadProperties.maxSharedMemory.ToString();
                TaskCBo.SelectedValue = wantedItem.downloadProperties.downloadAction;
            }
            var downloadActionOptions = new Dictionary<int, string>
            {
                { (int)DownloadAction.Install, ResourceProvider.GetString("LOCInstallGame") },
                { (int)DownloadAction.Repair, ResourceProvider.GetString(LOC.LegendaryRepair) }
            };
            TaskCBo.ItemsSource = downloadActionOptions;

            var cacheSDLPath = LegendaryLibrary.Instance.GetCachePath("sdlcache");
            var cacheSDLFile = Path.Combine(cacheSDLPath, SelectedDownload.gameID + ".json");
            requiredThings = new List<string>();
            if (File.Exists(cacheSDLFile))
            {
                var content = FileSystem.ReadFileAsStringSafe(cacheSDLFile);
                if (Serialization.TryFromJson<Dictionary<string, LegendarySDLInfo>>(content, out var sdlInfo))
                {
                    if (sdlInfo.ContainsKey("__required"))
                    {
                        foreach (var tag in sdlInfo["__required"].Tags)
                        {
                            requiredThings.Add(tag);
                        }
                        sdlInfo.Remove("__required");
                    }
                    if (wantedItem.downloadProperties != null)
                    {
                        foreach (var selectedExtraContent in wantedItem.downloadProperties.extraContent)
                        {
                            var wantedExtraItem = sdlInfo.SingleOrDefault(i => i.Value.Tags.Contains(selectedExtraContent));
                            if (wantedExtraItem.Key != null)
                            {
                                ExtraContentLB.SelectedItems.Add(wantedExtraItem);
                            }
                        }
                    }
                    ExtraContentLB.ItemsSource = sdlInfo;
                    ExtraContentTbI.Visibility = Visibility.Visible;
                }
            }
        }

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
            }
        }

        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "downloadManager.json");
            var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == SelectedDownload.gameID);
            wantedItem.downloadProperties.installPath = SelectedGamePathTxt.Text;
            wantedItem.downloadProperties.downloadAction = (int)TaskCBo.SelectedValue;
            wantedItem.downloadProperties.enableReordering = (bool)ReorderingChk.IsChecked;
            wantedItem.downloadProperties.maxWorkers = int.Parse(MaxWorkersNI.Value);
            wantedItem.downloadProperties.maxSharedMemory = int.Parse(MaxSharedMemoryNI.Value);
            var selectedExtraContent = new List<string>();
            if (requiredThings.Count > 0)
            {
                selectedExtraContent.Add("");
                foreach (var requiredThing in requiredThings)
                {
                    selectedExtraContent.Add(requiredThing);
                }
            }
            if (ExtraContentLB.Items.Count > 0)
            {
                selectedExtraContent.AddMissing("");
                foreach (var selectedOption in ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList())
                {
                    foreach (var tag in selectedOption.Value.Tags)
                    {
                        if (!selectedExtraContent.Contains(tag))
                        {
                            selectedExtraContent.Add(tag);
                        }
                    }
                }
            }
            wantedItem.downloadProperties.extraContent = selectedExtraContent;
            var strConf = Serialization.ToJson(downloadManagerData, true);
            File.WriteAllText(dataFile, strConf);
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
    }
}
