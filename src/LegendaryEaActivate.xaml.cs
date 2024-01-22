using CliWrap;
using CliWrap.Buffered;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LegendaryLibraryNS
{
    /// <summary>
    /// Interaction logic for LegendaryEaActivate.xaml
    /// </summary>
    public partial class LegendaryEaActivate : UserControl
    {
        private ILogger logger = LogManager.GetLogger();
        private IPlayniteAPI playniteAPI = API.Instance;

        public LegendaryEaActivate()
        {
            InitializeComponent();
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            EaGamesSP.Visibility = Visibility.Collapsed;
            LoadingEaTB.Visibility = Visibility.Visible;
            if (!LegendaryLauncher.IsInstalled)
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryLauncherNotInstalled));
                return;
            }
            var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
            var cacheInfoFile = Path.Combine(cacheInfoPath, "_allEaGames.json");
            var eaGamesOnly = new List<LegendaryMetadata.Rootobject>();
            bool correctJson = false;
            if (File.Exists(cacheInfoFile))
            {
                if (File.GetLastWriteTime(cacheInfoFile) < DateTime.Now.AddDays(-7))
                {
                    File.Delete(cacheInfoFile);
                }
            }

            var eaGamesOutput = new List<LegendaryMetadata.Rootobject>();
            string content;
            if (File.Exists(cacheInfoFile))
            {
                content = FileSystem.ReadFileAsStringSafe(cacheInfoFile);
                if (!content.IsNullOrEmpty())
                {
                    if (Serialization.TryFromJson(content, out eaGamesOutput))
                    {
                        foreach (LegendaryMetadata.Rootobject eaGame in eaGamesOutput)
                        {
                            eaGamesOnly.Add(eaGame);
                        }
                        correctJson = true;
                    }
                }
            }

            if (!correctJson)
            {
                var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                      .WithArguments(new[] { "list", "-T", "--json", "--force-refresh" })
                                      .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                      .WithValidation(CommandResultValidation.None)
                                      .ExecuteBufferedAsync();
                if (result.StandardOutput.IsNullOrEmpty())
                {
                    logger.Error("[Legendary]" + result.StandardError);
                    if (result.StandardError.Contains("Failed to establish a new connection")
                        || result.StandardError.Contains("Log in failed")
                        || result.StandardError.Contains("Login failed")
                        || result.StandardError.Contains("No saved credentials"))
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired)));
                    }
                    else
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteMetadataDownloadError).Format(ResourceProvider.GetString(LOC.LegendaryCheckLog)));
                    }
                }
                else
                {
                    bool jediFound = false;
                    if (Serialization.TryFromJson(result.StandardOutput, out eaGamesOutput))
                    {
                        foreach (LegendaryMetadata.Rootobject eaGame in eaGamesOutput)
                        {
                            if (eaGame.metadata?.customAttributes?.ThirdPartyManagedApp?.value == "Origin" || eaGame.metadata?.customAttributes?.ThirdPartyManagedApp?.value == "the EA app")
                            {
                                eaGame.app_title = eaGame.app_title.RemoveTrademarks();
                                eaGamesOnly.Add(eaGame);
                                if (!jediFound && eaGame.app_title.Contains("Star Wars"))
                                {
                                    jediFound = true;
                                }
                            }
                        }
                    }
                    if (eaGamesOnly.Count > 0)
                    {
                        if (jediFound)
                        {
                            playniteAPI.Dialogs.ShowMessage(LOC.LegendaryStarWarsMessage, "", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                        Helpers.SaveJsonSettingsToFile(eaGamesOnly, "_allEaGames", cacheInfoPath);
                    }
                }
            }

            EaGamesLB.ItemsSource = eaGamesOnly;
            LoadingEaTB.Visibility = Visibility.Collapsed;
            if (eaGamesOnly.Count > 0)
            {
                EaGamesSP.Visibility = Visibility.Visible;
            }
            else
            {
                NoEaGamesTB.Visibility = Visibility.Visible;
            }

        }

        private void EaGamesLB_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (EaGamesLB.SelectedIndex == -1)
            {
                ActivateBtn.IsEnabled = false;
            }
            else
            {
                ActivateBtn.IsEnabled = true;
            }
        }

        private void SelectAllBtn_Click(object sender, RoutedEventArgs e)
        {
            if (EaGamesLB.Items.Count > 0)
            {
                if (EaGamesLB.Items.Count == EaGamesLB.SelectedItems.Count)
                {
                    EaGamesLB.UnselectAll();
                }
                else
                {
                    EaGamesLB.SelectAll();
                }
            }
        }

        private async void ActivateBtn_Click(object sender, RoutedEventArgs e)
        {
            playniteAPI.Dialogs.ShowMessage(LOC.LegendaryEANotice, "", MessageBoxButton.OK, MessageBoxImage.Information);
            bool errorDisplayed = false;
            int i = 0;
            foreach (var selectedGame in EaGamesLB.SelectedItems.Cast<LegendaryMetadata.Rootobject>())
            {
                bool canActivate = true;
                i++;
                if (i > 1)
                {
                    var confirmActivate = playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryActivateNextConfirm),
                                         "",
                                         MessageBoxButton.YesNo,
                                         MessageBoxImage.Question);
                    if (confirmActivate == MessageBoxResult.No)
                    {
                        canActivate = false;
                    }
                }
                if (canActivate)
                {
                    var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                          .WithArguments(new[] { "launch", selectedGame.app_name, "--origin" })
                                          .WithEnvironmentVariables(LegendaryLauncher.DefaultEnvironmentVariables)
                                          .WithValidation(CommandResultValidation.None)
                                          .ExecuteBufferedAsync();
                    var errorMessage = result.StandardError;
                    if (errorMessage.Contains("ERROR") || errorMessage.Contains("WARNING"))
                    {
                        errorDisplayed = true;
                        logger.Error(errorMessage);
                    }
                    else if (errorMessage.Contains("Failed to establish a new connection")
                        || errorMessage.Contains("Log in failed")
                        || errorMessage.Contains("Login failed")
                        || errorMessage.Contains("No saved credentials"))
                    {
                        playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.Legendary3P_PlayniteLoginRequired));
                    }
                }
            }
            if (errorDisplayed)
            {
                playniteAPI.Dialogs.ShowErrorMessage(ResourceProvider.GetString(LOC.LegendaryGamesActivateFailure).Format("EA", ResourceProvider.GetString(LOC.LegendaryCheckLog)));
            }
            else
            {
                playniteAPI.Dialogs.ShowMessage(ResourceProvider.GetString(LOC.LegendaryGamesActivateSuccess).Format("EA"));
            }

        }
    }
}
