using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Playnite;
using Playnite.SDK;
using Playnite.Common;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryLibrarySettingsView.xaml
    /// </summary>
    public partial class LegendaryLibrarySettingsView : UserControl
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        public LegendaryLibrarySettingsView()
        {
            InitializeComponent();
            if (!LegendaryLauncher.IsEOSOverlayInstalled)
            {
                EOSOInstallBtn.Visibility = Visibility.Visible;
                EOSODisableBtn.Visibility = Visibility.Hidden;
                EOSOUninstallBtn.Visibility = Visibility.Hidden;
            }
        }

        private void ChooseLauncherBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedLauncherPathTxtBox.Text = path;
            }
        }

        private void ChooseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxtBox.Text = path;
            }
        }

        private void ExcludeOnlineGamesBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowCloseButton = false,
            });
            window.Content = new LegendaryExcludeOnlineGames();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.Height = 450;
            window.Width = 800;
            window.Title = ResourceProvider.GetString("LOCLegendaryExcludeGames");
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private void EOSOUninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = playniteAPI.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString("LOCLegendaryUninstallGame"), ResourceProvider.GetString("LOCLegendaryEOSOverlay")), ResourceProvider.GetString("LOCUninstallGame"), MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                ProcessStarter.StartProcessWait(LegendaryLauncher.ClientExecPath, "-y eos-overlay remove", null, false);
                EOSOInstallBtn.Visibility = Visibility.Visible;
                EOSOUninstallBtn.Visibility = Visibility.Hidden;
                EOSODisableBtn.Visibility = Visibility.Hidden;
                EOSOEnableBtn.Visibility = Visibility.Hidden;
            }
        }

        private void EOSOInstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
                ShowCloseButton = false,
            });
            window.Title = ResourceProvider.GetString("LOCLegendaryEOSOverlay");
            window.DataContext = "eos-overlay";
            window.Content = new LegendaryGameInstaller("eos-overlay");
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.Height = 180;
            window.Width = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = window.ShowDialog();
            if (result == false)
            {
                if (LegendaryLauncher.IsEOSOverlayInstalled)
                {
                    EOSOInstallBtn.Visibility = Visibility.Hidden;
                    EOSOUninstallBtn.Visibility = Visibility.Visible;
                    EOSODisableBtn.Visibility = Visibility.Visible;
                }
            }
        }

        private void ImportEGL_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                ProcessStarter.StartProcessWait(LegendaryLauncher.ClientExecPath, "-y import "+ new System.IO.DirectoryInfo(path).Name + " "+path, null, false);
                ProcessStarter.StartProcessWait(LegendaryLauncher.ClientExecPath, "-y repair " + new System.IO.DirectoryInfo(path).Name, null, false);
            }
        }
    }
}
