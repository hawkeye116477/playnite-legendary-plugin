using LegendaryLibraryNS.Enums;
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
            var dataDir = LegendaryLibrary.Instance.GetPluginUserDataPath();
            var dataFile = Path.Combine(dataDir, "downloadManager.json");
            downloadManagerData = Serialization.FromJson<DownloadManagerData.Rootobject>(FileSystem.ReadFileAsStringSafe(dataFile));
            return downloadManagerData;
        }

        private void LegendaryDownloadPropertiesUC_Loaded(object sender, RoutedEventArgs e)
        {
            var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == SelectedDownload.gameID);
            SelectedGamePathTxt.Text = wantedItem.installPath;
            ReorderingChk.IsChecked = wantedItem.enableReordering;
            MaxWorkersNI.Value = wantedItem.maxWorkers.ToString();
            MaxSharedMemoryNI.Value = wantedItem.maxSharedMemory.ToString();
            var downloadActionOptions = new Dictionary<int, string>
            {
                { (int)DownloadAction.Install, ResourceProvider.GetString("LOCInstallGame") },
                { (int)DownloadAction.Repair, ResourceProvider.GetString(LOC.LegendaryRepair) }
            };
            TaskCBo.ItemsSource = downloadActionOptions;
            TaskCBo.SelectedValue = wantedItem.downloadAction;
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
                    foreach (var selectedExtraContent in wantedItem.extraContent)
                    {
                        var wantedExtraItem = sdlInfo.SingleOrDefault(i => i.Value.Tags.Contains(selectedExtraContent));
                        if (wantedExtraItem.Key != null)
                        {
                            ExtraContentLB.SelectedItems.Add(wantedExtraItem);
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
            var downloadManagerData = Serialization.FromJson<DownloadManagerData.Rootobject>(FileSystem.ReadFileAsStringSafe(dataFile));
            var wantedItem = downloadManagerData.downloads.FirstOrDefault(item => item.gameID == SelectedDownload.gameID);
            wantedItem.installPath = SelectedGamePathTxt.Text;
            wantedItem.downloadAction = (int)TaskCBo.SelectedValue;
            wantedItem.enableReordering = (bool)ReorderingChk.IsChecked;
            wantedItem.maxWorkers = int.Parse(MaxWorkersNI.Value);
            wantedItem.maxSharedMemory = int.Parse(MaxSharedMemoryNI.Value);
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
            wantedItem.extraContent = selectedExtraContent;
            var strConf = Serialization.ToJson(downloadManagerData, true);
            File.WriteAllText(dataFile, strConf);
            Window.GetWindow(this).Close();
        }
    }
}
