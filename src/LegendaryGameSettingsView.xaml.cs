using System.Windows;
using CommonPlugin;
using LegendaryLibraryNS.Models;
using Playnite;

namespace LegendaryLibraryNS;

/// <summary>
/// Interaction logic for LegendaryGameSettingsView.xaml
/// </summary>
public partial class LegendaryGameSettingsView
{
    private LegendaryGameSettingsViewModel Vm => (DataContext as LegendaryGameSettingsViewModel)!;
    private Game Game => Vm.Game;
    private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
    public GameSettings? GameSettings;
    private CommonHelpers commonHelpers = LegendaryLibrary.Instance.CommonHelpers;


    public LegendaryGameSettingsView()
    {
        InitializeComponent();
    }

    private void LegendaryGameSettingsViewUC_Loaded(object sender, RoutedEventArgs e)
    {
        commonHelpers.SetControlBackground(this);
        var appList = LegendaryLauncher.GetInstalledAppList();
        if (appList.ContainsKey(Game.LibraryGameId!))
        {
            if (appList[Game.LibraryGameId!].Can_run_offline)
            {
                EnableOfflineModeChk.IsEnabled = true;
            }

            GameVersionTxt.Text = appList[Game.LibraryGameId!].Version;
        }
        else
        {
            VersionSP.Visibility = Visibility.Collapsed;
        }
    }

    private async void ChooseAlternativeExeBtn_Click(object sender, RoutedEventArgs e)
    {
        var fileTypes = new Dictionary<string, string[]>
        {
            { LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExecutableTitle), ["*.exe"] }
        };
        var files = await playniteApi.Dialogs.SelectFileAsync(fileTypes, initialDir: Game.InstallDirectory);
        if (files is { Count: > 0 })
        {
            if (!string.IsNullOrEmpty(Game.InstallDirectory))
            {
                SelectedAlternativeExeTxt.Text = RelativePath.Get(Game.InstallDirectory, files[0]);
            }
        }
    }
}