using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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

        public List<DownloadManagerData.Download> MultiInstallData
        {
            get => (List<DownloadManagerData.Download>)DataContext;
            set { }
        }

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

        public async Task Install()
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
            var downloadTasks = new List<DownloadManagerData.Download>();
            var downloadItemsAlreadyAdded = new List<string>();
            foreach (var installData in MultiInstallData)
            {
                var gameId = installData.gameID;
                var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == gameId);
                if (wantedItem == null)
                {
                    var downloadProperties = GetDownloadProperties(DownloadAction.Install, installPath);
                    installData.downloadProperties = downloadProperties;
                    downloadTasks.Add(installData);
                    if (ExtraContentLB.Items.Count > 0)
                    {
                        foreach (var selectedOption in ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList())
                        {
                            if (selectedOption.Value.Is_dlc)
                            {
                                var wantedDlcItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == selectedOption.Key);
                                if (wantedDlcItem != null)
                                {
                                    downloadItemsAlreadyAdded.Add(wantedDlcItem.name);
                                }
                                else
                                {
                                    var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
                                    var cacheDlcInfoFile = Path.Combine(cacheInfoPath, selectedOption.Key + ".json");
                                    double dlcDownloadSizeNumber = 0;
                                    double dlcInstallSizeNumber = 0;
                                    if (File.Exists(cacheDlcInfoFile))
                                    {
                                        LegendaryGameInfo.Rootobject dlcManifest = new LegendaryGameInfo.Rootobject();
                                        var cacheDlcContent = FileSystem.ReadFileAsStringSafe(cacheDlcInfoFile);
                                        if (!cacheDlcContent.IsNullOrWhiteSpace() && Serialization.TryFromJson(cacheDlcContent, out dlcManifest))
                                        {
                                            if (dlcManifest != null && dlcManifest.Manifest != null)
                                            {
                                                dlcDownloadSizeNumber = dlcManifest.Manifest.Download_size;
                                                dlcInstallSizeNumber = dlcManifest.Manifest.Disk_size;
                                            }
                                        }
                                    }
                                    downloadTasks.Add(new DownloadManagerData.Download
                                    {
                                        gameID = selectedOption.Key,
                                        name = selectedOption.Value.Name,
                                        downloadSizeNumber = dlcDownloadSizeNumber,
                                        installSizeNumber = dlcInstallSizeNumber,
                                        downloadProperties = downloadProperties
                                    });
                                }
                            }
                        }
                    }
                }
            }
            if (downloadItemsAlreadyAdded.Count > 0)
            {
                if (downloadItemsAlreadyAdded.Count == 1)
                {
                    playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), downloadItemsAlreadyAdded[0]), "", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    string downloadItemsAlreadyAddedComnined = string.Join(", ", downloadItemsAlreadyAdded.Select(item => item.ToString()));
                    playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExistsOther), downloadItemsAlreadyAddedComnined), "", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            if (downloadTasks.Count > 0)
            {
                await downloadManager.EnqueueMultipleJobs(downloadTasks);
            }
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            await Install();
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
            if (MultiInstallData.First().downloadProperties.downloadAction == DownloadAction.Repair)
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
            if (!Directory.Exists(cacheInfoPath))
            {
                Directory.CreateDirectory(cacheInfoPath);
            }

            var ubisoftOnlyGames = new List<string>();
            var ubisoftRecommendedGames = new List<string>();
            var downloadItemsAlreadyAdded = new List<string>();
            var prerequisites = new Dictionary<string, string>();
            LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();

            foreach (var installData in MultiInstallData.ToList())
            {
                var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == installData.gameID);
                if (wantedItem != null)
                {
                    MultiInstallData.Remove(installData);
                    downloadItemsAlreadyAdded.Add(installData.name);
                    continue;
                }
                manifest = await LegendaryLauncher.GetGameInfo(installData.gameID);
                if (manifest != null && manifest.Manifest != null && manifest.Game != null)
                {
                    if (manifest.Manifest.Prerequisites != null)
                    {
                        if (manifest.Manifest.Prerequisites.ids != null && manifest.Manifest.Prerequisites.ids.Length > 0 && !manifest.Manifest.Prerequisites.path.IsNullOrEmpty())
                        {
                            if (!manifest.Manifest.Prerequisites.name.IsNullOrEmpty())
                            {
                                prereqName = manifest.Manifest.Prerequisites.name;
                            }
                            else
                            {
                                prereqName = Path.GetFileName(manifest.Manifest.Prerequisites.path);
                            }
                            prerequisites.Add(prereqName, "");
                            if (manifest.Manifest.Prerequisites.ids.Contains("uplay"))
                            {
                                var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                                          .WithArguments(new[] { "install", installData.gameID })
                                                          .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                                          .WithStandardInputPipe(PipeSource.FromString("n"))
                                                          .AddCommandToLog()
                                                          .WithValidation(CommandResultValidation.None)
                                                          .ExecuteBufferedAsync();
                                if (result.StandardOutput.Contains("Failure") && result.StandardOutput.Contains("Uplay"))
                                {
                                    ubisoftOnlyGames.Add(installData.name);
                                    MultiInstallData.Remove(installData);
                                }
                                else if (result.StandardOutput.Contains("Uplay"))
                                {
                                    ubisoftRecommendedGames.Add(installData.name);
                                }
                            }
                        }
                    }
                }
                else
                {
                    MultiInstallData.Remove(installData);
                }
            }

            if (prerequisites.Count > 0)
            {
                PrerequisitesChk.IsChecked = true;
                string prerequisitesCombined = string.Join(", ", prerequisites.Select(item => item.Key.ToString()));
                PrerequisitesChk.Content = string.Format(PrerequisitesChk.Content.ToString(), prerequisitesCombined);
                PrerequisitesChk.Visibility = Visibility.Visible;
            }

            Dictionary<string, LegendarySDLInfo> multipleExtraContentInfo = new Dictionary<string, LegendarySDLInfo>();
            downloadSizeNumber = 0;
            installSizeNumber = 0;
            foreach (var installData in MultiInstallData)
            {
                manifest = await LegendaryLauncher.GetGameInfo(installData.gameID);
                if (manifest != null && manifest.Manifest != null && manifest.Game != null)
                {
                    if (installData.downloadSizeNumber == 0 || installData.installSizeNumber == 0)
                    {
                        installData.downloadSizeNumber = manifest.Manifest.Download_size;
                        installData.installSizeNumber = manifest.Manifest.Disk_size;
                    }
                    if (manifest.Manifest.Install_tags.Count > 1 || manifest.Game.Owned_dlc.Count > 0)
                    {
                        Dictionary<string, LegendarySDLInfo> extraContentInfo = new Dictionary<string, LegendarySDLInfo>();
                        if (manifest.Manifest.Install_tags.Count > 1)
                        {
                            double singleDownloadSizeNumber = 0;
                            double singleInstallSizeNumber = 0;
                            var cacheSDLPath = LegendaryLibrary.Instance.GetCachePath("sdlcache");
                            var cacheSDLFile = Path.Combine(cacheSDLPath, installData.gameID + ".json");
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
                                var response = await httpClient.GetAsync("https://api.legendary.gl/v1/sdl/" + installData.gameID + ".json");
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
                                foreach (var sdl in extraContentInfo)
                                {
                                    sdl.Value.BaseGameID = installData.gameID;
                                }
                                if (extraContentInfo.ContainsKey("__required"))
                                {
                                    foreach (var tag in extraContentInfo["__required"].Tags)
                                    {
                                        foreach (var tagDo in manifest.Manifest.Tag_download_size)
                                        {
                                            if (tagDo.Tag == tag)
                                            {
                                                singleDownloadSizeNumber += tagDo.Size;
                                                break;
                                            }
                                        }
                                        foreach (var tagDi in manifest.Manifest.Tag_disk_size)
                                        {
                                            if (tagDi.Tag == tag)
                                            {
                                                singleInstallSizeNumber += tagDi.Size;
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
                                        singleDownloadSizeNumber += tagDo.Size;
                                        break;
                                    }
                                }
                                foreach (var tagDi in manifest.Manifest.Tag_disk_size)
                                {
                                    if (tagDi.Tag == "")
                                    {
                                        singleInstallSizeNumber += tagDi.Size;
                                        break;
                                    }
                                }
                                installData.downloadSizeNumber = singleDownloadSizeNumber;
                                installData.installSizeNumber = singleInstallSizeNumber;
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
                                if (AllOrNothingChk.Visibility != Visibility.Visible)
                                {
                                    AllOrNothingChk.Visibility = Visibility.Visible;
                                }
                            }
                        }
                        if (manifest.Game.Owned_dlc.Count > 0)
                        {
                            foreach (var dlc in manifest.Game.Owned_dlc.OrderBy(obj => obj.Title))
                            {
                                if (!dlc.App_name.IsNullOrEmpty())
                                {
                                    var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == dlc.Id);
                                    if (wantedItem != null)
                                    {
                                        downloadItemsAlreadyAdded.Add(installData.name);
                                    }
                                    else
                                    {
                                        var dlcInfo = new LegendarySDLInfo
                                        {
                                            Name = dlc.Title.RemoveTrademarks(),
                                            Is_dlc = true,
                                            BaseGameID = installData.gameID
                                        };
                                        extraContentInfo.Add(dlc.App_name, dlcInfo);
                                        var dlcManifest = await LegendaryLauncher.GetGameInfo(dlc.App_name);
                                    }

                                }
                            }
                            if (AllDlcsChk.Visibility != Visibility.Visible)
                            {
                                AllDlcsChk.Visibility = Visibility.Visible;
                            }
                        }
                        if (extraContentInfo.Keys.Count > 0)
                        {
                            foreach (var extraContent in extraContentInfo)
                            {
                                if (!multipleExtraContentInfo.ContainsKey(extraContent.Key))
                                {
                                    multipleExtraContentInfo.Add(extraContent.Key, extraContent.Value);
                                }
                            }
                        }
                    }
                    downloadSizeNumber += installData.downloadSizeNumber;
                    installSizeNumber += installData.installSizeNumber;
                }
            }

            GamesLB.ItemsSource = MultiInstallData;
            if (MultiInstallData.Count > 1)
            {
                GamesBrd.Visibility = Visibility.Visible;
            }

            UpdateAfterInstallingSize();
            DownloadSizeTB.Text = Helpers.FormatSize(downloadSizeNumber);
            InstallSizeTB.Text = Helpers.FormatSize(installSizeNumber);

            if (multipleExtraContentInfo.Keys.Count > 0)
            {
                ExtraContentLB.ItemsSource = multipleExtraContentInfo;
                ExtraContentBrd.Visibility = Visibility.Visible;
                foreach (var installData in MultiInstallData)
                {
                    if (installData.downloadProperties.downloadAction == DownloadAction.Repair)
                    {
                        string[] installedTags = default;
                        var installedAppList = LegendaryLauncher.GetInstalledAppList();
                        if (installedAppList != null)
                        {
                            if (installedAppList.ContainsKey(installData.gameID))
                            {
                                var installedGameData = installedAppList[installData.gameID];
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

            if (ubisoftOnlyGames.Count > 0)
            {
                if (ubisoftOnlyGames.Count == 1)
                {
                    playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.LegendaryRequiredInstallViaThirdPartyLauncherError).Format("Ubisoft Connect", ubisoftOnlyGames[0])));
                }
                else
                {
                    string ubisoftOnlyGamesCombined = string.Join(", ", ubisoftOnlyGames.Select(item => item.ToString()));
                    playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryRequiredInstallViaThirdPartyLauncherErrorOther).Format("Ubisoft Connect", ubisoftOnlyGamesCombined));
                }

            }
            if (ubisoftRecommendedGames.Count > 0)
            {
                if (ubisoftRecommendedGames.Count == 1)
                {
                    playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryRequiredInstallOfThirdPartyLauncher).Format("Ubisoft Connect", ubisoftRecommendedGames[0]), "", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string ubisoftRecommendedGamesCombined = string.Join(", ", ubisoftRecommendedGames.Select(item => item.ToString()));
                    playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryRequiredInstallOfThirdPartyLauncherOther).Format("Ubisoft Connect", ubisoftRecommendedGamesCombined), "", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }

            if (downloadItemsAlreadyAdded.Count > 0)
            {
                if (downloadItemsAlreadyAdded.Count == 1)
                {
                    playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), downloadItemsAlreadyAdded[0]), "", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                else
                {
                    string downloadItemsAlreadyAddedComnined = string.Join(", ", downloadItemsAlreadyAdded.Select(item => item.ToString()));
                    playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExistsOther), downloadItemsAlreadyAddedComnined), "", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            if (MultiInstallData.Count <= 0)
            {
                InstallerWindow.Close();
                return;
            }
            if (downloadSizeNumber != 0 && installSizeNumber != 0)
            {
                InstallBtn.IsEnabled = true;
            }
            else if (MultiInstallData.First().downloadProperties.downloadAction != DownloadAction.Repair)
            {
                InstallerWindow.Close();
            }
            if (AllDlcsChk.Visibility == Visibility.Visible && settings.DownloadAllDlcs)
            {
                AllDlcsChk.IsChecked = true;
            }
            if (settings.UnattendedInstall && (MultiInstallData.First().downloadProperties.downloadAction == DownloadAction.Install))
            {
                await Install();
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

            DownloadSizeTB.Text = Helpers.FormatSize(initialDownloadSizeNumber);
            InstallSizeTB.Text = Helpers.FormatSize(initialInstallSizeNumber);
            installSizeNumberAfterMod = initialInstallSizeNumber;
            UpdateAfterInstallingSize();
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

        private async void RepairBtn_Click(object sender, RoutedEventArgs e)
        {
            InstallerWindow.Close();
            LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
            var downloadTasks = new List<DownloadManagerData.Download>();
            foreach (var installData in MultiInstallData)
            {
                installData.downloadProperties = GetDownloadProperties(DownloadAction.Repair);
                downloadTasks.Add(installData);
            }
            if (downloadTasks.Count > 0)
            {
                await downloadManager.EnqueueMultipleJobs(downloadTasks);
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
