using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using Linguini.Shared.Types.Bundle;
using Playnite.SDK;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryDlcManager.xaml
    /// </summary>
    public partial class LegendaryDlcManager : UserControl
    {
        private LegendaryGameInfo.Rootobject manifest;
        private Game Game;
        public string GameId;
        private IPlayniteAPI playniteAPI = API.Instance;
        private ILogger logger = LogManager.GetLogger();
        public Window DlcManagerWindow => Window.GetWindow(this);
        public ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>> installedDLCs;
        public ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>> notInstalledDLCs;
        public long availableFreeSpace;
        public Dictionary<string, Installed> installedAppList;
        public List<string> installedSdls = new List<string>();

        public LegendaryDlcManager()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            Game = DataContext as Game;
            GameId = Game.GameId;
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                CloseWindowTab.Visibility = Visibility.Visible;
            }
            CommonHelpers.SetControlBackground(this);
            await RefreshAll();
        }

        private async Task RefreshAll()
        {
            NoAvailableDlcsTB.Visibility = Visibility.Collapsed;
            NoInstalledDlcsTB.Visibility = Visibility.Collapsed;
            BottomADGrd.Visibility = Visibility.Collapsed;
            TopADSP.Visibility = Visibility.Collapsed;
            InstalledDlcsSP.Visibility = Visibility.Collapsed;
            ReloadABtn.IsEnabled = false;
            LoadingATB.Visibility = Visibility.Visible;
            LoadingITB.Visibility = Visibility.Visible;
            var gameData = new LegendaryGameInfo.Game
            {
                Title = Game.Name,
                App_name = Game.GameId
            };
            manifest = await LegendaryLauncher.GetGameInfo(gameData);
            if (manifest != null && manifest.Manifest != null)
            {
                if (manifest.Game.Owned_dlc.Count > 0)
                {
                    installedAppList = LegendaryLauncher.GetInstalledAppList();
                    installedDLCs = new ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>>();
                    notInstalledDLCs = new ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>>();
                    foreach (var dlc in manifest.Game.Owned_dlc.OrderBy(obj => obj.Title))
                    {
                        if (!dlc.App_name.IsNullOrEmpty())
                        {
                            var dlcData = new LegendaryGameInfo.Game
                            {
                                Title = dlc.Title.RemoveTrademarks(),
                                App_name = dlc.App_name
                            };
                            var dlcInfo = await LegendaryLauncher.GetGameInfo(dlcData);
                            dlcInfo.Game.Title = dlcInfo.Game.Title.RemoveTrademarks();
                            if (installedAppList.ContainsKey(dlc.App_name))
                            {
                                installedDLCs.Add(new KeyValuePair<string, LegendaryGameInfo.Rootobject>(dlc.App_name, dlcInfo));
                            }
                            else
                            {
                                notInstalledDLCs.Add(new KeyValuePair<string, LegendaryGameInfo.Rootobject>(dlc.App_name, dlcInfo));
                            }
                        }
                    }
                    InstalledDlcsLB.ItemsSource = installedDLCs;
                    AvailableDlcsLB.ItemsSource = notInstalledDLCs;

                    if (!Game.InstallDirectory.IsNullOrEmpty())
                    {
                        DriveInfo dDrive = new DriveInfo(Path.GetFullPath(Game.InstallDirectory));
                        if (dDrive.IsReady)
                        {
                            availableFreeSpace = dDrive.AvailableFreeSpace;
                            SpaceTB.Text = CommonHelpers.FormatSize(availableFreeSpace);
                            AfterInstallingTB.Text = CommonHelpers.FormatSize(availableFreeSpace);
                        }
                    }
                    var settings = LegendaryLibrary.GetSettings();
                    MaxWorkersNI.Value = settings.MaxWorkers.ToString();
                    MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
                    ReorderingChk.IsChecked = settings.EnableReordering;
                    if (InstalledDlcsLB.Items.Count == 0)
                    {
                        InstalledDlcsSP.Visibility = Visibility.Collapsed;
                        NoInstalledDlcsTB.Visibility = Visibility.Visible;
                    }
                    if (AvailableDlcsLB.Items.Count == 0)
                    {
                        BottomADGrd.Visibility = Visibility.Collapsed;
                        TopADSP.Visibility = Visibility.Collapsed;
                        NoAvailableDlcsTB.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    NoAvailableDlcsTB.Visibility = Visibility.Visible;
                    BottomADGrd.Visibility = Visibility.Collapsed;
                    TopADSP.Visibility = Visibility.Collapsed;
                    InstalledDlcsTbI.Visibility = Visibility.Collapsed;
                }
            }
            LoadingATB.Visibility = Visibility.Collapsed;
            LoadingITB.Visibility = Visibility.Collapsed;
            if (InstalledDlcsLB.Items.Count > 0)
            {
                InstalledDlcsSP.Visibility = Visibility.Visible;
            }
            if (AvailableDlcsLB.Items.Count > 0)
            {
                installedSdls = new List<string>();
                if (installedAppList != null && installedAppList.Count > 0)
                {
                    if (installedAppList.ContainsKey(Game.GameId))
                    {
                        installedSdls = installedAppList[Game.GameId].Install_tags;
                    }
                }
                BottomADGrd.Visibility = Visibility.Visible;
                TopADSP.Visibility = Visibility.Visible;
            }
            if (Game.InstallDirectory.IsNullOrEmpty())
            {
                AvailableDlcsActionSP.Visibility = Visibility.Collapsed;
                BottomADGrd.Visibility = Visibility.Collapsed;
                AvailableDlcsAOBrd.Visibility = Visibility.Collapsed;
            }
            ReloadABtn.IsEnabled = true;
        }

        private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result;
            if (InstalledDlcsLB.SelectedItems.Count == 1)
            {
                var selectedDLC = (KeyValuePair<string, LegendaryGameInfo.Rootobject>)InstalledDlcsLB.SelectedItems[0];
                result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonUninstallGameConfirm, new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)selectedDLC.Value.Game.Title }),
                                                         LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                                                         MessageBoxButton.YesNo,
                                                         MessageBoxImage.Question);
            }
            else
            {
                result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonUninstallSelectedDlcs),
                                                         LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                                                         MessageBoxButton.YesNo,
                                                         MessageBoxImage.Question);
            }
            if (result == MessageBoxResult.Yes)
            {
                foreach (var selectedDlc in InstalledDlcsLB.SelectedItems.Cast<KeyValuePair<string, LegendaryGameInfo.Rootobject>>().ToList())
                {
                    var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                       .WithArguments(new[] { "-y", "uninstall", selectedDlc.Key })
                                       .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                       .AddCommandToLog()
                                       .WithValidation(CommandResultValidation.None)
                                       .ExecuteBufferedAsync();
                    if (!cmd.StandardError.Contains("has been uninstalled"))
                    {
                        logger.Debug("[Legendary] " + cmd.StandardError);
                        logger.Error("[Legendary] exit code: " + cmd.ExitCode);
                    }
                    else
                    {
                        installedDLCs.Remove(selectedDlc);
                        notInstalledDLCs.Add(selectedDlc);
                    }
                }
                if (InstalledDlcsLB.Items.Count == 0)
                {
                    InstalledDlcsSP.Visibility = Visibility.Collapsed;
                    NoInstalledDlcsTB.Visibility = Visibility.Visible;
                }
            }
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableDlcsLB.SelectedItems.Count > 0)
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
                DlcManagerWindow.Close();
                LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();

                var tasks = new List<DownloadManagerData.Download>();
                foreach (var selectedOption in AvailableDlcsLB.SelectedItems.Cast<KeyValuePair<string, LegendaryGameInfo.Rootobject>>())
                {
                    var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == selectedOption.Key);
                    if (wantedItem != null)
                    {
                        playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonDownloadAlreadyExists, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)wantedItem.name }), "", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        DownloadProperties downloadProperties = new DownloadProperties()
                        {
                            downloadAction = DownloadAction.Install,
                            enableReordering = (bool)ReorderingChk.IsChecked,
                            maxWorkers = maxWorkers,
                            maxSharedMemory = maxSharedMemory,
                            ignoreFreeSpace = (bool)IgnoreFreeSpaceChk.IsChecked
                        };
                        if (installedSdls.Count > 0)
                        {
                            foreach (var installedSdl in installedSdls)
                            {
                                downloadProperties.extraContent.AddMissing(installedSdl);
                            }
                        }
                        var downloadTask = new DownloadManagerData.Download
                        {
                            gameID = selectedOption.Key,
                            name = selectedOption.Value.Game.Title,
                            downloadProperties = downloadProperties
                        };
                        var dlcSize = await LegendaryLauncher.CalculateGameSize(downloadTask);
                        downloadTask.downloadSizeNumber = dlcSize.Download_size;
                        downloadTask.installSizeNumber = dlcSize.Disk_size;
                        tasks.Add(downloadTask);
                    }
                }
                if (tasks.Count > 0)
                {
                    await downloadManager.EnqueueMultipleJobs(tasks);
                }
            }
        }

        private async void AvailableDlcsLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Game.InstallDirectory.IsNullOrEmpty())
            {
                return;
            }
            if (AvailableDlcsLB.SelectedIndex == -1)
            {
                InstallBtn.IsEnabled = false;
            }
            else
            {
                InstallBtn.IsEnabled = true;
            }
            double initialDownloadSizeNumber = 0;
            double initialInstallSizeNumber = 0;

            foreach (var selectedOption in AvailableDlcsLB.SelectedItems.Cast<KeyValuePair<string, LegendaryGameInfo.Rootobject>>().ToList())
            {
                var dlcInstallData = new DownloadManagerData.Download
                {
                    gameID = selectedOption.Key,
                    name = selectedOption.Value.Game.Title
                };
                if (installedSdls.Count > 0)
                {
                    foreach (var installedSdl in installedSdls)
                    {
                        dlcInstallData.downloadProperties.extraContent.AddMissing(installedSdl);
                    }
                }
                var dlcSize = await LegendaryLauncher.CalculateGameSize(dlcInstallData);
                initialDownloadSizeNumber += dlcSize.Download_size;
                initialInstallSizeNumber += dlcSize.Disk_size;
            }
            var downloadSize = CommonHelpers.FormatSize(initialDownloadSizeNumber);
            DownloadSizeTB.Text = downloadSize;
            var installSize = CommonHelpers.FormatSize(initialInstallSizeNumber);
            InstallSizeTB.Text = installSize;
            double afterInstallSizeNumber = (double)(availableFreeSpace - initialInstallSizeNumber);
            AfterInstallingTB.Text = CommonHelpers.FormatSize(afterInstallSizeNumber);
        }

        private void InstalledDlcsLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InstalledDlcsLB.SelectedIndex == -1)
            {
                UninstallBtn.IsEnabled = false;
            }
            else
            {
                UninstallBtn.IsEnabled = true;
            }
        }

        private void SelectAllAvDlcsBtn_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableDlcsLB.Items.Count > 0)
            {
                if (AvailableDlcsLB.Items.Count == AvailableDlcsLB.SelectedItems.Count)
                {
                    AvailableDlcsLB.UnselectAll();
                }
                else
                {
                    AvailableDlcsLB.SelectAll();
                }
            }
        }

        private void SelectAllInDlcsBtn_Click_1(object sender, RoutedEventArgs e)
        {
            if (InstalledDlcsLB.Items.Count > 0)
            {
                if (InstalledDlcsLB.Items.Count == InstalledDlcsLB.SelectedItems.Count)
                {
                    InstalledDlcsLB.UnselectAll();
                }
                else
                {
                    InstalledDlcsLB.SelectAll();
                }
            }
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tabControl)
            {
                if (tabControl.SelectedItem == CloseWindowTab)
                {
                    Window.GetWindow(this).Close();
                }
            }
        }

        private async void ReloadABtn_Click(object sender, RoutedEventArgs e)
        {
            var result = playniteAPI.Dialogs.ShowMessage(LocalizationManager.Instance.GetString(LOC.CommonReloadConfirm), LocalizationManager.Instance.GetString(LOC.CommonReload), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                InstallBtn.IsEnabled = false;
                DownloadSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
              
                var cacheDirs = new List<string>()
                {
                    LegendaryLibrary.Instance.GetCachePath("infocache"),
                    LegendaryLibrary.Instance.GetCachePath("sdlcache"),
                    LegendaryLibrary.Instance.GetCachePath("updateinfocache"),
                    Path.Combine(LegendaryLauncher.ConfigPath, "metadata")
                };

                foreach (var cacheDir in cacheDirs)
                {
                    foreach (var file in Directory.GetFiles(cacheDir, "*", SearchOption.AllDirectories))
                    {
                        if (file.Contains(GameId))
                        {
                            File.Delete(file);
                        }
                    }
                }
                await RefreshAll();
            }
        }
    }
}
