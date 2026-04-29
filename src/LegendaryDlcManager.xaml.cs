using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using Linguini.Shared.Types.Bundle;
using Playnite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryDlcManager.xaml
    /// </summary>
    public partial class LegendaryDlcManager
    {
        private LegendaryGameInfo.Rootobject? manifest;
        private Game game = null!;
        private string gameId = null!;
        private static IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
        private ILogger logger = LogManager.GetLogger();
        private Window DlcManagerWindow => Window.GetWindow(this)!;
        private ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>> installedDlCs = [];
        private ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>> notInstalledDlCs = [];
        private long availableFreeSpace;
        private Dictionary<string, Installed>? installedAppList;
        private List<string> installedSdls = [];
        public CommonHelpers CommonHelpers = new CommonHelpers(playniteApi);

        public LegendaryDlcManager()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            game = (DataContext as Game)!;
            gameId = game.LibraryGameId!;
            if (playniteApi.AppInfo.Mode == AppMode.Fullscreen)
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
            BottomAvDlcGrd.Visibility = Visibility.Collapsed;
            TopAvDlcSP.Visibility = Visibility.Collapsed;
            InstalledDlcsSP.Visibility = Visibility.Collapsed;
            ReloadABtn.IsEnabled = false;
            LoadingAvTB.Visibility = Visibility.Visible;
            LoadingITB.Visibility = Visibility.Visible;
            var gameData = new LegendaryGameInfo.Game
            {
                Title = game.Name,
                App_name = game.LibraryGameId!
            };
            manifest = await LegendaryLauncher.GetGameInfo(gameData);
            if (manifest is { Manifest: not null })
            {
                if (manifest?.Game?.Owned_dlc.Count > 0)
                {
                    installedAppList = LegendaryLauncher.GetInstalledAppList();
                    installedDlCs = [];
                    notInstalledDlCs = [];
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
                            dlcInfo.Game?.Title = dlcInfo.Game.Title.RemoveTrademarks();
                            if (installedAppList.ContainsKey(dlc.App_name))
                            {
                                installedDlCs.Add(
                                    new KeyValuePair<string, LegendaryGameInfo.Rootobject>(dlc.App_name, dlcInfo));
                            }
                            else
                            {
                                notInstalledDlCs.Add(
                                    new KeyValuePair<string, LegendaryGameInfo.Rootobject>(dlc.App_name, dlcInfo));
                            }
                        }
                    }

                    InstalledDlcsLB.ItemsSource = installedDlCs;
                    AvailableDlcsLB.ItemsSource = notInstalledDlCs;

                    if (!string.IsNullOrEmpty(game.InstallDirectory))
                    {
                        DriveInfo dDrive = new DriveInfo(Path.GetFullPath(game.InstallDirectory));
                        if (dDrive.IsReady)
                        {
                            availableFreeSpace = dDrive.AvailableFreeSpace;
                            SpaceTB.Text = CommonHelpers.FormatSize(availableFreeSpace);
                            AfterInstallingTB.Text = CommonHelpers.FormatSize(availableFreeSpace);
                        }
                    }

                    var settings = LegendaryLibrary.GetSettings();
                    if (settings != null)
                    {
                        MaxWorkersNI.Value = settings.MaxWorkers.ToString();
                        MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
                        ReorderingChk.IsChecked = settings.EnableReordering;
                    }

                    if (InstalledDlcsLB.Items.Count == 0)
                    {
                        InstalledDlcsSP.Visibility = Visibility.Collapsed;
                        NoInstalledDlcsTB.Visibility = Visibility.Visible;
                    }

                    if (AvailableDlcsLB.Items.Count == 0)
                    {
                        BottomAvDlcGrd.Visibility = Visibility.Collapsed;
                        TopAvDlcSP.Visibility = Visibility.Collapsed;
                        NoAvailableDlcsTB.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    NoAvailableDlcsTB.Visibility = Visibility.Visible;
                    BottomAvDlcGrd.Visibility = Visibility.Collapsed;
                    TopAvDlcSP.Visibility = Visibility.Collapsed;
                    InstalledDlcsTbI.Visibility = Visibility.Collapsed;
                }
            }

            LoadingAvTB.Visibility = Visibility.Collapsed;
            LoadingITB.Visibility = Visibility.Collapsed;
            if (InstalledDlcsLB.Items.Count > 0)
            {
                InstalledDlcsSP.Visibility = Visibility.Visible;
            }

            if (AvailableDlcsLB.Items.Count > 0)
            {
                installedSdls = new List<string>();
                if (installedAppList is { Count: > 0 })
                {
                    if (installedAppList.TryGetValue(game.LibraryGameId!, out var value))
                    {
                        installedSdls = value.Install_tags;
                    }
                }

                BottomAvDlcGrd.Visibility = Visibility.Visible;
                TopAvDlcSP.Visibility = Visibility.Visible;
            }

            if (string.IsNullOrEmpty(game.InstallDirectory))
            {
                AvailableDlcsActionSP.Visibility = Visibility.Collapsed;
                BottomAvDlcGrd.Visibility = Visibility.Collapsed;
                AvailableDlcsAOBrd.Visibility = Visibility.Collapsed;
            }

            ReloadABtn.IsEnabled = true;
        }

        private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result;
            if (InstalledDlcsLB.SelectedItems.Count == 1)
            {
                var selectedDlc = (KeyValuePair<string, LegendaryGameInfo.Rootobject>)InstalledDlcsLB.SelectedItems[0]!;
                result = await playniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonUninstallGameConfirm,
                        new Dictionary<string, IFluentType>
                            { ["gameTitle"] = (FluentString)selectedDlc.Value.Game!.Title }),
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                    MessageBoxButtons.YesNo,
                    MessageBoxSeverity.Question);
            }
            else
            {
                result = await playniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonUninstallSelectedDlcs),
                    LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
                    MessageBoxButtons.YesNo,
                    MessageBoxSeverity.Question);
            }

            if (result == MessageBoxResult.Yes)
            {
                foreach (var selectedDlc in InstalledDlcsLB.SelectedItems
                                                           .Cast<KeyValuePair<string, LegendaryGameInfo.Rootobject>>()
                                                           .ToList())
                {
                    var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                       .WithArguments(["-y", "uninstall", selectedDlc.Key])
                                       .WithEnvironmentVariables(
                                            await LegendaryLauncher.GetDefaultEnvironmentVariables())
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
                        installedDlCs.Remove(selectedDlc);
                        notInstalledDlCs.Add(selectedDlc);
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
                var isUdmInstalled = await LegendaryDownloadLogic.CheckIfUdmInstalled();
                if (!isUdmInstalled)
                {
                    return;
                }

                var settings = LegendaryLibrary.GetSettings();
                var maxWorkers = settings?.MaxWorkers ?? 0;
                if (MaxWorkersNI.Value != "")
                {
                    maxWorkers = int.Parse(MaxWorkersNI.Value);
                }

                var maxSharedMemory = settings?.MaxSharedMemory ?? 0;
                if (MaxSharedMemoryNI.Value != "")
                {
                    maxSharedMemory = int.Parse(MaxSharedMemoryNI.Value);
                }

                DlcManagerWindow.Close();
                var tasks = new List<DownloadManagerData.Download>();
                foreach (var selectedOption in AvailableDlcsLB.SelectedItems
                                                              .Cast<KeyValuePair<string,
                                                                   LegendaryGameInfo.Rootobject>>())
                {
                    var downloadProperties = new DownloadProperties()
                    {
                        DownloadAction = DownloadAction.Install,
                        EnableReordering = ReorderingChk?.IsChecked ?? false,
                        MaxWorkers = maxWorkers,
                        MaxSharedMemory = maxSharedMemory,
                        IgnoreFreeSpace = IgnoreFreeSpaceChk?.IsChecked ?? false,
                    };
                    if (installedSdls.Count > 0)
                    {
                        foreach (var installedSdl in installedSdls)
                        {
                            downloadProperties.ExtraContent?.AddMissing(installedSdl);
                        }
                    }

                    var downloadTask = new DownloadManagerData.Download
                    {
                        GameId = selectedOption.Key,
                        Name = selectedOption.Value.Game?.Title ?? "",
                        DownloadProperties = downloadProperties
                    };
                    var dlcSize = await LegendaryLauncher.CalculateGameSize(downloadTask);
                    downloadTask.DownloadSizeNumber = dlcSize.Download_size;
                    downloadTask.InstallSizeNumber = dlcSize.Disk_size;
                    tasks.Add(downloadTask);
                }

                if (tasks.Count > 0)
                {
                    if (LegendaryLibrary.Instance.UnifiedDownloadLogic is LegendaryDownloadLogic downloadLogic)
                    {
                        await downloadLogic.AddTasks(tasks);
                    }
                }
            }
        }

        private async void AvailableDlcsLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(game.InstallDirectory))
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

            foreach (var selectedOption in AvailableDlcsLB.SelectedItems
                                                          .Cast<KeyValuePair<string, LegendaryGameInfo.Rootobject>>()
                                                          .ToList())
            {
                var dlcInstallData = new DownloadManagerData.Download
                {
                    GameId = selectedOption.Key,
                    Name = selectedOption.Value.Game?.Title ?? ""
                };
                if (installedSdls.Count > 0)
                {
                    foreach (var installedSdl in installedSdls)
                    {
                        dlcInstallData.DownloadProperties.ExtraContent?.AddMissing(installedSdl);
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
            var afterInstallSizeNumber = availableFreeSpace - initialInstallSizeNumber;
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
                if (ReferenceEquals(tabControl.SelectedItem, CloseWindowTab))
                {
                    Window.GetWindow(this)?.Close();
                }
            }
        }

        private async void ReloadABtn_Click(object sender, RoutedEventArgs e)
        {
            var result = await playniteApi.Dialogs.ShowMessageAsync(
                LocalizationManager.Instance.GetString(LOC.CommonReloadConfirm),
                LocalizationManager.Instance.GetString(LOC.CommonReload), MessageBoxButtons.YesNo,
                MessageBoxSeverity.Question);
            if (result == MessageBoxResult.Yes)
            {
                InstallBtn.IsEnabled = false;
                DownloadSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
                
                var gameIds = new List<string> { gameId };
                LegendaryLauncher.ClearSpecificGamesCache(gameIds);

                await RefreshAll();
            }
        }
    }
}