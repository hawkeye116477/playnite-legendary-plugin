using System.Windows;
using System.Windows.Controls;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using Playnite;

namespace LegendaryLibraryNS;

/// <summary>
/// Interaction logic for LegendaryUpdater.xaml
/// </summary>
public partial class LegendaryUpdater : UserControl
{
    private Dictionary<string, UpdateInfo> updatesList = [];
    private readonly IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
    private CommonHelpers commonHelpers = LegendaryLibrary.Instance.CommonHelpers;

    public LegendaryUpdater()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        var isUdmInstalled = await LegendaryDownloadLogic.CheckIfUdmInstalled();
        if (!isUdmInstalled)
        {
            Window.GetWindow(this)?.Close();
            return;
        }

        updatesList = (Dictionary<string, UpdateInfo>)DataContext;
        commonHelpers.SetControlBackground(this);
        RefreshWindow();
        var settings = LegendaryLibrary.GetSettings();
        MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
        MaxWorkersNI.Value = settings!.MaxWorkers.ToString();
        MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
        ReorderingChk.IsChecked = settings.EnableReordering;

        var successUpdates = updatesList.Where(i => i.Value.Status == UpdateStatus.Available).ToDictionary(i => i.Key, i => i.Value);

        var checkedGames = updatesList.Where(i => i.Value.Status != UpdateStatus.Available)
                                      .ToDictionary(i => i.Key, i => i.Value);
        var failedGames = checkedGames.Any(i => i.Value.Status == UpdateStatus.Error);
        if (checkedGames.Count > 0 && successUpdates.Count == 0)
        {
            var noUpdatesMessage = LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable);
            var noUpdatesSeverity = MessageBoxSeverity.Information;
            if (failedGames)
            {
                noUpdatesMessage = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUpdateCheckFailMessage);
                noUpdatesSeverity = MessageBoxSeverity.Error;
            }

            var options = new List<MessageBoxResponse>
            {
                new(LocalizationManager.Instance.GetString(LOC.CommonReload)),
                new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteOkLabel), true, true)
            };
            var result = await playniteApi.Dialogs.ShowMessageAsync(
                noUpdatesMessage, LegendaryLibrary.LibraryName,
                noUpdatesSeverity, options, []);
            if (result == options[0])
            {
                var checkedGamesIds = checkedGames.Select(g => g.Key).ToList();
                var updateCheckProgressOptions =
                    new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false)
                        { IsIndeterminate = true };
                await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(updateCheckProgressOptions, async a =>
                {
                    LegendaryGames.ClearSpecificGamesCache(checkedGamesIds!);
                    var legendaryUpdateController = new LegendaryUpdateController();
                    if (checkedGamesIds.Count > 1)
                    {
                        updatesList = await legendaryUpdateController.CheckAllGamesUpdates();
                    }
                    else
                    {
                        updatesList = await legendaryUpdateController.CheckGameUpdates(checkedGames.First().Value.Title,
                            checkedGames.First().Key);
                    }
                });
                if (updatesList.All(i => i.Value.Status != UpdateStatus.Available))
                {
                    await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonNoUpdatesAvailable),
                        LegendaryLibrary.LibraryName);
                    Window.GetWindow(this)?.Close();
                    return;
                }

                RefreshWindow();
            }
            else
            {
                Window.GetWindow(this)?.Close();
            }
        }
    }

    private void RefreshWindow()
    {
        UpdateBtn.IsEnabled = false;
        DownloadSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);
        InstallSizeTB.Text = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoadingLabel);

        var successUpdates = updatesList.Where(i => i.Value.Status == UpdateStatus.Available).ToDictionary(i => i.Key, i => i.Value);
        UpdatesLB.ItemsSource = successUpdates;
        UpdatesLB.SelectAll();
        if (updatesList.Count > 0)
        {
            UpdateBtn.IsEnabled = true;
        }
    }

    private void UpdatesLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateBtn.IsEnabled = UpdatesLB.SelectedIndex != -1;
        double initialDownloadSizeNumber = 0;
        double initialInstallSizeNumber = 0;
        foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, UpdateInfo>>().ToList())
        {
            initialDownloadSizeNumber += selectedOption.Value.Download_size;
            initialInstallSizeNumber += selectedOption.Value.Disk_size;
        }

        var downloadSize = CommonHelpers.FormatSize(initialDownloadSizeNumber);
        DownloadSizeTB.Text = downloadSize;
        var installSize = CommonHelpers.FormatSize(initialInstallSizeNumber);
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
            MaxWorkersNI.MaxValue = CommonHelpers.CpuThreadsNumber;
            var maxWorkers = settings!.MaxWorkers;
            if (MaxWorkersNI.Value != "")
            {
                maxWorkers = int.Parse(MaxWorkersNI.Value);
            }

            var maxSharedMemory = settings.MaxSharedMemory;
            if (MaxSharedMemoryNI.Value != "")
            {
                maxSharedMemory = int.Parse(MaxSharedMemoryNI.Value);
            }

            var legendaryUpdateController = new LegendaryUpdateController();
            var downloadProperties = new DownloadProperties
            {
                DownloadAction = DownloadAction.Update,
                MaxWorkers = maxWorkers,
                MaxSharedMemory = maxSharedMemory,
                EnableReordering = (bool)ReorderingChk.IsChecked!,
                IgnoreFreeSpace = (bool)IgnoreFreeSpaceChk.IsChecked!
            };
            Window.GetWindow(this)?.Close();
            var newUpdatesList = new Dictionary<string, UpdateInfo>();
            foreach (var selectedOption in UpdatesLB.SelectedItems.Cast<KeyValuePair<string, UpdateInfo>>().ToList())
            {
                newUpdatesList.Add(selectedOption.Key, selectedOption.Value);
            }

            await legendaryUpdateController.UpdateGame(newUpdatesList, "", false, downloadProperties);
        }
    }
}