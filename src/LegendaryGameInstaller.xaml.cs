using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using LegendaryLibraryNS.Enums;
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
using System.Media;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryGameInstaller.xaml
    /// </summary>
    public partial class LegendaryGameInstaller : UserControl
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;
        public string installCommand;
        public string downloadSize;
        public string installSize;
        public List<string> requiredThings;
        public double downloadSizeNumber;
        public double installSizeNumber;
        private LegendaryGameInfo.Rootobject manifest;

        public LegendaryGameInstaller()
        {
            InitializeComponent();
            SetControlStyles();
        }

        public Window InstallerWindow => Window.GetWindow(this);

        public DownloadManagerData.Download InstallData => (DownloadManagerData.Download)DataContext;
        public string GameID => InstallData.gameID;

        private void ChooseGamePathBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                SelectedGamePathTxt.Text = path;
                UpdateSpaceInfo(path);
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Window.GetWindow(this).Close();
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            var settings = LegendaryLibrary.GetSettings();
            var installPath = settings.GamesInstallationPath;
            if (SelectedGamePathTxt.Text != "")
            {
                installPath = SelectedGamePathTxt.Text;
            }
            if (GameID == "eos-overlay")
            {
                installPath = Path.Combine(SelectedGamePathTxt.Text, ".overlay");
            }
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
            var selectedExtraContent = new List<string>();
            if (requiredThings.Count > 0)
            {
                selectedExtraContent.Add("");
                foreach (var requiredThing in requiredThings)
                {
                    selectedExtraContent.Add(requiredThing);
                }
            }
            if (ExtraContentLB.Items.Count > 0)
            {
                selectedExtraContent.AddMissing("");
                foreach (var selectedOption in ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList())
                {
                    foreach (var tag in selectedOption.Value.Tags)
                    {
                        selectedExtraContent.AddMissing(tag);
                    }
                }
            }
            InstallerWindow.Close();
            LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
            var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == GameID);
            if (wantedItem != null)
            {
                playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), wantedItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                var messagesSettings = LegendaryMessagesSettings.LoadSettings();
                if (!messagesSettings.DontShowDownloadManagerWhatsUpMsg)
                {
                    var okResponse = new MessageBoxOption("LOCOKLabel", true, true);
                    var dontShowResponse = new MessageBoxOption("LOCDontShowAgainTitle");
                    var response = playniteAPI.Dialogs.ShowMessage(LOC.LegendaryDownloadManagerWhatsUp, "", MessageBoxImage.Information, new List<MessageBoxOption> { okResponse, dontShowResponse });
                    if (response == dontShowResponse)
                    {
                        messagesSettings.DontShowDownloadManagerWhatsUpMsg = true;
                        LegendaryMessagesSettings.SaveSettings(messagesSettings);
                    }
                }

                DownloadProperties downloadProperties = new DownloadProperties()
                {
                    installPath = installPath,
                    downloadAction = (int)DownloadAction.Install,
                    enableReordering = enableReordering,
                    maxWorkers = maxWorkers,
                    maxSharedMemory = maxSharedMemory,
                    extraContent = selectedExtraContent
                };
                await downloadManager.EnqueueJob(GameID, InstallerWindow.Title, downloadSize, installSize, downloadProperties);
            }
        }

        private async void ImportBtn_Click(object sender, RoutedEventArgs e)
        {
            var path = playniteAPI.Dialogs.SelectFolder();
            if (path != "")
            {
                var importCmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                         .WithArguments(new[] { "-y", "import", GameID, path })
                                         .WithValidation(CommandResultValidation.None)
                                         .ExecuteBufferedAsync();
                if (importCmd.StandardError.Contains("has been imported"))
                {
                    InstallerWindow.DialogResult = true;
                }
                else
                {
                    logger.Debug("[Legendary] " + importCmd.StandardError);
                    logger.Error("[Legendary] exit code: " + importCmd.ExitCode);
                }
                InstallerWindow.Close();
            }
        }

        private async void LegendaryGameInstallerUC_Loaded(object sender, RoutedEventArgs e)
        {
            if (InstallData.downloadProperties.downloadAction == (int)DownloadAction.Repair)
            {
                FolderDP.Visibility = Visibility.Collapsed;
                ImportBtn.Visibility = Visibility.Collapsed;
                InstallBtn.Visibility = Visibility.Collapsed;
                RepairBtn.Visibility = Visibility.Visible;
            }
            var settings = LegendaryLibrary.GetSettings();
            SelectedGamePathTxt.Text = settings.GamesInstallationPath;
            ReorderingChk.IsChecked = settings.EnableReordering;
            MaxWorkersNI.Value = settings.MaxWorkers.ToString();
            MaxSharedMemoryNI.Value = settings.MaxSharedMemory.ToString();
            if (!SelectedGamePathTxt.Text.IsNullOrEmpty())
            {
                UpdateSpaceInfo(SelectedGamePathTxt.Text);
            }
            else
            {
                UpdateSpaceInfo(settings.GamesInstallationPath);
            }
            requiredThings = new List<string>();
            var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
            var cacheInfoFile = Path.Combine(cacheInfoPath, GameID + ".json");
            if (!Directory.Exists(cacheInfoPath))
            {
                Directory.CreateDirectory(cacheInfoPath);
            }
            if (GameID != "eos-overlay")
            {
                bool correctJson = false;
                if (File.Exists(cacheInfoFile))
                {
                    if (File.GetLastWriteTime(cacheInfoFile) < DateTime.Now.AddDays(-7))
                    {
                        File.Delete(cacheInfoFile);
                    }
                    if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(cacheInfoFile), out manifest))
                    {
                        if (manifest != null && manifest.Manifest != null)
                        {
                            correctJson = true;
                        }
                    }
                }
                if (!correctJson)
                {
                    var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                      .WithArguments(new[] { "info", GameID, "--json" })
                                      .WithValidation(CommandResultValidation.None)
                                      .ExecuteBufferedAsync();
                    if (result.ExitCode != 0)
                    {
                        logger.Error("[Legendary]" + result.StandardError);
                        if (result.StandardError.Contains("Log in failed"))
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)));
                        }
                        else
                        {
                            playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                        }
                        Window.GetWindow(this).Close();
                        return;
                    }
                    else
                    {
                        File.WriteAllText(cacheInfoFile, result.StandardOutput);
                        manifest = Serialization.FromJson<LegendaryGameInfo.Rootobject>(result.StandardOutput);
                    }
                }
                if (manifest.Manifest.Install_tags.Length > 1)
                {
                    downloadSizeNumber = 0;
                    installSizeNumber = 0;
                    var cacheSDLPath = LegendaryLibrary.Instance.GetCachePath("sdlcache");
                    var cacheSDLFile = Path.Combine(cacheSDLPath, GameID + ".json");
                    string content = null;
                    if (File.Exists(cacheSDLFile))
                    {
                        if (File.GetLastWriteTime(cacheSDLFile) < DateTime.Now.AddDays(-7))
                        {
                            File.Delete(cacheSDLFile);
                        }
                    }
                    if (!File.Exists(cacheSDLFile))
                    {
                        var httpClient = new HttpClient();
                        var response = await httpClient.GetAsync("https://api.legendary.gl/v1/sdl/" + GameID + ".json");
                        if (response.IsSuccessStatusCode)
                        {
                            content = await response.Content.ReadAsStringAsync();
                            if (!Directory.Exists(cacheSDLPath))
                            {
                                Directory.CreateDirectory(cacheSDLPath);
                            }
                            File.WriteAllText(cacheSDLFile, content);
                        }
                        httpClient.Dispose();
                    }
                    else
                    {
                        content = FileSystem.ReadFileAsStringSafe(cacheSDLFile);
                    }
                    if (content.IsNullOrEmpty())
                    {
                        logger.Error("An error occurred while downloading SDL data.");
                    }
                    if (Serialization.TryFromJson<Dictionary<string, LegendarySDLInfo>>(content, out var sdlInfo))
                    {
                        if (sdlInfo.ContainsKey("__required"))
                        {
                            foreach (var tag in sdlInfo["__required"].Tags)
                            {
                                foreach (var tagDo in manifest.Manifest.Tag_download_size)
                                {
                                    if (tagDo.Tag == tag)
                                    {
                                        downloadSizeNumber += tagDo.Size;
                                        break;
                                    }
                                }
                                foreach (var tagDi in manifest.Manifest.Tag_disk_size)
                                {
                                    if (tagDi.Tag == tag)
                                    {
                                        installSizeNumber += tagDi.Size;
                                        break;
                                    }
                                }
                                requiredThings.Add(tag);
                            }
                            sdlInfo.Remove("__required");
                        }
                        foreach (var tagDo in manifest.Manifest.Tag_download_size)
                        {
                            if (tagDo.Tag == "")
                            {
                                downloadSizeNumber += tagDo.Size;
                                break;
                            }
                        }
                        foreach (var tagDi in manifest.Manifest.Tag_disk_size)
                        {
                            if (tagDi.Tag == "")
                            {
                                installSizeNumber += tagDi.Size;
                                break;
                            }
                        }
                        ExtraContentLB.ItemsSource = sdlInfo;
                        ExtraContentBrd.Visibility = Visibility.Visible;
                        downloadSize = Helpers.FormatSize(downloadSizeNumber);
                        installSize = Helpers.FormatSize(installSizeNumber);
                    }
                }
                if (downloadSize.IsNullOrEmpty() || installSize.IsNullOrEmpty())
                {
                    downloadSize = Helpers.FormatSize(manifest.Manifest.Download_size);
                    installSize = Helpers.FormatSize(manifest.Manifest.Disk_size);
                }
                DownloadSizeTB.Text = downloadSize;
                InstallSizeTB.Text = installSize;
            }
            else
            {
                ImportBtn.IsEnabled = false;
                ImportBtn.Visibility = Visibility.Collapsed;

                var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                      .WithArguments(new[] { GameID, "install" })
                                      .WithStandardInputPipe(PipeSource.FromString("n"))
                                      .WithValidation(CommandResultValidation.None)
                                      .ExecuteBufferedAsync();
                if (result.ExitCode != 0)
                {
                    logger.Error("[Legendary]" + result.StandardError);
                    if (result.StandardError.Contains("Failed to establish a new connection"))
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)));
                    }
                    else
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(string.Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteGameInstallError), ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                    }
                    Window.GetWindow(this).Close();
                }
                else
                {
                    string[] lines = result.StandardError.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        if (line.Contains("Download size:"))
                        {
                            var downloadSizeValue = double.Parse(line.Substring(line.IndexOf("Download size:") + 15).Replace(" MiB", ""), CultureInfo.InvariantCulture) * 1024 * 1024;
                            downloadSize = Helpers.FormatSize(downloadSizeValue);
                            DownloadSizeTB.Text = downloadSize;
                        }
                        if (line.Contains("Install size:"))
                        {
                            var installSizeValue = double.Parse(line.Substring(line.IndexOf("Install size:") + 14).Replace(" MiB", ""), CultureInfo.InvariantCulture) * 1024 * 1024;
                            installSize = Helpers.FormatSize(installSizeValue);
                            InstallSizeTB.Text = installSize;
                        }
                    }
                }

            }
            InstallBtn.IsEnabled = true;
            RepairBtn.IsEnabled = true;
        }

        private void UpdateSpaceInfo(string path)
        {
            DriveInfo dDrive = new DriveInfo(path);
            if (dDrive.IsReady)
            {
                SpaceTB.Text = Helpers.FormatSize(dDrive.AvailableFreeSpace);
            }
        }

        private void ExtraContentLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var initialDownloadSizeNumber = downloadSizeNumber;
            var initialInstallSizeNumber = installSizeNumber;
            foreach (var selectedOption in ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList())
            {
                foreach (var tag in selectedOption.Value.Tags)
                {
                    foreach (var tagDo in manifest.Manifest.Tag_download_size)
                    {
                        if (tagDo.Tag == tag)
                        {
                            initialDownloadSizeNumber += tagDo.Size;
                            break;
                        }
                    }
                    foreach (var tagDi in manifest.Manifest.Tag_disk_size)
                    {
                        if (tagDi.Tag == tag)
                        {
                            initialInstallSizeNumber += tagDi.Size;
                            break;
                        }
                    }
                }
            }
            downloadSize = Helpers.FormatSize(initialDownloadSizeNumber);
            DownloadSizeTB.Text = downloadSize;
            installSize = Helpers.FormatSize(initialInstallSizeNumber);
            InstallSizeTB.Text = installSize;
        }

        private void SetControlStyles()
        {
            var baseStyleName = "BaseTextBlockStyle";
            if (playniteAPI.ApplicationInfo.Mode == ApplicationMode.Fullscreen)
            {
                baseStyleName = "TextBlockBaseStyle";
                Resources.Add(typeof(Button), new Style(typeof(Button), null));
            }

            if (ResourceProvider.GetResource(baseStyleName) is Style baseStyle && baseStyle.TargetType == typeof(TextBlock))
            {
                var implicitStyle = new Style(typeof(TextBlock), baseStyle);
                Resources.Add(typeof(TextBlock), implicitStyle);
            }
        }

        private async void RepairBtn_Click(object sender, RoutedEventArgs e)
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
            var selectedExtraContent = new List<string>();
            if (requiredThings.Count > 0)
            {
                selectedExtraContent.Add("");
                foreach (var requiredThing in requiredThings)
                {
                    selectedExtraContent.Add(requiredThing);
                }
            }
            if (ExtraContentLB.Items.Count > 0)
            {
                selectedExtraContent.AddMissing("");
                foreach (var selectedOption in ExtraContentLB.SelectedItems.Cast<KeyValuePair<string, LegendarySDLInfo>>().ToList())
                {
                    foreach (var tag in selectedOption.Value.Tags)
                    {
                        selectedExtraContent.AddMissing(tag);
                    }
                }
            }
            InstallerWindow.Close();
            LegendaryDownloadManager downloadManager = LegendaryLibrary.GetLegendaryDownloadManager();
            var wantedItem = downloadManager.downloadManagerData.downloads.FirstOrDefault(item => item.gameID == GameID);
            if (wantedItem != null)
            {
                playniteAPI.Dialogs.ShowMessage(string.Format(ResourceProvider.GetString(LOC.LegendaryDownloadAlreadyExists), wantedItem.name), "", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                var messagesSettings = LegendaryMessagesSettings.LoadSettings();
                if (!messagesSettings.DontShowDownloadManagerWhatsUpMsg)
                {
                    var okResponse = new MessageBoxOption("LOCOKLabel", true, true);
                    var dontShowResponse = new MessageBoxOption("LOCDontShowAgainTitle");
                    var response = playniteAPI.Dialogs.ShowMessage(LOC.LegendaryDownloadManagerWhatsUp, "", MessageBoxImage.Information, new List<MessageBoxOption> { okResponse, dontShowResponse });
                    if (response == dontShowResponse)
                    {
                        messagesSettings.DontShowDownloadManagerWhatsUpMsg = true;
                        LegendaryMessagesSettings.SaveSettings(messagesSettings);
                    }
                }

                DownloadProperties downloadProperties = new DownloadProperties()
                {
                    installPath = "",
                    downloadAction = (int)DownloadAction.Repair,
                    enableReordering = enableReordering,
                    maxWorkers = maxWorkers,
                    maxSharedMemory = maxSharedMemory,
                    extraContent = selectedExtraContent
                };
                await downloadManager.EnqueueJob(GameID, InstallerWindow.Title, downloadSize, installSize, downloadProperties);
            }
        }
    }
}
