using System.Windows;
using System.Windows.Controls;
using CommonPlugin;
using CommonPlugin.Enums;
using Playnite;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace LegendaryLibraryNS;

public partial class LegendaryGameSettingsCloudView : UserControl
{
    private LegendaryGameSettingsViewModel Vm => (DataContext as LegendaryGameSettingsViewModel)!;
    private Game Game => Vm.Game;
    private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
    private string? cloudPath;
    private ILogger logger = LogManager.GetLogger<LegendaryGameSettingsCloudView>();

    public LegendaryGameSettingsCloudView()
    {
        InitializeComponent();
    }

    private async void CalculatePathBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(cloudPath))
        {
            cloudPath = await LegendaryCloud.CalculateGameSavesPath(Game.Name, Game.LibraryGameId!, Game.InstallDirectory!, false);
        }

        if (!cloudPath.IsNullOrEmpty())
        {
            SelectedSavePathTxt.Text = cloudPath;
        }
    }

    private async void ChooseSavePathBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = await playniteApi.Dialogs.SelectFolderAsync();
        if (result is { Count: > 0 })
        {
            SelectedSavePathTxt.Text = result[0];
        }
    }

    private async void AutoSyncSavesChk_Click(object sender, RoutedEventArgs e)
    {
        if (AutoSyncSavesChk.IsChecked == true)
        {
            await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonSyncGameSavesWarn), "",
                MessageBoxButtons.OK, MessageBoxSeverity.Warning);
        }
    }

    private async void SyncSavesBtn_Click(object sender, RoutedEventArgs e)
    {
        var result = await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonCloudSaveConfirm),
            LocalizationManager.Instance.GetString(LOC.CommonCloudSaves), MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
        if (result == MessageBoxResult.Yes)
        {
            var forceCloudSync = (bool)ForceCloudActionChk.IsChecked!;
            var selectedCloudSyncAction = (CloudSyncAction)ManualSyncSavesCBo.SelectedValue;
            var selectedSavePath = SelectedSavePathTxt.Text;
            if (selectedSavePath != "")
            {
                await LegendaryCloud.SyncGameSaves(Game, selectedCloudSyncAction, forceCloudSync, true, true, selectedSavePath);
            }
            else
            {
                await LegendaryCloud.SyncGameSaves(Game, selectedCloudSyncAction, forceCloudSync, true);
            }
        }
    }

    private void LegendaryGameSettingsCloudView_OnInitialized(object? sender, EventArgs e)
    {
        var cloudSyncActions = new Dictionary<CloudSyncAction, string>
        {
            { CloudSyncAction.Download, LocalizationManager.Instance.GetString(LOC.CommonDownload) },
            { CloudSyncAction.Upload, LocalizationManager.Instance.GetString(LOC.CommonUpload) }
        };
        ManualSyncSavesCBo.ItemsSource = cloudSyncActions;
        ManualSyncSavesCBo.SelectedIndex = 0;

        Dispatcher.BeginInvoke((Action)(async void () =>
        {
            try
            {
                cloudPath = await LegendaryCloud.CalculateGameSavesPath(Game.Name, Game.LibraryGameId!, Game.InstallDirectory!);
                if (cloudPath.IsNullOrEmpty())
                {
                    CloudSavesSP.IsEnabled = false;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }
        }));
    }
}