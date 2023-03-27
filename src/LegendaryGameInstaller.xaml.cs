using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryGameInstaller.xaml
    /// </summary>
    public partial class LegendaryGameInstaller : UserControl
    {
        private IPlayniteAPI playniteAPI = API.Instance;

        public LegendaryGameInstaller(string gameID)
        {
            InitializeComponent();
            SelectedGamePathTxtBox.Text = LegendaryLibrary.GetSettings().GamesInstallationPath;
            this.Dispatcher.BeginInvoke((Action)(() => {
                if (gameID != "eos-overlay")
                {
                    ProcessStarter.StartProcessWait(LegendaryLauncher.ClientExecPath, "info " + gameID + " --json", null, out var stdOut, out var stdErr);
                    var manifest = Serialization.FromJson<LegendaryGameInfo.Rootobject>(stdOut.ToString());
                    downloadSize.Content = FormatSize(manifest.Manifest.Download_size);
                    installSize.Content = FormatSize(manifest.Manifest.Disk_size);
                }
                else
                {
                    ISizePanel.Visibility = Visibility.Hidden;
                    DSizePanel.Visibility = Visibility.Hidden;
                }
                cancelButton.IsEnabled = true;
                installButton.IsEnabled = true;
            }));
        }

        private void ChooseGamePath_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxtBox.Text = path;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
            if (DataContext.ToString() != "eos-overlay")
            {
                throw new OperationCanceledException();
            }
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            var settings = LegendaryLibrary.GetSettings();
            var installPath = settings.GamesInstallationPath;
            if (SelectedGamePathTxtBox.Text != "")
            {
                installPath = SelectedGamePathTxtBox.Text;
            }
            var gameID = DataContext.ToString();
            var installCommand = "-y install " + gameID + " --base-path " + installPath;
            var prefferedCDN = settings.PreferredCDN;
            if (prefferedCDN != "")
            {
                installCommand += " --preferred-cdn " + prefferedCDN;
            }
            if (settings.NoHttps)
            {
                installCommand += " --no-https";
            }
            var proc = ProcessStarter.StartProcess(LegendaryLauncher.ClientExecPath, installCommand);
            if (gameID == "eos-overlay")
            {
                installPath = System.IO.Path.Combine(SelectedGamePathTxtBox.Text, ".overlay");
                installCommand = "-y eos-overlay install --path " + installPath;
                proc.WaitForExit();
            }
            Window.GetWindow(this).Close();
        }

        static readonly string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        public static string FormatSize(double size)
        {
            int i = 0;
            decimal number = (decimal)size;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                i++;
            }
            return string.Format("{0:n2} {1}", number, suffixes[i]);
        }
    }
}
