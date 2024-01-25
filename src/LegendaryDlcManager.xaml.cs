using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Enums;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
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
        private Game Game => DataContext as Game;
        public string GameId => Game.GameId;
        private IPlayniteAPI playniteAPI = API.Instance;
        private ILogger logger = LogManager.GetLogger();
        public Window DlcManagerWindow => Window.GetWindow(this);
        public ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>> installedDLCs;
        public ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>> notInstalledDLCs;

        public LegendaryDlcManager()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            AvailableDlcsSP.Visibility = Visibility.Collapsed;
            InstalledDlcsSP.Visibility = Visibility.Collapsed;
            LoadingATB.Visibility = Visibility.Visible;
            LoadingITB.Visibility = Visibility.Visible;
            manifest = await LegendaryLauncher.GetGameInfo(GameId);
            if (manifest != null && manifest.Manifest != null)
            {
                if (manifest.Game.Owned_dlc.Length > 0)
                {
                    var installedAppList = LegendaryLauncher.GetInstalledAppList();
                    installedDLCs = new ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>>();
                    notInstalledDLCs = new ObservableCollection<KeyValuePair<string, LegendaryGameInfo.Rootobject>>();
                    foreach (var dlc in manifest.Game.Owned_dlc.OrderBy(obj => obj.Title))
                    {
                        var dlcInfo = await LegendaryLauncher.GetGameInfo(dlc.App_name);
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
                    InstalledDlcsLB.ItemsSource = installedDLCs;
                    AvailableDlcsLB.ItemsSource = notInstalledDLCs;
                    DriveInfo dDrive = new DriveInfo(Path.GetFullPath(Game.InstallDirectory));
                    if (dDrive.IsReady)
                    {
                        SpaceTB.Text = Helpers.FormatSize(dDrive.AvailableFreeSpace);
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
                        AvailableDlcsSP.Visibility = Visibility.Collapsed;
                        NoAvailableDlcsTB.Visibility = Visibility.Visible;
                    }
                }
                else
                {
                    NoAvailableDlcsTB.Visibility = Visibility.Visible;
                    AvailableDlcsSP.Visibility = Visibility.Collapsed;
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
                AvailableDlcsSP.Visibility = Visibility.Visible;
            }
        }

        private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = new MessageBoxResult();
            if (InstalledDlcsLB.SelectedItems.Count == 1)
            {
                var selectedDLC = (KeyValuePair<string, LegendaryGameInfo.Rootobject>)InstalledDlcsLB.SelectedItems[0];
                result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryUninstallGameConfirm).Format(selectedDLC.Value.Game.Title),
                                                         ResourceProvider.GetString(LOC.Legendary3P_PlayniteUninstallGame),
                                                         MessageBoxButton.YesNo,
                                                         MessageBoxImage.Question);
            }
            else
            {
                result = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryUninstallSelectedDlcs),
                                                         ResourceProvider.GetString(LOC.Legendary3P_PlayniteUninstallGame),
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
                bool enableReordering = Convert.ToBoolean(ReorderingChk.IsChecked);
                DlcManagerWindow.Close();
                LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();

                var tasks = new List<DownloadManagerData.Download>();
                foreach (var selectedOption in AvailableDlcsLB.SelectedItems.Cast<KeyValuePair<string, LegendaryGameInfo.Rootobject>>())
                {
                    var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == selectedOption.Key);
                    if (wantedItem != null)
                    {
                        playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), wantedItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    else
                    {
                        DownloadProperties downloadProperties = new DownloadProperties()
                        {
                            downloadAction = DownloadAction.Install,
                            enableReordering = enableReordering,
                            maxWorkers = maxWorkers,
                            maxSharedMemory = maxSharedMemory
                        };
                        var dlcInfo = selectedOption.Value;
                        var downloadSize = "0 b";
                        var installSize = "0 b";
                        if (dlcInfo.Manifest != null)
                        {
                            downloadSize = Helpers.FormatSize(dlcInfo.Manifest.Download_size);
                            installSize = Helpers.FormatSize(dlcInfo.Manifest.Disk_size);
                        }
                        var downloadTask = new DownloadManagerData.Download
                        {
                            gameID = selectedOption.Key,
                            name = selectedOption.Value.Game.Title,
                            downloadSize = downloadSize,
                            installSize = installSize,
                            downloadProperties = downloadProperties
                        };
                        tasks.Add(downloadTask);
                    }
                }
                if (tasks.Count > 0)
                {
                    await downloadManager.EnqueueMultipleJobs(tasks);
                }
            }
        }

        private void AvailableDlcsLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
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
                var dlcManifest = selectedOption.Value;
                bool correctDlcJson = false;
                if (dlcManifest != null && dlcManifest.Manifest != null)
                {
                    correctDlcJson = true;
                }
                if (correctDlcJson)
                {
                    initialDownloadSizeNumber += dlcManifest.Manifest.Download_size;
                    initialInstallSizeNumber += dlcManifest.Manifest.Disk_size;
                }
            }
            var downloadSize = Helpers.FormatSize(initialDownloadSizeNumber);
            DownloadSizeTB.Text = downloadSize;
            var installSize = Helpers.FormatSize(initialInstallSizeNumber);
            InstallSizeTB.Text = installSize;
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
    }
}
