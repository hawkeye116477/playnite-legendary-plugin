using CliWrap;
using CliWrap.Buffered;
using Playnite;
using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
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
    /// Interaction logic for LegendaryLibrarySettingsView.xaml
    /// </summary>
    public partial class LegendaryLibrarySettingsView : UserControl
    {
        private IPlayniteAPI playniteAPI = API.Instance;
        public LegendaryLibrarySettingsView()
        {
            InitializeComponent();
        }

        private void ChooseLauncherBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedLauncherPathTxt.Text = path;
            }
        }

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
            }
        }

        private void ExcludeOnlineGamesBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMinimizeButton = false,
            });
            window.Content = new LegendaryExcludeOnlineGames();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.Height = 450;
            window.Width = 800;
            window.Title = ResourceProvider.GetString(LOC.LegendaryExcludeGames);
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            window.ShowDialog();
        }

        private async void EOSOUninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var result = playniteAPI.Dialogs.ShowMessage(
                string.Format(ResourceProvider.GetString(LOC.LegendaryUninstallGameConfirm), ResourceProvider.GetString(LOC.LegendaryEOSOverlay)), ResourceProvider.GetString("LOCUninstallGame"), MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                   .WithValidation(CommandResultValidation.None)
                                   .WithArguments(new[] { "-y", "eos-overlay", "remove" })
                                   .ExecuteBufferedAsync();
                if (cmd.StandardError.Contains("Done"))
                {
                    EOSOInstallBtn.Visibility = Visibility.Visible;
                    EOSOUninstallBtn.Visibility = Visibility.Collapsed;
                    EOSOToggleBtn.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void EOSOInstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var window = playniteAPI.Dialogs.CreateWindow(new WindowCreationOptions
            {
                ShowMaximizeButton = false
            });
            window.Title = ResourceProvider.GetString(LOC.LegendaryEOSOverlay);
            window.DataContext = "eos-overlay";
            window.Content = new LegendaryGameInstaller();
            window.Owner = playniteAPI.Dialogs.GetCurrentAppWindow();
            window.SizeToContent = SizeToContent.Height;
            window.Width = 600;
            window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            var result = window.ShowDialog();
            if (result == true)
            {
                if (LegendaryLauncher.IsEOSOverlayInstalled)
                {
                    EOSOInstallBtn.Visibility = Visibility.Collapsed;
                    EOSOUninstallBtn.Visibility = Visibility.Visible;
                    EOSOToggleBtn.Content = ResourceProvider.GetString(LOC.LegendaryDisable);
                    EOSOToggleBtn.Visibility = Visibility.Visible;
                }
            }
        }

        private async void EOSOToggleBtn_Click(object sender, RoutedEventArgs e)
        {
            var toggleCommand = "disable";
            if (!LegendaryLauncher.IsEOSOverlayEnabled)
            {
                toggleCommand = "enable";
            }
            await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                     .WithArguments(new[] { "-y", "eos-overlay", toggleCommand })
                     .WithValidation(CommandResultValidation.None)
                     .ExecuteAsync();
            var toggleTxt = LOC.LegendaryEnable;
            if (LegendaryLauncher.IsEOSOverlayEnabled)
            {
                toggleTxt = LOC.LegendaryDisable;
            }
            EOSOToggleBtn.Content = ResourceProvider.GetString(toggleTxt);
        }

        private void LegendarySettingsUC_Loaded(object sender, RoutedEventArgs e)
        {
            if (!LegendaryLauncher.IsEOSOverlayInstalled)
            {
                EOSOInstallBtn.Visibility = Visibility.Visible;
                EOSOToggleBtn.Visibility = Visibility.Collapsed;
                EOSOUninstallBtn.Visibility = Visibility.Collapsed;
            }
            else
            {
                if (!LegendaryLauncher.IsEOSOverlayEnabled)
                {
                    EOSOToggleBtn.Content = ResourceProvider.GetString(LOC.LegendaryEnable);
                }
            }

            var downloadCompleteActions = new Dictionary<int, string>
            {
                { (int)DownloadCompleteAction.Nothing, ResourceProvider.GetString("LOCDoNothing") },
                { (int)DownloadCompleteAction.ShutDown, ResourceProvider.GetString("LOCMenuShutdownSystem") },
                { (int)DownloadCompleteAction.Reboot, ResourceProvider.GetString("LOCMenuRestartSystem") },
                { (int)DownloadCompleteAction.Hibernate, ResourceProvider.GetString("LOCMenuHibernateSystem") },
                { (int)DownloadCompleteAction.Sleep, ResourceProvider.GetString("LOCMenuSuspendSystem") },
            };
            AfterDownloadCompleteCBo.ItemsSource = downloadCompleteActions;
        }

        private void Increment(TextBox textBox, int defaultValue, int maxValue, int stepValue)
        {
            int number;
            if (textBox.Text != "")
            {
                number = Convert.ToInt32(textBox.Text);
            }
            else
            {
                number = defaultValue;
            }
            if (number < maxValue)
            {
                textBox.Text = Convert.ToString(number + stepValue);
            }
        }

        private void Decrement(TextBox textBox, int defaultValue, int minValue, int stepValue)
        {
            int number;
            if (textBox.Text != "")
            {
                number = Convert.ToInt32(textBox.Text);
            }
            else
            {
                number = defaultValue;
            }
            if (number > minValue)
            {
                textBox.Text = Convert.ToString(number - stepValue);
            }
        }

        private void NumericTextChanged(TextBox textBox, int defaultValue, int minValue, int maxValue)
        {
            int number;
            if (textBox.Text != "")
            {
                if (!int.TryParse(textBox.Text, out number))
                {
                    textBox.Text = defaultValue.ToString();
                }
                if (number > maxValue)
                {
                    textBox.Text = maxValue.ToString();
                }
                if (number < minValue)
                {
                    textBox.Text = minValue.ToString();
                }
                textBox.SelectionStart = textBox.Text.Length;
            }

        }

        private void MoreWorkersRpt_Click(object sender, RoutedEventArgs e)
        {
            Increment(WorkersTxt, LegendaryLibrary.GetSettings().MaxWorkers, 16, 1);
        }

        private void LessWorkersRpt_Click(object sender, RoutedEventArgs e)
        {
            Decrement(WorkersTxt, LegendaryLibrary.GetSettings().MaxWorkers, 0, 1);
        }

        private void WorkersTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            NumericTextChanged(WorkersTxt, LegendaryLibrary.GetSettings().MaxWorkers, 0, 16);
        }

        private int TotalRAM
        {
            get
            {
                ObjectQuery wql = new ObjectQuery("SELECT * FROM Win32_OperatingSystem");
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(wql);
                ManagementObjectCollection results = searcher.Get();
                double ram = 0.0;
                foreach (ManagementObject result in results)
                {
                    ram = Convert.ToDouble(result["TotalVisibleMemorySize"].ToString().Replace("KB", ""));
                }
                ram = Math.Round(ram / 1024);
                return Convert.ToInt32(ram);
            }
        }

        private void MoreSharedMemoryRpt_Click(object sender, RoutedEventArgs e)
        {
            Increment(SharedMemoryTxt, LegendaryLibrary.GetSettings().MaxSharedMemory, TotalRAM, 128);
        }

        private void LessSharedMemoryRpt_Click(object sender, RoutedEventArgs e)
        {
            Decrement(SharedMemoryTxt, LegendaryLibrary.GetSettings().MaxSharedMemory, 0, 128);
        }

        private void SharedMemoryTxt_TextChanged(object sender, TextChangedEventArgs e)
        {
            NumericTextChanged(SharedMemoryTxt, LegendaryLibrary.GetSettings().MaxSharedMemory, 0, TotalRAM);
        }
    }
}
