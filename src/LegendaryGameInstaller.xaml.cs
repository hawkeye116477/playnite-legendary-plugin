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
        public long availableFreeSpace;
        private LegendaryGameInfo.Rootobject manifest;
        private bool uncheckedByUser = true;
        private bool checkedByUser = true;
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

        public async Task StartTask(DownloadAction downloadAction)
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
                    var downloadProperties = GetDownloadProperties(installData, downloadAction, installPath);
                    installData.downloadProperties = downloadProperties;
                    downloadTasks.Add(installData);
                }
                var selectedDlcs = installData.downloadProperties.selectedDlcs;
                if (selectedDlcs != null && selectedDlcs.Count > 0)
                {
                    foreach (var selectedDlc in selectedDlcs)
                    {
                        var wantedDlc = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == selectedDlc.Key);
                        if (wantedDlc == null)
                        {
                            var dlcInstallData = selectedDlc.Value;
                            var downloadProperties = GetDownloadProperties(dlcInstallData, downloadAction, installPath);
                            dlcInstallData.downloadProperties = downloadProperties;
                            downloadTasks.Add(dlcInstallData);
                        }
                        else
                        {
                            downloadItemsAlreadyAdded.Add(selectedDlc.Value.name);
                        }
                    }
                }
                installData.extraContentAvailable = null;
                installData.downloadProperties.selectedDlcs = null;
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
            await StartTask(DownloadAction.Install);
        }

        public DownloadProperties GetDownloadProperties(DownloadManagerData.Download installData, DownloadAction downloadAction, string installPath = "")
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
            installData.downloadProperties.downloadAction = downloadAction;
            installData.downloadProperties.installPath = installPath;
            installData.downloadProperties.installPrerequisites = (bool)PrerequisitesChk.IsChecked;
            installData.downloadProperties.prerequisitesName = prereqName;
            installData.downloadProperties.enableReordering = (bool)ReorderingChk.IsChecked;
            installData.downloadProperties.ignoreFreeSpace = (bool)IgnoreFreeSpaceChk.IsChecked;
            installData.downloadProperties.maxWorkers = maxWorkers;
            installData.downloadProperties.maxSharedMemory = maxSharedMemory;
            installData.extraContentAvailable = null;
            return installData.downloadProperties;
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
            MaxWorkersNI.MaxValue = Helpers.CpuThreadsNumber;
            MaxWorkersNI.Value = settings.MaxWorkers.ToString();
            MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
            UpdateSpaceInfo(installPath);
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

            bool gamesListShouldBeDisplayed = false;

            foreach (var installData in MultiInstallData.ToList())
            {
                var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == installData.gameID);
                if (wantedItem != null)
                {
                    downloadItemsAlreadyAdded.Add(installData.name);
                    MultiInstallData.Remove(installData);
                    continue;
                }
                var requiredTags = await LegendaryLauncher.GetRequiredSdlsTags(installData);
                if (requiredTags.Count > 0)
                {
                    foreach (var requiredTag in requiredTags)
                    {
                        installData.downloadProperties.extraContent.AddMissing(requiredTag);
                    }
                }
                var gameData = new LegendaryGameInfo.Game
                {
                    Title = installData.name,
                    App_name = installData.gameID,
                };
                manifest = await LegendaryLauncher.GetGameInfo(gameData);
                if (manifest != null && manifest.Manifest != null && manifest.Game != null)
                {
                    Dictionary<string, LegendarySDLInfo> extraContentInfo = await LegendaryLauncher.GetExtraContentInfo(installData);
                    if (extraContentInfo.Count > 0)
                    {
                        installData.extraContentAvailable = true;
                    }
                    if (settings.DownloadAllDlcs)
                    {
                        var dlcs = extraContentInfo.Where(i => i.Value.Is_dlc).ToList();
                        if (dlcs.Count > 0)
                        {
                            installData.downloadProperties.selectedDlcs = new Dictionary<string, DownloadManagerData.Download>();
                            foreach (var dlc in dlcs)
                            {
                                var dlcInstallData = new DownloadManagerData.Download
                                {
                                    gameID = dlc.Key,
                                    name = dlc.Value.Name
                                };
                                if (installData.downloadProperties.extraContent.Count > 0)
                                {
                                    Dictionary<string, LegendarySDLInfo> extraContentDlcInfo = await LegendaryLauncher.GetExtraContentInfo(dlcInstallData, true);
                                    if (extraContentDlcInfo.Count > 0)
                                    {
                                        dlcInstallData.downloadProperties.extraContent = new List<string>
                                        {
                                            ""
                                        };
                                        foreach (var singleSdl in installData.downloadProperties.extraContent)
                                        {
                                            if (extraContentDlcInfo.ContainsKey(singleSdl))
                                            {
                                                dlcInstallData.downloadProperties.extraContent.Add(singleSdl);
                                            }
                                        }
                                    }
                                }
                                var dlcSize = await CalculateGameSize(dlcInstallData);
                                dlcInstallData.downloadSizeNumber = dlcSize.Download_size;
                                dlcInstallData.installSizeNumber = dlcSize.Disk_size;
                                installData.downloadProperties.selectedDlcs.Add(dlcInstallData.gameID, dlcInstallData);
                            }
                        }
                    }
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
                                    continue;
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
                    gamesListShouldBeDisplayed = true;
                    continue;
                }
                var gameSize = await CalculateGameSize(installData);
                installData.downloadSizeNumber = gameSize.Download_size;
                installData.installSizeNumber = gameSize.Disk_size;
            }

            if (MultiInstallData.Count == 1)
            {
                if (!gamesListShouldBeDisplayed)
                {
                    Dictionary<string, LegendarySDLInfo> extraContentInfo = await LegendaryLauncher.GetExtraContentInfo(MultiInstallData[0]);
                    var dlcs = extraContentInfo.Where(i => i.Value.Is_dlc).ToList();
                    var sdls = extraContentInfo.Where(i => i.Value.Is_dlc == false).ToList();
                    if (dlcs.Count > 1)
                    {
                        AllDlcsChk.Visibility = Visibility.Visible;
                    }
                    if (sdls.Count > 1)
                    {
                        AllOrNothingChk.Visibility = Visibility.Visible;
                    }
                    if (extraContentInfo.Count > 0)
                    {
                        ExtraContentLB.ItemsSource = extraContentInfo;
                        ExtraContentBrd.Visibility = Visibility.Visible;
                        if (MultiInstallData[0].downloadProperties.extraContent.Count > 0)
                        {
                            foreach (var item in MultiInstallData[0].downloadProperties.extraContent)
                            {
                                var sdl = sdls.FirstOrDefault(i => i.Key == item);
                                if (sdl.Key != null)
                                {
                                    ExtraContentLB.SelectedItems.Add(sdl);
                                }
                            }
                        }
                        if (settings.DownloadAllDlcs && MultiInstallData[0].downloadProperties.downloadAction == DownloadAction.Install)
                        {
                            foreach (var item in ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySDLInfo>>())
                            {
                                if (item.Value.Is_dlc)
                                {
                                    ExtraContentLB.SelectedItems.Add(item);
                                }
                            }
                        }
                    }
                }
            }

            if (prerequisites.Count > 0)
            {
                PrerequisitesChk.IsChecked = true;
                string prerequisitesCombined = string.Join(", ", prerequisites.Select(item => item.Key.ToString()));
                PrerequisitesChk.Content = string.Format(PrerequisitesChk.Content.ToString(), prerequisitesCombined);
                PrerequisitesChk.Visibility = Visibility.Visible;
            }

            var games = MultiInstallData;
            GamesLB.ItemsSource = games;
            if (games.Count > 1 || gamesListShouldBeDisplayed)
            {
                GamesBrd.Visibility = Visibility.Visible;
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

            CalculateTotalSize();

            if (MultiInstallData.Count <= 0)
            {
                InstallerWindow.Close();
                return;
            }
            if (downloadSizeNumber != 0 && installSizeNumber != 0)
            {
                InstallBtn.IsEnabled = true;
                RepairBtn.IsEnabled = true;
            }
            else if (MultiInstallData.First().downloadProperties.downloadAction != DownloadAction.Repair)
            {
                InstallerWindow.Close();
            }
            if (settings.UnattendedInstall && (MultiInstallData.First().downloadProperties.downloadAction == DownloadAction.Install))
            {
                await StartTask(DownloadAction.Install);
            }
        }

        private async Task<LegendaryGameInfo.Manifest> CalculateGameSize(DownloadManagerData.Download installData)
        {
            var gameData = new LegendaryGameInfo.Game
            {
                App_name = installData.gameID,
                Title = installData.name
            };
            var gameManifest = await LegendaryLauncher.GetGameInfo(gameData);
            var size = new LegendaryGameInfo.Manifest
            {
                Disk_size = 0,
                Download_size = 0
            };
            size.Disk_size = gameManifest.Manifest.Disk_size;
            size.Download_size = gameManifest.Manifest.Download_size;
            if (installData.downloadProperties.extraContent.Count > 0)
            {
                size.Disk_size = 0;
                size.Download_size = 0;
                foreach (var tag in installData.downloadProperties.extraContent)
                {
                    var tagDo = gameManifest.Manifest.Tag_download_size.FirstOrDefault(t => t.Tag == tag);
                    if (tagDo != null)
                    {
                        size.Download_size += tagDo.Size;
                    }
                    var tagDi = gameManifest.Manifest.Tag_disk_size.FirstOrDefault(t => t.Tag == tag);
                    if (tagDi != null)
                    {
                        size.Disk_size += tagDi.Size;
                    }
                }
            }
            return size;
        }

        private void CalculateTotalSize()
        {
            downloadSizeNumber = 0;
            installSizeNumber = 0;
            foreach (var installData in MultiInstallData)
            {
                var selectedDlcs = installData.downloadProperties.selectedDlcs;
                if (selectedDlcs != null && selectedDlcs.Count > 0)
                {
                    foreach (var selectedDlc in selectedDlcs)
                    {
                        downloadSizeNumber += selectedDlc.Value.downloadSizeNumber;
                        installSizeNumber += selectedDlc.Value.installSizeNumber;
                    }
                }
                downloadSizeNumber += installData.downloadSizeNumber;
                installSizeNumber += installData.installSizeNumber;
            }
            if (downloadSizeNumber != 0 && installSizeNumber != 0)
            {
                InstallBtn.IsEnabled = true;
                RepairBtn.IsEnabled = true;
            }
            UpdateAfterInstallingSize();
            DownloadSizeTB.Text = Helpers.FormatSize(downloadSizeNumber);
            InstallSizeTB.Text = Helpers.FormatSize(installSizeNumber);
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
            if (afterInstallSizeNumber < 0)
            {
                afterInstallSizeNumber = 0;
            }
            AfterInstallingTB.Text = Helpers.FormatSize(afterInstallSizeNumber);
        }

        private async void ExtraContentLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RepairBtn.IsEnabled = false;
            InstallBtn.IsEnabled = false;
            var selectedExtraContent = ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList();
            var selectedDLCs = selectedExtraContent.Where(i => i.Value.Is_dlc).ToList();
            var sdls = selectedExtraContent.Where(i => i.Value.Is_dlc == false).ToList();

            var selectedSdls = new List<string>();

            if (sdls.Count > 0)
            {
                foreach (var sdl in sdls)
                {
                    selectedSdls.AddMissing(sdl.Key);
                }
            }

            var requiredTags = await LegendaryLauncher.GetRequiredSdlsTags(MultiInstallData[0]);
            if (requiredTags.Count > 0)
            {
                foreach (var requiredTag in requiredTags)
                {
                    selectedSdls.AddMissing(requiredTag);
                }
            }
            MultiInstallData[0].downloadProperties.extraContent = selectedSdls;
            var gameData = new LegendaryGameInfo.Game
            {
                App_name = MultiInstallData[0].gameID,
                Title = MultiInstallData[0].name,
            };
            var gameSize = await CalculateGameSize(MultiInstallData[0]);
            MultiInstallData[0].downloadSizeNumber = gameSize.Download_size;
            MultiInstallData[0].installSizeNumber = gameSize.Disk_size;


            MultiInstallData[0].downloadProperties.selectedDlcs = new Dictionary<string, DownloadManagerData.Download>();
            if (selectedDLCs.Count > 0)
            {
                foreach (var dlc in selectedDLCs)
                {
                    var dlcInstallData = new DownloadManagerData.Download
                    {
                        gameID = dlc.Key,
                        name = dlc.Value.Name,
                    };
                    if (MultiInstallData[0].downloadProperties.extraContent.Count > 0)
                    {
                        Dictionary<string, LegendarySDLInfo> extraContentDlcInfo = await LegendaryLauncher.GetExtraContentInfo(dlcInstallData, true);
                        if (extraContentDlcInfo.Count > 0)
                        {
                            dlcInstallData.downloadProperties.extraContent = new List<string>
                            {
                                ""
                            };
                            foreach (var singleSdl in MultiInstallData[0].downloadProperties.extraContent)
                            {
                                if (extraContentDlcInfo.ContainsKey(singleSdl))
                                {
                                    dlcInstallData.downloadProperties.extraContent.Add(singleSdl);
                                }
                            }
                        }
                    }
                    var dlcSize = await CalculateGameSize(dlcInstallData);
                    dlcInstallData.downloadSizeNumber = dlcSize.Download_size;
                    dlcInstallData.installSizeNumber = dlcSize.Disk_size;
                    MultiInstallData[0].downloadProperties.selectedDlcs.Add(dlcInstallData.gameID, dlcInstallData);
                }
            }

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
            var allDLCs = ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySDLInfo>>().Where(i => i.Value.Is_dlc).ToList();
            if (AllDlcsChk.IsChecked == true && selectedDLCs.Count() != allDLCs.Count)
            {
                uncheckedByUser = false;
                AllDlcsChk.IsChecked = false;
                uncheckedByUser = true;
            }
            if (AllDlcsChk.IsChecked == false && selectedDLCs.Count() == allDLCs.Count)
            {
                checkedByUser = false;
                AllDlcsChk.IsChecked = true;
                checkedByUser = true;
            }
            CalculateTotalSize();
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
            await StartTask(DownloadAction.Repair);
        }

        private void AllDlcsChk_Checked(object sender, RoutedEventArgs e)
        {
            if (checkedByUser)
            {
                var dlcs = ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySDLInfo>>().Where(x => x.Value.Is_dlc).ToList();
                foreach (var dlc in dlcs)
                {
                    ExtraContentLB.SelectedItems.Add(dlc);
                }
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

        private async void GameExtraContentBtn_Click(object sender, RoutedEventArgs e)
        {
            var selectedGame = ((Button)sender).DataContext as DownloadManagerData.Download;
            var playniteAPI = API.Instance;
            Window window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false,
            });
            window.DataContext = selectedGame;
            window.Content = new LegendaryExtraInstallationContentView();
            window.Owner = InstallerWindow;
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.MinWidth = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Title = selectedGame.name;
            var result = window.ShowDialog();
            if (result == false)
            {
                RepairBtn.IsEnabled = false;
                InstallBtn.IsEnabled = false;
                foreach (var installData in MultiInstallData)
                {
                    var gameInfo = new LegendaryGameInfo.Game
                    {
                        App_name = installData.gameID,
                        Title = installData.name
                    };
                    manifest = await LegendaryLauncher.GetGameInfo(gameInfo);
                    var gameSize = await CalculateGameSize(installData);
                    installData.downloadSizeNumber = gameSize.Download_size;
                    installData.installSizeNumber = gameSize.Disk_size;
                    var selectedDlcs = installData.downloadProperties.selectedDlcs;
                    if (selectedDlcs != null && selectedDlcs.Count > 0)
                    {
                        foreach (var selectedDlc in selectedDlcs)
                        {
                            var dlcInstallData = selectedDlc.Value;
                            if (installData.downloadProperties.extraContent.Count > 0)
                            {
                                Dictionary<string, LegendarySDLInfo> extraContentDlcInfo = await LegendaryLauncher.GetExtraContentInfo(dlcInstallData, true);
                                if (extraContentDlcInfo.Count > 0)
                                {
                                    dlcInstallData.downloadProperties.extraContent = new List<string>
                                    {
                                        ""
                                    };
                                    foreach (var singleSdl in installData.downloadProperties.extraContent)
                                    {
                                        if (extraContentDlcInfo.ContainsKey(singleSdl))
                                        {
                                            dlcInstallData.downloadProperties.extraContent.Add(singleSdl);
                                        }
                                    }
                                }
                            }
                            var dlcSize = await CalculateGameSize(dlcInstallData);
                            selectedDlc.Value.downloadSizeNumber = dlcSize.Download_size;
                            selectedDlc.Value.installSizeNumber = dlcSize.Disk_size;
                        }
                    }

                }
                CalculateTotalSize();
            }
        }
    }
}
