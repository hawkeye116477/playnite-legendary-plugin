using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Linguini.Shared.Types.Bundle;
using Playnite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using UnifiedDownloadManagerApiNS;
using UnifiedDownloadManagerApiNS.Models;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryGameInstaller.xaml
    /// </summary>
    public partial class LegendaryGameInstaller : UserControl
    {
        private ILogger logger = LogManager.GetLogger();
        private readonly IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
        public string InstallCommand;
        public List<string> RequiredThings;
        public double DownloadSizeNumber;
        public double InstallSizeNumber;
        public long AvailableFreeSpace;
        private LegendaryGameInfo.Rootobject manifest;
        private bool uncheckedByUser = true;
        private bool checkedByUser = true;
        public string PrereqName = "";
        private readonly CommonHelpers commonHelpers = LegendaryLibrary.Instance.CommonHelpers;

        public LegendaryGameInstaller()
        {
            InitializeComponent();
        }

        private Window? InstallerWindow => Window.GetWindow(this);

        public List<DownloadManagerData.Download> MultiInstallData
        {
            get => (List<DownloadManagerData.Download>)DataContext;
            set { }
        }

        private async void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = await playniteApi.Dialogs.SelectFolderAsync();
            if (result is { Count: > 0 } && result[0] != "")
            {
                SelectedGamePathTxt.Text = result[0];
                UpdateSpaceInfo(result[0]);
            }
        }

        public async Task StartTask(DownloadAction downloadAction, bool silently = false)
        {
            var installPath = SelectedGamePathTxt.Text;
            if (installPath == "")
            {
                installPath = LegendaryLauncher.GamesInstallationPath;
            }

            var playniteDirectoryVariable = ExpandableVariables.PlayniteDirectory.ToString();
            if (installPath.Contains(playniteDirectoryVariable))
            {
                installPath = installPath.Replace(playniteDirectoryVariable, playniteApi.AppInfo.ApplicationDirectory);
            }

            InstallerWindow.Close();


            var downloadTasks = new List<DownloadManagerData.Download>();


            var installedAppList = LegendaryLauncher.GetInstalledAppList();
            foreach (var installData in MultiInstallData)
            {
                var gameId = installData.GameId;
                if (!await commonHelpers.IsDirectoryWritable(installPath, LOC.CommonPermissionError))
                {
                    continue;
                }

                if (downloadAction != DownloadAction.Install)
                {
                    var installedInfo = installedAppList[gameId];
                    installPath = installedInfo.Install_path;
                    installData.FullInstallPath = installPath;
                }

                var downloadProperties = GetDownloadProperties(installData, downloadAction, installPath);
                installData.DownloadProperties = downloadProperties;


                var selectedDlcs = installData.DownloadProperties.SelectedDlcs;
                if (selectedDlcs is { Count: > 0 })
                {
                    foreach (var selectedDlc in selectedDlcs)
                    {
                        var dlcInstallData = selectedDlc.Value;
                        if (downloadAction == DownloadAction.Repair)
                        {
                            var installedInfo = installedAppList[selectedDlc.Key];
                            installPath = CommonHelpers.NormalizePath(installedInfo.Install_path);
                            dlcInstallData.FullInstallPath = installPath;
                        }

                        var dlcDownloadProperties = GetDownloadProperties(dlcInstallData, downloadAction, installPath);
                        dlcInstallData.DownloadProperties = dlcDownloadProperties;
                        downloadTasks.Add(dlcInstallData);
                    }
                }

                installData.ExtraContentAvailable = null;
                installData.DownloadProperties.SelectedDlcs = null;
                downloadTasks.Add(installData);
            }

            if (downloadTasks.Count > 0)
            {
                var legendaryDownloadLogic = new LegendaryDownloadLogic();
                await legendaryDownloadLogic.AddTasks(downloadTasks, silently);
            }
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            await StartTask(DownloadAction.Install);
        }

        public DownloadProperties GetDownloadProperties(
            DownloadManagerData.Download installData, DownloadAction downloadAction, string installPath = "")
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

            var newDownloadProperties = new DownloadProperties();
            newDownloadProperties = installData.DownloadProperties.GetClone();
            newDownloadProperties.DownloadAction = downloadAction;
            newDownloadProperties.InstallPath = installPath;
            newDownloadProperties.InstallPrerequisites = (bool)PrerequisitesChk.IsChecked;
            newDownloadProperties.PrerequisitesName = PrereqName;
            newDownloadProperties.EnableReordering = (bool)ReorderingChk.IsChecked;
            newDownloadProperties.IgnoreFreeSpace = (bool)IgnoreFreeSpaceChk.IsChecked;
            newDownloadProperties.MaxWorkers = maxWorkers;
            newDownloadProperties.MaxSharedMemory = maxSharedMemory;
            return newDownloadProperties;
        }

        private async void LegendaryGameInstallerUC_Loaded(object sender, RoutedEventArgs e)
        {
            bool shouldCloseWindow = false;
            if (!LegendaryLauncher.IsInstalled)
            {
                shouldCloseWindow = true;
                LegendaryLauncher.ShowNotInstalledError();
            }

            var isUdmInstalled = await LegendaryDownloadLogic.CheckIfUdmInstalled();
            if (!isUdmInstalled)
            {
                shouldCloseWindow = true;
            }

            if (shouldCloseWindow)
            {
                Window.GetWindow(this).Close();
                return;
            }

            commonHelpers.SetControlBackground(this);
            if (MultiInstallData.First().DownloadProperties.DownloadAction == DownloadAction.Repair)
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
                installPath = installPath.Replace(playniteDirectoryVariable, playniteApi.AppInfo.ApplicationDirectory);
            }

            SelectedGamePathTxt.Text = installPath;
            ReorderingChk.IsChecked = settings.EnableReordering;
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
            MaxWorkersNI.Value = settings.MaxWorkers.ToString();
            MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
            UpdateSpaceInfo(installPath);
            var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
            if (!Directory.Exists(cacheInfoPath))
            {
                Directory.CreateDirectory(cacheInfoPath);
            }

            await RefreshAll();
            if (settings.UnattendedInstall &&
                (MultiInstallData.First().DownloadProperties.DownloadAction == DownloadAction.Install))
            {
                await StartTask(DownloadAction.Install, true);
            }
        }

        public async Task RefreshAll()
        {
            UnifiedDownloadManagerApi unifiedDownloadManagerApi = new UnifiedDownloadManagerApi(playniteApi);
            ReloadBtn.IsEnabled = false;
            AllDlcsChk.Visibility = Visibility.Collapsed;
            AllOrNothingChk.Visibility = Visibility.Collapsed;
            ExtraContentBrd.Visibility = Visibility.Collapsed;
            PrerequisitesChk.Visibility = Visibility.Collapsed;
            GamesBrd.Visibility = Visibility.Collapsed;
            InstallBtn.IsEnabled = false;
            RepairBtn.IsEnabled = false;
            UpdateSpaceInfo(SelectedGamePathTxt.Text);

            var settings = LegendaryLibrary.GetSettings();

            var eaAppGames = new List<string>();
            var ubisoftOnlyGames = new List<string>();
            var ubisoftRecommendedGames = new List<string>();
            var prerequisites = new Dictionary<string, string>();

            bool gamesListShouldBeDisplayed = false;

            var pluginDownloadData = LegendaryLibrary.Instance.PluginDownloadData;
            var installedAppList = LegendaryLauncher.GetInstalledAppList();
            foreach (var installData in MultiInstallData.ToList())
            {
                var wantedItem = pluginDownloadData.Downloads.FirstOrDefault(item => item.GameId == installData.GameId);
                var wantedUnifiedTask =
                    unifiedDownloadManagerApi.GetTask(installData.GameId, LegendaryLibrary.PluginId);
                if (installData.DownloadProperties.DownloadAction == DownloadAction.Repair &&
                    installedAppList.ContainsKey(installData.GameId))
                {
                    var installedSdls = installedAppList[installData.GameId].Install_tags;
                    if (installedSdls.Count > 0)
                    {
                        installData.DownloadProperties.ExtraContent = installedSdls;
                    }
                }

                var requiredTags = await LegendaryLauncher.GetRequiredSdlsTags(installData);
                if (requiredTags.Count > 0)
                {
                    foreach (var requiredTag in requiredTags)
                    {
                        installData.DownloadProperties.ExtraContent.AddMissing(requiredTag);
                    }
                }

                var gameData = new LegendaryGameInfo.Game
                {
                    Title = installData.Name,
                    App_name = installData.GameId,
                };
                manifest = await LegendaryLauncher.GetGameInfo(gameData);
                if (manifest.Game != null)
                {
                    if (!manifest.Game.External_activation.IsNullOrEmpty() &&
                        (manifest.Game.External_activation.ToLower() == "origin" ||
                         manifest.Game.External_activation.ToLower() == "the ea app"))
                    {
                        eaAppGames.Add(installData.Name);
                        MultiInstallData.Remove(installData);
                        continue;
                    }
                }

                if (manifest != null && manifest.Manifest != null && manifest.Game != null && !manifest.errorDisplayed)
                {
                    Dictionary<string, LegendarySdlInfo> extraContentInfo =
                        await LegendaryLauncher.GetExtraContentInfo(installData);
                    if (extraContentInfo.Count > 0)
                    {
                        installData.ExtraContentAvailable = true;
                    }

                    if (installData.DownloadProperties.DownloadAction == DownloadAction.Repair)
                    {
                        var dlcs = extraContentInfo.Where(i => i.Value.Is_dlc).ToList();
                        if (dlcs.Count > 0)
                        {
                            installData.DownloadProperties.SelectedDlcs =
                                new Dictionary<string, DownloadManagerData.Download>();
                            foreach (var dlc in dlcs)
                            {
                                if (installedAppList.ContainsKey(dlc.Key))
                                {
                                    var dlcInstallData = new DownloadManagerData.Download
                                    {
                                        GameId = dlc.Key,
                                        Name = dlc.Value.Name
                                    };
                                    var dlcSize = await LegendaryLauncher.CalculateGameSize(dlcInstallData);
                                    dlcInstallData.DownloadSizeNumber = dlcSize.Download_size;
                                    dlcInstallData.InstallSizeNumber = dlcSize.Disk_size;
                                    installData.DownloadProperties.SelectedDlcs.Add(dlc.Key, dlcInstallData);
                                }
                            }
                        }
                    }

                    if (settings.DownloadAllDlcs &&
                        installData.DownloadProperties.DownloadAction == DownloadAction.Install)
                    {
                        var dlcs = extraContentInfo.Where(i => i.Value.Is_dlc).ToList();
                        if (dlcs.Count > 0)
                        {
                            installData.DownloadProperties.SelectedDlcs =
                                new Dictionary<string, DownloadManagerData.Download>();
                            foreach (var dlc in dlcs)
                            {
                                var dlcInstallData = new DownloadManagerData.Download
                                {
                                    GameId = dlc.Key,
                                    Name = dlc.Value.Name
                                };
                                if (installData.DownloadProperties.ExtraContent.Count > 0)
                                {
                                    Dictionary<string, LegendarySdlInfo> extraContentDlcInfo =
                                        await LegendaryLauncher.GetExtraContentInfo(dlcInstallData, true);
                                    if (extraContentDlcInfo.Count > 0)
                                    {
                                        dlcInstallData.DownloadProperties.ExtraContent = new List<string>
                                        {
                                            ""
                                        };
                                        foreach (var singleSdl in installData.DownloadProperties.ExtraContent)
                                        {
                                            if (extraContentDlcInfo.ContainsKey(singleSdl))
                                            {
                                                dlcInstallData.DownloadProperties.ExtraContent.Add(singleSdl);
                                            }
                                        }
                                    }
                                }

                                var dlcSize = await LegendaryLauncher.CalculateGameSize(dlcInstallData);
                                dlcInstallData.DownloadSizeNumber = dlcSize.Download_size;
                                dlcInstallData.InstallSizeNumber = dlcSize.Disk_size;
                                installData.DownloadProperties.SelectedDlcs.Add(dlcInstallData.GameId, dlcInstallData);
                            }
                        }
                    }

                    if (manifest.Manifest.Prerequisites != null)
                    {
                        if (manifest.Manifest.Prerequisites.ids != null &&
                            manifest.Manifest.Prerequisites.ids.Length > 0 &&
                            !manifest.Manifest.Prerequisites.path.IsNullOrEmpty())
                        {
                            if (!manifest.Manifest.Prerequisites.name.IsNullOrEmpty())
                            {
                                PrereqName = manifest.Manifest.Prerequisites.name;
                            }
                            else
                            {
                                PrereqName = Path.GetFileName(manifest.Manifest.Prerequisites.path);
                            }

                            if (!prerequisites.ContainsKey(PrereqName))
                            {
                                prerequisites.Add(PrereqName, "");
                            }

                            if (manifest.Manifest.Prerequisites.ids.Contains("uplay"))
                            {
                                var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                                      .WithArguments(new[] { "install", installData.GameId })
                                                      .WithEnvironmentVariables(await LegendaryLauncher
                                                          .GetDefaultEnvironmentVariables())
                                                      .WithStandardInputPipe(PipeSource.FromString("n"))
                                                      .AddCommandToLog()
                                                      .WithValidation(CommandResultValidation.None)
                                                      .ExecuteBufferedAsync();
                                if (result.StandardOutput.Contains("Failure") &&
                                    result.StandardOutput.Contains("Uplay"))
                                {
                                    ubisoftOnlyGames.AddMissing(installData.Name);
                                    MultiInstallData.Remove(installData);
                                    continue;
                                }
                                else if (result.StandardOutput.Contains("Uplay"))
                                {
                                    ubisoftRecommendedGames.AddMissing(installData.Name);
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

                var gameSize = await LegendaryLauncher.CalculateGameSize(installData);
                installData.DownloadSizeNumber = gameSize.Download_size;
                installData.InstallSizeNumber = gameSize.Disk_size;
            }

            if (MultiInstallData.Count == 1)
            {
                if (!gamesListShouldBeDisplayed)
                {
                    Dictionary<string, LegendarySdlInfo> extraContentInfo =
                        await LegendaryLauncher.GetExtraContentInfo(MultiInstallData[0]);
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
                        var selectedExtraContent = new Dictionary<string, LegendarySdlInfo>();
                        if (MultiInstallData[0].DownloadProperties.SelectedDlcs != null &&
                            MultiInstallData[0].DownloadProperties.SelectedDlcs.Count > 0)
                        {
                            var allExtraContent = ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySdlInfo>>();
                            foreach (var selectedDlc in MultiInstallData[0].DownloadProperties.SelectedDlcs)
                            {
                                var dlcItem = allExtraContent.FirstOrDefault(i => i.Key == selectedDlc.Key);
                                if (dlcItem.Key != null)
                                {
                                    var sdlInfo = new LegendarySdlInfo
                                    {
                                        Is_dlc = true,
                                    };
                                    selectedExtraContent.Add(selectedDlc.Key, sdlInfo);
                                }
                            }
                        }

                        if (MultiInstallData[0].DownloadProperties.ExtraContent.Count > 0)
                        {
                            foreach (var item in MultiInstallData[0].DownloadProperties.ExtraContent)
                            {
                                var sdlInfo = new LegendarySdlInfo
                                {
                                    Is_dlc = false,
                                };
                                selectedExtraContent.Add(item, sdlInfo);
                            }
                        }

                        if (selectedExtraContent.Count > 0)
                        {
                            foreach (var singleSelectedExtraContent in selectedExtraContent)
                            {
                                var selectedItem =
                                    extraContentInfo.FirstOrDefault(i => i.Key == singleSelectedExtraContent.Key);
                                if (selectedItem.Key != null)
                                {
                                    ExtraContentLB.SelectedItems.Add(selectedItem);
                                }
                            }
                        }

                        if (settings.DownloadAllDlcs && MultiInstallData[0].DownloadProperties.DownloadAction ==
                            DownloadAction.Install)
                        {
                            foreach (var item in ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySdlInfo>>())
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
                PrerequisitesChk.Content = PrerequisitesChk.Content.ToString()
                                                           .Replace("$prerequisiteName", prerequisitesCombined);
                PrerequisitesChk.Visibility = Visibility.Visible;
            }

            var games = MultiInstallData;
            GamesLB.ItemsSource = games;
            if (games.Count > 1 || gamesListShouldBeDisplayed)
            {
                GamesBrd.Visibility = Visibility.Visible;
            }

            if (eaAppGames.Count > 0)
            {
                var fluentEaArgs = new Dictionary<string, IFluentType>
                {
                    ["count"] = (FluentNumber)eaAppGames.Count
                };
                if (eaAppGames.Count == 1)
                {
                    fluentEaArgs["gameTitle"] = (FluentString)eaAppGames[0];
                }
                else
                {
                    string eaAppGamesCombined = string.Join(", ", eaAppGames.Select(item => item.ToString()));
                    fluentEaArgs["gameTitle"] = (FluentString)eaAppGamesCombined;
                }

                fluentEaArgs["thirdPartyLauncherName"] = (FluentString)"EA App";
                await playniteApi.Dialogs.ShowErrorMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.LegendaryRequiredInstallViaThirdPartyLauncherError,
                        fluentEaArgs));
            }

            if (ubisoftOnlyGames.Count > 0)
            {
                var fluentUbisoftArgs = new Dictionary<string, IFluentType>
                {
                    ["count"] = (FluentNumber)ubisoftOnlyGames.Count
                };
                if (ubisoftOnlyGames.Count == 1)
                {
                    fluentUbisoftArgs["gameTitle"] = (FluentString)ubisoftOnlyGames[0];
                }
                else
                {
                    string ubisoftOnlyGamesCombined =
                        string.Join(", ", ubisoftOnlyGames.Select(item => item.ToString()));
                    fluentUbisoftArgs["gameTitle"] = (FluentString)ubisoftOnlyGamesCombined;
                }

                fluentUbisoftArgs["thirdPartyLauncherName"] = (FluentString)"Ubisoft Connect";
                await playniteApi.Dialogs.ShowErrorMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.LegendaryRequiredInstallViaThirdPartyLauncherError,
                        fluentUbisoftArgs));
            }

            if (ubisoftRecommendedGames.Count > 0)
            {
                var fluentUbisoftArgs = new Dictionary<string, IFluentType>
                {
                    ["count"] = (FluentNumber)ubisoftRecommendedGames.Count
                };
                if (ubisoftRecommendedGames.Count == 1)
                {
                    fluentUbisoftArgs["gameTitle"] = (FluentString)ubisoftRecommendedGames[0];
                }
                else
                {
                    string ubisoftRecommendedGamesCombined =
                        string.Join(", ", ubisoftRecommendedGames.Select(item => item.ToString()));
                    fluentUbisoftArgs["gameTitle"] = (FluentString)ubisoftRecommendedGamesCombined;
                }

                fluentUbisoftArgs["thirdPartyLauncherName"] = (FluentString)"Ubisoft Connect";
                await playniteApi.Dialogs.ShowErrorMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.LegendaryRequiredInstallOfThirdPartyLauncher,
                        fluentUbisoftArgs));
            }

            CalculateTotalSize();

            var clientApi = new EpicAccountClient(playniteApi);
            var userLoggedIn = await clientApi.GetIsUserLoggedIn();
            if (MultiInstallData.Count <= 0 || !userLoggedIn)
            {
                if (!userLoggedIn)
                {
                    await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(
                        LOC.ThirdPartyPlayniteGameInstallError,
                        new Dictionary<string, IFluentType>
                        {
                            ["var0"] = (FluentString)LocalizationManager.Instance.GetString(
                                LOC.ThirdPartyPlayniteLoginRequired)
                        }));
                }

                InstallerWindow.Close();
                return;
            }

            if (DownloadSizeNumber != 0 && InstallSizeNumber != 0)
            {
                InstallBtn.IsEnabled = true;
                RepairBtn.IsEnabled = true;
            }

            ReloadBtn.IsEnabled = true;
        }

        private void CalculateTotalSize()
        {
            DownloadSizeNumber = 0;
            InstallSizeNumber = 0;
            foreach (var installData in MultiInstallData)
            {
                var selectedDlcs = installData.DownloadProperties.SelectedDlcs;
                if (selectedDlcs != null && selectedDlcs.Count > 0)
                {
                    foreach (var selectedDlc in selectedDlcs)
                    {
                        DownloadSizeNumber += selectedDlc.Value.DownloadSizeNumber;
                        InstallSizeNumber += selectedDlc.Value.InstallSizeNumber;
                    }
                }

                DownloadSizeNumber += installData.DownloadSizeNumber;
                InstallSizeNumber += installData.InstallSizeNumber;
            }

            if (DownloadSizeNumber != 0 && InstallSizeNumber != 0)
            {
                InstallBtn.IsEnabled = true;
                RepairBtn.IsEnabled = true;
            }

            UpdateAfterInstallingSize();
            DownloadSizeTB.Text = CommonHelpers.FormatSize(DownloadSizeNumber);
            InstallSizeTB.Text = CommonHelpers.FormatSize(InstallSizeNumber);
        }

        private void UpdateSpaceInfo(string path)
        {
            DriveInfo dDrive = new DriveInfo(path);
            if (dDrive.IsReady)
            {
                AvailableFreeSpace = dDrive.AvailableFreeSpace;
                SpaceTB.Text = CommonHelpers.FormatSize(AvailableFreeSpace);
            }

            UpdateAfterInstallingSize();
        }

        private void UpdateAfterInstallingSize()
        {
            double afterInstallSizeNumber = (double)(AvailableFreeSpace - InstallSizeNumber);
            if (afterInstallSizeNumber < 0)
            {
                afterInstallSizeNumber = 0;
            }

            AfterInstallingTB.Text = CommonHelpers.FormatSize(afterInstallSizeNumber);
        }

        private async void ExtraContentLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RepairBtn.IsEnabled = false;
            InstallBtn.IsEnabled = false;
            var selectedExtraContent =
                ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySdlInfo>>().ToList();
            var selectedDlCs = selectedExtraContent.Where(i => i.Value.Is_dlc).ToList();
            var sdls = selectedExtraContent.Where(i => i.Value.Is_dlc == false).ToList();

            var selectedSdls = new List<string>();

            var requiredTags = await LegendaryLauncher.GetRequiredSdlsTags(MultiInstallData[0]);
            if (requiredTags.Count > 0)
            {
                foreach (var requiredTag in requiredTags)
                {
                    selectedSdls.AddMissing(requiredTag);
                }
            }

            if (sdls.Count > 0)
            {
                foreach (var sdl in sdls)
                {
                    selectedSdls.AddMissing(sdl.Key);
                }
            }

            MultiInstallData[0].DownloadProperties.ExtraContent = selectedSdls;
            var gameData = new LegendaryGameInfo.Game
            {
                App_name = MultiInstallData[0].GameId,
                Title = MultiInstallData[0].Name,
            };
            var gameSize = await LegendaryLauncher.CalculateGameSize(MultiInstallData[0]);
            MultiInstallData[0].DownloadSizeNumber = gameSize.Download_size;
            MultiInstallData[0].InstallSizeNumber = gameSize.Disk_size;


            MultiInstallData[0].DownloadProperties.SelectedDlcs =
                new Dictionary<string, DownloadManagerData.Download>();
            if (selectedDlCs.Count > 0)
            {
                foreach (var dlc in selectedDlCs)
                {
                    var dlcInstallData = new DownloadManagerData.Download
                    {
                        GameId = dlc.Key,
                        Name = dlc.Value.Name,
                    };
                    if (MultiInstallData[0].DownloadProperties.ExtraContent.Count > 0)
                    {
                        Dictionary<string, LegendarySdlInfo> extraContentDlcInfo =
                            await LegendaryLauncher.GetExtraContentInfo(dlcInstallData, true);
                        if (extraContentDlcInfo.Count > 0)
                        {
                            dlcInstallData.DownloadProperties.ExtraContent = new List<string>
                            {
                                ""
                            };
                            foreach (var singleSdl in MultiInstallData[0].DownloadProperties.ExtraContent)
                            {
                                if (extraContentDlcInfo.ContainsKey(singleSdl))
                                {
                                    dlcInstallData.DownloadProperties.ExtraContent.Add(singleSdl);
                                }
                            }
                        }
                    }

                    var dlcSize = await LegendaryLauncher.CalculateGameSize(dlcInstallData);
                    dlcInstallData.DownloadSizeNumber = dlcSize.Download_size;
                    dlcInstallData.InstallSizeNumber = dlcSize.Disk_size;
                    MultiInstallData[0].DownloadProperties.SelectedDlcs.Add(dlcInstallData.GameId, dlcInstallData);
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

            var allDlCs = ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySdlInfo>>()
                                        .Where(i => i.Value.Is_dlc)
                                        .ToList();
            if (AllDlcsChk.IsChecked == true && selectedDlCs.Count() != allDlCs.Count)
            {
                uncheckedByUser = false;
                AllDlcsChk.IsChecked = false;
                uncheckedByUser = true;
            }

            if (AllDlcsChk.IsChecked == false && selectedDlCs.Count() == allDlCs.Count)
            {
                checkedByUser = false;
                AllDlcsChk.IsChecked = true;
                checkedByUser = true;
            }

            CalculateTotalSize();
        }

        private async void RepairBtn_Click(object sender, RoutedEventArgs e)
        {
            foreach (var installData in MultiInstallData)
            {
                installData.DownloadSizeNumber = 0;
                installData.InstallSizeNumber = 0;
                var selectedDlcs = installData.DownloadProperties.SelectedDlcs;
                if (selectedDlcs != null && selectedDlcs.Count > 0)
                {
                    foreach (var selectedDlc in selectedDlcs)
                    {
                        var dlcInstallData = selectedDlc.Value;
                        dlcInstallData.DownloadSizeNumber = 0;
                        dlcInstallData.InstallSizeNumber = 0;
                    }
                }
            }

            await StartTask(DownloadAction.Repair);
        }

        private void AllDlcsChk_Checked(object sender, RoutedEventArgs e)
        {
            if (checkedByUser)
            {
                var dlcs = ExtraContentLB.Items.Cast<KeyValuePair<string, LegendarySdlInfo>>()
                                         .Where(x => x.Value.Is_dlc)
                                         .ToList();
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
                var dlcs = ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySdlInfo>>()
                                         .Where(x => x.Value.Is_dlc)
                                         .ToList();
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
            Window window = playniteApi.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false,
            });
            window.DataContext = selectedGame;
            window.Content = new LegendaryExtraInstallationContentView();
            window.Owner = InstallerWindow;
            window.SizeToContent = SizeToContent.WidthAndHeight;
            window.MinWidth = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.Title = selectedGame.Name;
            var result = window.ShowDialog();
            if (result == false)
            {
                RepairBtn.IsEnabled = false;
                InstallBtn.IsEnabled = false;
                foreach (var installData in MultiInstallData)
                {
                    var gameInfo = new LegendaryGameInfo.Game
                    {
                        App_name = installData.GameId,
                        Title = installData.Name
                    };
                    manifest = await LegendaryLauncher.GetGameInfo(gameInfo);
                    var gameSize = await LegendaryLauncher.CalculateGameSize(installData);
                    installData.DownloadSizeNumber = gameSize.Download_size;
                    installData.InstallSizeNumber = gameSize.Disk_size;
                    var selectedDlcs = installData.DownloadProperties.SelectedDlcs;
                    if (selectedDlcs != null && selectedDlcs.Count > 0)
                    {
                        foreach (var selectedDlc in selectedDlcs)
                        {
                            var dlcInstallData = selectedDlc.Value;
                            if (installData.DownloadProperties.ExtraContent.Count > 0)
                            {
                                Dictionary<string, LegendarySdlInfo> extraContentDlcInfo =
                                    await LegendaryLauncher.GetExtraContentInfo(dlcInstallData, true);
                                if (extraContentDlcInfo.Count > 0)
                                {
                                    dlcInstallData.DownloadProperties.ExtraContent = new List<string>
                                    {
                                        ""
                                    };
                                    foreach (var singleSdl in installData.DownloadProperties.ExtraContent)
                                    {
                                        if (extraContentDlcInfo.ContainsKey(singleSdl))
                                        {
                                            dlcInstallData.DownloadProperties.ExtraContent.Add(singleSdl);
                                        }
                                    }
                                }
                            }

                            var dlcSize = await LegendaryLauncher.CalculateGameSize(dlcInstallData);
                            selectedDlc.Value.DownloadSizeNumber = dlcSize.Download_size;
                            selectedDlc.Value.InstallSizeNumber = dlcSize.Disk_size;
                        }
                    }
                }

                CalculateTotalSize();
            }
        }

        private async void ReloadBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = await playniteApi.Dialogs.ShowMessageAsync(
                LocalizationManager.Instance.GetString(LOC.CommonReloadConfirm),
                LocalizationManager.Instance.GetString(LOC.CommonReload), MessageBoxButtons.YesNo,
                MessageBoxSeverity.Question);
            if (result == MessageBoxResult.Yes)
            {
                InstallBtn.IsEnabled = false;
                DownloadSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
                InstallSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
                AfterInstallingTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);

                var gameIds = MultiInstallData.Select(g => g.GameId).ToList();
                LegendaryLauncher.ClearSpecificGamesCache(gameIds);

                await RefreshAll();
            }
        }
    }
}