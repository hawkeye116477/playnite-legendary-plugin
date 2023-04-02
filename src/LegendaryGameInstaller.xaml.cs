using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryGameInstaller.xaml
    /// </summary>
    public partial class LegendaryGameInstaller : UserControl
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;

        public LegendaryGameInstaller()
        {
            InitializeComponent();
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
        }

        private void InstallButton_Click(object sender, RoutedEventArgs e)
        {
            var installerWindow = Window.GetWindow(this);
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
            if (gameID == "eos-overlay")
            {
                installPath = System.IO.Path.Combine(SelectedGamePathTxtBox.Text, ".overlay");
                installCommand = "-y eos-overlay install --path " + installPath;
            }
            var proc = ProcessStarter.StartProcess(LegendaryLauncher.ClientExecPath, installCommand);
            if (gameID == "eos-overlay")
            {
                proc.WaitForExit();
            }
            installerWindow.DialogResult = true;
            installerWindow.Close();
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

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            var installerWindow = Window.GetWindow(this);
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                var gameID = DataContext.ToString();
                ProcessStarter.StartProcessWait(LegendaryLauncher.ClientExecPath, "-y import " + gameID + " " + path, null, false);
                ProcessStarter.StartProcessWait(LegendaryLauncher.ClientExecPath, "-y repair " + gameID, null, false);
                installerWindow.DialogResult = true;
                installerWindow.Close();
            }
        }

        private async void LegendaryGameInstallerUC_Loaded(object sender, RoutedEventArgs e)
        {
            SelectedGamePathTxtBox.Text = LegendaryLibrary.GetSettings().GamesInstallationPath;
            var gameID = DataContext.ToString();
            if (gameID != "eos-overlay")
            {
                var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                    .WithArguments(new[] { "info", gameID, "--json" })
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync();
                if (result.ExitCode != 0)
                {
                    logger.Error(result.StandardError);
                    Window.GetWindow(this).Close();
                }
                else
                {
                    var manifest = Serialization.FromJson<LegendaryGameInfo.Rootobject>(result.StandardOutput);
                    downloadSize.Content = FormatSize(manifest.Manifest.Download_size);
                    installSize.Content = FormatSize(manifest.Manifest.Disk_size);
                }
            }
            else
            {
                importButton.IsEnabled = false;
                importButton.Visibility = Visibility.Hidden;
                importButton.Width = 0;
                importButton.Height = 0;

                var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                    .WithArguments(new[] { gameID, "install" })
                    .WithStandardInputPipe(PipeSource.FromString("n"))
                    .ExecuteBufferedAsync();
                string[] lines = result.StandardError.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                foreach (var line in lines)
                {
                    if (line.Contains("Download size:"))
                    {
                        var downloadSizeValue = double.Parse(line.Substring(line.IndexOf("Download size:") + 15).Replace(" MiB", ""), CultureInfo.InvariantCulture) * 1024 * 1024;
                        downloadSize.Content = FormatSize(downloadSizeValue);
                    }
                    if (line.Contains("Install size:"))
                    {
                        var installSizeValue = double.Parse(line.Substring(line.IndexOf("Install size:") + 14).Replace(" MiB", ""), CultureInfo.InvariantCulture) * 1024 * 1024;
                        installSize.Content = FormatSize(installSizeValue);
                    }
                }
            }
            installButton.IsEnabled = true;
        }
    }
}
