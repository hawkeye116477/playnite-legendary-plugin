using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryGameInstaller.xaml
    /// </summary>
    public partial class LegendaryGameInstaller : UserControl
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        public string installCommand;
        public string downloadSizeWithoutDlcs;
        public string installSizeWithoutDlcs;
        public List<string> requiredThings;
        public double downloadSizeNumber;
        public double installSizeNumber;
        public double? installSizeNumberAfterMod;
        public long availableFreeSpace;
        private LegendaryGameInfo.Rootobject manifest;
        public bool uncheckedByUser = true;
        public string prereqName = "";

        public LegendaryGameInstaller()
        {
            InitializeComponent();
            SetControlStyles();
        }

        public Window InstallerWindow => Window.GetWindow(this);

        public DownloadManagerData.Download InstallData => (DownloadManagerData.Download)DataContext;
        public string GameID => InstallData.gameID;

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
                UpdateSpaceInfo(path);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        public void Install()
        {
            var settings = LegendaryLibrary.GetSettings();
            var installPath = SelectedGamePathTxt.Text;
            if (installPath == "")
            {
                installPath = LegendaryLauncher.GamesInstallationPath;
            }
            var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            if (installPath.Contains(playniteDirectoryVariable))
            {
                installPath = installPath.Replace(playniteDirectoryVariable, playniteAPI.Paths.ApplicationPath);
            }
            if (!Helpers.IsDirectoryWritable(installPath))
            {
                return;
            }
            InstallerWindow.Close();
            LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
            var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == GameID);
            if (wantedItem != null)
            {
                playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), wantedItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                var downloadProperties = GetDownloadProperties(DownloadAction.Install, installPath);
                InstallData.downloadProperties = downloadProperties;
                if (!downloadSizeWithoutDlcs.IsNullOrEmpty())
                {
                    InstallData.downloadSize = downloadSizeWithoutDlcs;
                }
                if (!installSizeWithoutDlcs.IsNullOrEmpty())
                {
                    InstallData.installSize = installSizeWithoutDlcs;
                }
                var downloadTasks = new List<DownloadManagerData.Download>
                {
                    InstallData
                };

                if (ExtraContentLB.Items.Count > 0)
                {
                    foreach (var selectedOption in ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList())
                    {
                        if (selectedOption.Value.Is_dlc)
                        {
                            var wantedDlcItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == selectedOption.Key);
                            if (wantedDlcItem != null)
                            {
                                playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), wantedDlcItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                            else
                            {
                                var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
                                var cacheDlcInfoFile = Path.Combine(cacheInfoPath, selectedOption.Key + ".json");
                                var dlcDownloadSize = "0";
                                var dlcInstallSize = "0";
                                if (File.Exists(cacheDlcInfoFile))
                                {
                                    LegendaryGameInfo.Rootobject dlcManifest = new LegendaryGameInfo.Rootobject();
                                    var cacheDlcContent = FileSystem.ReadFileAsStringSafe(cacheDlcInfoFile);
                                    if (!cacheDlcContent.IsNullOrWhiteSpace() && Serialization.TryFromJson(cacheDlcContent, out dlcManifest))
                                    {
                                        if (dlcManifest != null && dlcManifest.Manifest != null)
                                        {
                                            dlcDownloadSize = Helpers.FormatSize(dlcManifest.Manifest.Download_size);
                                            dlcInstallSize = Helpers.FormatSize(dlcManifest.Manifest.Disk_size);
                                        }
                                    }
                                }
                                downloadTasks.Add(new DownloadManagerData.Download
                                {
                                    gameID = selectedOption.Key,
                                    name = selectedOption.Value.Name,
                                    downloadSize = dlcDownloadSize,
                                    installSize = dlcInstallSize,
                                    downloadProperties = downloadProperties
                                });
                            }
                        }
                    }
                }
                if (downloadTasks.Count > 0)
                {
                    downloadManager.EnqueueMultipleJobs(downloadTasks);
                }
            }
        }

        private void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            Install();
        }

        public DownloadProperties GetDownloadProperties(DownloadAction downloadAction, string installPath = "")
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
                var anyNonDlc = ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySDLInfo>>().FirstOrDefault(a => a.Value.Is_dlc == false);
                if (anyNonDlc.Key != null)
                {
                    selectedExtraContent.AddMissing("");
                    foreach (var selectedOption in ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList())
                    {
                        if (!selectedOption.Value.Is_dlc)
                        {
                            foreach (var tag in selectedOption.Value.Tags)
                            {
                                selectedExtraContent.AddMissing(tag);
                            }
                        }
                    }
                }
            }
            DownloadProperties downloadProperties = new DownloadProperties()
            {
                downloadAction = downloadAction,
                installPath = installPath,
                installPrerequisites = (bool)PrerequisitesChk.IsChecked,
                prerequisitesName = prereqName,
                enableReordering = (bool)ReorderingChk.IsChecked,
                ignoreFreeSpace = (bool)IgnoreFreeSpaceChk.IsChecked,
                maxWorkers = maxWorkers,
                maxSharedMemory = maxSharedMemory,
                extraContent = selectedExtraContent
            };
            return downloadProperties;
        }

        private async void LegendaryGameInstallerUC_Loaded(object sender, RoutedEventArgs e)
        {
            if (InstallData.downloadProperties.downloadAction == DownloadAction.Repair)
            {
                FolderDP.Visibility = Visibility.Collapsed;
                InstallBtn.Visibility = Visibility.Collapsed;
                RepairBtn.Visibility = Visibility.Visible;
                AfterInstallingSP.Visibility = Visibility.Collapsed;
            }
            var settings = LegendaryLibrary.GetSettings();
            var installPath = LegendaryLauncher.GamesInstallationPath;
            var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            if (installPath.Contains(playniteDirectoryVariable))
            {
                installPath = installPath.Replace(playniteDirectoryVariable, playniteAPI.Paths.ApplicationPath);
            }
            SelectedGamePathTxt.Text = installPath;
            ReorderingChk.IsChecked = settings.EnableReordering;
            MaxWorkersNI.Value = settings.MaxWorkers.ToString();
            MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
            UpdateSpaceInfo(installPath);
            requiredThings = new List<string>();
            var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
            var cacheInfoFile = Path.Combine(cacheInfoPath, GameID + ".json");
            if (!Directory.Exists(cacheInfoPath))
            {
                Directory.CreateDirectory(cacheInfoPath);
            }
            manifest = await LegendaryLauncher.GetGameInfo(GameID);
            if (manifest != null && manifest.Manifest != null && manifest.Game != null)
            {
                if (manifest.Manifest.Prerequisites != null)
                {
                    if (manifest.Manifest.Prerequisites.ids != null && manifest.Manifest.Prerequisites.ids.Length > 0 && !manifest.Manifest.Prerequisites.path.IsNullOrEmpty())
                    {
                        PrerequisitesChk.IsChecked = true;
                        PrerequisitesChk.Visibility = Visibility.Visible;
                        if (!manifest.Manifest.Prerequisites.name.IsNullOrEmpty())
                        {
                            prereqName = manifest.Manifest.Prerequisites.name;
                        }
                        else
                        {
                            prereqName = Path.GetFileName(manifest.Manifest.Prerequisites.path);
                        }
                        PrerequisitesChk.Content = string.Format(PrerequisitesChk.Content.ToString(), prereqName);
                        if (manifest.Manifest.Prerequisites.ids.Contains("uplay"))
                        {
                            var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                                  .WithArguments(new[] { "install", GameID })
                                                  .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                                  .WithStandardInputPipe(PipeSource.FromString("n"))
                                                  .AddCommandToLog()
                                                  .WithValidation(CommandResultValidation.None)
                                                  .ExecuteBufferedAsync();
                            if (result.StandardOutput.Contains("Failure") && result.StandardOutput.Contains("Uplay"))
                            {
                                playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.LegendaryRequiredInstallViaThirdPartyLauncherError).Format("Ubisoft Connect")));
                                Window.GetWindow(this).Close();
                                return;
                            }
                            else if (result.StandardOutput.Contains("Uplay"))
                            {
                                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryRequiredInstallOfThirdPartyLauncher).Format("Ubisoft Connect"), "", MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        }
                    }
                }
                if (manifest.Manifest.Install_tags.Count > 1 || manifest.Game.Owned_dlc.Count > 0)
                {
                    Dictionary<string, LegendarySDLInfo> extraContentInfo = new Dictionary<string, LegendarySDLInfo>();
                    if (manifest.Manifest.Install_tags.Count > 1)
                    {
                        downloadSizeNumber = 0;
                        installSizeNumber = 0;
                        var cacheSDLPath = LegendaryLibrary.Instance.GetCachePath("sdlcache");
                        var cacheSDLFile = Path.Combine(cacheSDLPath, GameID + ".json");
                        string content = null;
                        if (File.Exists(cacheSDLFile))
                        {
                            if (File.GetLastWriteTime(cacheSDLFile) < DateTime.Now.AddDays(-7))
                            {
                                File.Delete(cacheSDLFile);
                            }
                        }
                        if (!File.Exists(cacheSDLFile))
                        {
                            var httpClient = new HttpClient();
                            var response = await httpClient.GetAsync("https://api.legendary.gl/v1/sdl/" + GameID + ".json");
                            if (response.IsSuccessStatusCode)
                            {
                                content = await response.Content.ReadAsStringAsync();
                                if (!Directory.Exists(cacheSDLPath))
                                {
                                    Directory.CreateDirectory(cacheSDLPath);
                                }
                                File.WriteAllText(cacheSDLFile, content);
                            }
                            httpClient.Dispose();
                        }
                        else
                        {
                            content = FileSystem.ReadFileAsStringSafe(cacheSDLFile);
                        }
                        bool correctSdlJson = false;
                        if (!content.IsNullOrWhiteSpace() && Serialization.TryFromJson(content, out extraContentInfo))
                        {
                            correctSdlJson = true;
                            if (extraContentInfo.ContainsKey("__required"))
                            {
                                foreach (var tag in extraContentInfo["__required"].Tags)
                                {
                                    foreach (var tagDo in manifest.Manifest.Tag_download_size)
                                    {
                                        if (tagDo.Tag == tag)
                                        {
                                            downloadSizeNumber += tagDo.Size;
                                            break;
                                        }
                                    }
                                    foreach (var tagDi in manifest.Manifest.Tag_disk_size)
                                    {
                                        if (tagDi.Tag == tag)
                                        {
                                            installSizeNumber += tagDi.Size;
                                            break;
                                        }
                                    }
                                    requiredThings.Add(tag);
                                }
                                extraContentInfo.Remove("__required");
                            }
                            foreach (var tagDo in manifest.Manifest.Tag_download_size)
                            {
                                if (tagDo.Tag == "")
                                {
                                    downloadSizeNumber += tagDo.Size;
                                    break;
                                }
                            }
                            foreach (var tagDi in manifest.Manifest.Tag_disk_size)
                            {
                                if (tagDi.Tag == "")
                                {
                                    installSizeNumber += tagDi.Size;
                                    break;
                                }
                            }
                            InstallData.downloadSize = Helpers.FormatSize(downloadSizeNumber);
                            InstallData.installSize = Helpers.FormatSize(installSizeNumber);
                        }
                        else
                        {
                            logger.Error("An error occurred while reading SDL data.");
                        }
                        if (!correctSdlJson)
                        {
                            extraContentInfo = new Dictionary<string, LegendarySDLInfo>();
                        }
                        else
                        {
                            AllOrNothingChk.Visibility = Visibility.Visible;
                        }
                    }
                    if (manifest.Game.Owned_dlc.Count > 0)
                    {
                        foreach (var dlc in manifest.Game.Owned_dlc.OrderBy(obj => obj.Title))
                        {
                            if (!dlc.App_name.IsNullOrEmpty())
                            {
                                var dlcInfo = new LegendarySDLInfo
                                {
                                    Name = dlc.Title.RemoveTrademarks(),
                                    Is_dlc = true
                                };
                                extraContentInfo.Add(dlc.App_name, dlcInfo);
                                var dlcManifest = await LegendaryLauncher.GetGameInfo(dlc.App_name);
                            }
                        }
                        AllDlcsChk.Visibility = Visibility.Visible;
                    }
                    if (extraContentInfo.Keys.Count > 0)
                    {
                        ExtraContentLB.ItemsSource = extraContentInfo;
                        ExtraContentBrd.Visibility = Visibility.Visible;
                        if (InstallData.downloadProperties.downloadAction == DownloadAction.Repair)
                        {
                            string[] installedTags = default;
                            var installedAppList = LegendaryLauncher.GetInstalledAppList();
                            if (installedAppList != null)
                            {
                                if (installedAppList.ContainsKey(GameID))
                                {
                                    var installedGameData = installedAppList[GameID];
                                    if (installedGameData.Install_tags != null && installedGameData.Install_tags.Length > 1)
                                    {
                                        installedTags = installedGameData.Install_tags;
                                    }
                                }
                            }
                            foreach (KeyValuePair<string, LegendarySDLInfo> extraCheckbox in ExtraContentLB.Items)
                            {
                                if (extraCheckbox.Value.Tags.Count > 0)
                                {
                                    if (installedTags != null && installedTags.Length > 0 && installedTags.Contains(extraCheckbox.Value.Tags[0]))
                                    {
                                        ExtraContentLB.SelectedItems.Add(extraCheckbox);
                                    }
                                }
                            }
                        }
                    }
                }
                if (InstallData.downloadSize.IsNullOrEmpty() || InstallData.installSize.IsNullOrEmpty())
                {
                    if (manifest.Manifest != null)
                    {
                        downloadSizeNumber = manifest.Manifest.Download_size;
                        installSizeNumber = manifest.Manifest.Disk_size;
                        InstallData.downloadSize = Helpers.FormatSize(downloadSizeNumber);
                        InstallData.installSize = Helpers.FormatSize(installSizeNumber);
                    }
                }
                UpdateAfterInstallingSize();
                DownloadSizeTB.Text = InstallData.downloadSize;
                InstallSizeTB.Text = InstallData.installSize;
            }
            if (!InstallData.downloadSize.IsNullOrEmpty() && !InstallData.installSize.IsNullOrEmpty())
            {
                InstallBtn.IsEnabled = true;
            }
            else if (InstallData.downloadProperties.downloadAction != DownloadAction.Repair)
            {
                InstallerWindow.Close();
            }
            if (settings.UnattendedInstall && (InstallData.downloadProperties.downloadAction == DownloadAction.Install))
            {
                Install();
            }
        }

        private void UpdateSpaceInfo(string path)
        {
            DriveInfo dDrive = new DriveInfo(path);
            if (dDrive.IsReady)
            {
                availableFreeSpace = dDrive.AvailableFreeSpace;
                SpaceTB.Text = Helpers.FormatSize(availableFreeSpace);
            }
            UpdateAfterInstallingSize();
        }

        private void UpdateAfterInstallingSize()
        {
            double afterInstallSizeNumber = (double)(availableFreeSpace - installSizeNumber);
            if (installSizeNumberAfterMod != null)
            {
                afterInstallSizeNumber = (double)(availableFreeSpace - installSizeNumberAfterMod);
            }
            if (afterInstallSizeNumber < 0)
            {
                afterInstallSizeNumber = 0;
            }
            AfterInstallingTB.Text = Helpers.FormatSize(afterInstallSizeNumber);
        }

        private void ExtraContentLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            double allDlcsDownloadSizeNumber = 0;
            double allDlcsInstallSizeNumber = 0;
            var initialDownloadSizeNumber = downloadSizeNumber;
            var initialInstallSizeNumber = installSizeNumber;
            foreach (var selectedOption in ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList())
            {
                if (!selectedOption.Value.Is_dlc)
                {
                    foreach (var tag in selectedOption.Value.Tags)
                    {
                        foreach (var tagDo in manifest.Manifest.Tag_download_size)
                        {
                            if (tagDo.Tag == tag)
                            {
                                initialDownloadSizeNumber += tagDo.Size;
                                break;
                            }
                        }
                        foreach (var tagDi in manifest.Manifest.Tag_disk_size)
                        {
                            if (tagDi.Tag == tag)
                            {
                                initialInstallSizeNumber += tagDi.Size;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    LegendaryGameInfo.Rootobject dlcManifest = new LegendaryGameInfo.Rootobject();
                    bool correctDlcJson = false;
                    var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
                    var cacheDlcInfoFile = Path.Combine(cacheInfoPath, selectedOption.Key + ".json");
                    if (File.Exists(cacheDlcInfoFile))
                    {
                        var cacheDlcContent = FileSystem.ReadFileAsStringSafe(cacheDlcInfoFile);
                        if (!cacheDlcContent.IsNullOrWhiteSpace() && Serialization.TryFromJson(cacheDlcContent, out dlcManifest))
                        {
                            if (dlcManifest != null && dlcManifest.Manifest != null)
                            {
                                correctDlcJson = true;
                            }
                        }
                    }
                    if (correctDlcJson)
                    {
                        initialDownloadSizeNumber += dlcManifest.Manifest.Download_size;
                        initialInstallSizeNumber += dlcManifest.Manifest.Disk_size;
                        allDlcsDownloadSizeNumber += dlcManifest.Manifest.Download_size;
                        allDlcsInstallSizeNumber += dlcManifest.Manifest.Disk_size;
                    }
                }
            }
            InstallData.downloadSize = Helpers.FormatSize(initialDownloadSizeNumber);
            DownloadSizeTB.Text = InstallData.downloadSize;
            InstallData.installSize = Helpers.FormatSize(initialInstallSizeNumber);
            InstallSizeTB.Text = InstallData.installSize;
            installSizeNumberAfterMod = initialInstallSizeNumber;
            UpdateAfterInstallingSize();
            double downloadSizeWithoutDlcsNumber = initialDownloadSizeNumber - allDlcsDownloadSizeNumber;
            downloadSizeWithoutDlcs = Helpers.FormatSize(downloadSizeWithoutDlcsNumber);
            double installSizeWithoutDlcsNumber = initialInstallSizeNumber - allDlcsInstallSizeNumber;
            installSizeWithoutDlcs = Helpers.FormatSize(installSizeWithoutDlcsNumber);
        }

        private void SetControlStyles()
        {
            var baseStyleName = "BaseTextBlockStyle";
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                baseStyleName = "TextBlockBaseStyle";
                Resources.Add(typeof(Button), new Style(typeof(Button), null));
            }

            if (ResourceProvider.GetResource(baseStyleName) is Style baseStyle && baseStyle.TargetType == typeof(TextBlock))
            {
                var implicitStyle = new Style(typeof(TextBlock), baseStyle);
                Resources.Add(typeof(TextBlock), implicitStyle);
            }
        }

        private void RepairBtn_Click(object sender, RoutedEventArgs e)
        {
            InstallerWindow.Close();
            LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
            var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == GameID);
            if (wantedItem != null)
            {
                playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), wantedItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                InstallData.downloadProperties = GetDownloadProperties(DownloadAction.Repair);
                if (!downloadSizeWithoutDlcs.IsNullOrEmpty())
                {
                    InstallData.downloadSize = downloadSizeWithoutDlcs;
                }
                if (!installSizeWithoutDlcs.IsNullOrEmpty())
                {
                    InstallData.installSize = installSizeWithoutDlcs;
                }
                downloadManager.EnqueueJob(InstallData);
            }
        }

        private void AllDlcsChk_Checked(object sender, RoutedEventArgs e)
        {
            var dlcs = ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySDLInfo>>().Where(x => x.Value.Is_dlc).ToList();
            foreach (var dlc in dlcs)
            {
                ExtraContentLB.SelectedItems.Add(dlc);
            }
        }

        private void AllDlcsChk_Unchecked(object sender, RoutedEventArgs e)
        {
            if (uncheckedByUser)
            {
                var dlcs = ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().Where(x => x.Value.Is_dlc).ToList();
                foreach (var dlc in dlcs)
                {
                    ExtraContentLB.SelectedItems.Remove(dlc);
                }
            }
        }

        private void ExtraContentLBChk_Unchecked(object sender, RoutedEventArgs e)
        {
            uncheckedByUser = false;
            if (AllOrNothingChk.IsChecked == true)
            {
                AllOrNothingChk.IsChecked = false;
            }
            var extraCheckbox = sender as CheckBox;
            if (AllDlcsChk.IsChecked == true && (bool)extraCheckbox.Tag)
            {
                AllDlcsChk.IsChecked = false;
            }
            uncheckedByUser = true;
        }

        private void AllOrNothingChk_Checked(object sender, RoutedEventArgs e)
        {
            ExtraContentLB.SelectAll();
        }

        private void AllOrNothingChk_Unchecked(object sender, RoutedEventArgs e)
        {
            if (uncheckedByUser)
            {
                ExtraContentLB.SelectedItems.Clear();
            }
        }
    }
}
