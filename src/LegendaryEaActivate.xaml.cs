using CliWrap;
using CliWrap.Buffered;
using CliWrap.EventStream;
using CommonPlugin;
using LegendaryLibraryNS.Models;
using Linguini.Shared.Types.Bundle;
using Playnite.Common;
using Playnite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace LegendaryLibraryNS;

/// <summary>
/// Interaction logic for LegendaryEaActivate.xaml
/// </summary>
public partial class LegendaryEaActivate
{
    private ILogger logger = LogManager.GetLogger();
    private IPlayniteApi playniteApi = LegendaryLibrary.PlayniteApi;
    private readonly CommonHelpers commonHelpers = LegendaryLibrary.Instance.CommonHelpers;

    public LegendaryEaActivate()
    {
        InitializeComponent();
    }

    private async void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        commonHelpers.SetControlBackground(this);
        EaGamesSP.Visibility = Visibility.Collapsed;
        LoadingEaTB.Visibility = Visibility.Visible;
        if (!LegendaryLauncher.IsInstalled)
        {
            await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonLauncherNotInstalled));
            return;
        }

        var cacheInfoPath = LegendaryLibrary.Instance.GetCachePath("infocache");
        var cacheInfoFile = Path.Combine(cacheInfoPath, "_allEaGames.json");
        var eaGamesOnly = new List<LegendaryMetadata>();
        var correctJson = false;
        if (File.Exists(cacheInfoFile))
        {
            if (File.GetLastWriteTime(cacheInfoFile) < DateTime.Now.AddDays(-7))
            {
                File.Delete(cacheInfoFile);
            }
        }

        List<LegendaryMetadata>? eaGamesOutput;
        string content;
        if (File.Exists(cacheInfoFile))
        {
            content = FileSystem.ReadFileAsStringSafe(cacheInfoFile);
            if (!content.IsNullOrEmpty())
            {
                if (Serialization.TryFromJson(content, out eaGamesOutput))
                {
                    if (eaGamesOutput != null)
                    {
                        foreach (var eaGame in eaGamesOutput)
                        {
                            eaGamesOnly.Add(eaGame);
                        }

                        correctJson = true;
                    }
                }
            }
        }

        if (!correctJson)
        {
            var result = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                  .WithArguments(["list", "-T", "--json", "--force-refresh"])
                                  .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                                  .AddCommandToLog()
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
                    await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(
                        LOC.ThirdPartyPlayniteMetadataDownloadError,
                        new Dictionary<string, IFluentType>
                            { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired) }));
                }
                else
                {
                    await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(
                        LOC.ThirdPartyPlayniteMetadataDownloadError,
                        new Dictionary<string, IFluentType>
                            { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }));
                }
            }
            else
            {
                var jediFound = false;
                if (Serialization.TryFromJson(result.StandardOutput, out eaGamesOutput))
                {
                    if (eaGamesOutput != null)
                    {
                        foreach (var eaGame in eaGamesOutput)
                        {
                            var thirdPartyManagedApp = eaGame.Metadata?.CustomAttributes?.ThirdPartyManagedApp
                                                            ?.Value?.ToLower();
                            if (thirdPartyManagedApp is "origin" or "the ea app")
                            {
                                if (thirdPartyManagedApp == "the ea app")
                                {
                                    eaGame.Metadata?.CustomAttributes?.ThirdPartyManagedApp?.Value = "Origin";
                                    var metadataFile = Path.Combine(LegendaryLauncher.ConfigPath, "metadata",
                                        eaGame.App_name + ".json");
                                    if (File.Exists(metadataFile))
                                    {
                                        content = FileSystem.ReadFileAsStringSafe(metadataFile);
                                        if (!content.IsNullOrEmpty() &&
                                            Serialization.TryFromJson(content, out LegendaryMetadata? eaGameMeta))
                                        {
                                            if (eaGameMeta != null)
                                            {
                                                eaGameMeta.Metadata?.CustomAttributes?.ThirdPartyManagedApp?.Value = "Origin";
                                                var strConf = Serialization.ToJson(eaGameMeta, true);
                                                await File.WriteAllTextAsync(metadataFile, strConf);
                                            }
                                        }
                                    }
                                }

                                eaGame.App_title = eaGame.App_title.RemoveTrademarks();
                                eaGamesOnly.Add(eaGame);
                                if (!jediFound && eaGame.App_title.Contains("Star Wars"))
                                {
                                    jediFound = true;
                                }
                            }
                        }
                    }
                }

                if (eaGamesOnly.Count > 0)
                {
                    if (jediFound)
                    {
                        await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.LegendaryStarWarsMessage),
                            "", MessageBoxButtons.OK, MessageBoxSeverity.Information);
                    }

                    commonHelpers.SaveJsonSettingsToFile(eaGamesOnly, cacheInfoPath, "_allEaGames");
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
        await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.LegendaryEaNotice), "",
            MessageBoxButtons.OK, MessageBoxSeverity.Information);
        var errorDisplayed = false;
        var i = 0;
        foreach (var selectedGame in EaGamesLB.SelectedItems.Cast<LegendaryMetadata>())
        {
            var canActivate = true;
            i++;
            if (i > 1)
            {
                var confirmActivate = await playniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.LegendaryActivateNextConfirm),
                    "",
                    MessageBoxButtons.YesNo,
                    MessageBoxSeverity.Question);
                if (confirmActivate == MessageBoxResult.No)
                {
                    canActivate = false;
                }
            }

            if (canActivate)
            {
                var stdOutBuffer = new StringBuilder();
                var cmd = Cli.Wrap(LegendaryLauncher.ClientExecPath)
                             .WithArguments(["launch", selectedGame.App_name, "--origin"])
                             .WithEnvironmentVariables(await LegendaryLauncher.GetDefaultEnvironmentVariables())
                             .AddCommandToLog()
                             .WithValidation(CommandResultValidation.None);
                await foreach (var cmdEvent in cmd.ListenAsync())
                {
                    switch (cmdEvent)
                    {
                        case StartedCommandEvent started:
                            break;
                        case StandardErrorCommandEvent stdErr:
                            stdOutBuffer.AppendLine(stdErr.Text);
                            break;
                        case ExitedCommandEvent exited:
                            if (exited.ExitCode != 0)
                            {
                                var errorMessage = stdOutBuffer.ToString();
                                if (errorMessage.Contains("ERROR") || errorMessage.Contains("WARNING") ||
                                    errorMessage.Contains("exceptions"))
                                {
                                    errorDisplayed = true;
                                    logger.Error(errorMessage);
                                }
                                else if (errorMessage.Contains("Failed to establish a new connection")
                                         || errorMessage.Contains("Log in failed")
                                         || errorMessage.Contains("Login failed")
                                         || errorMessage.Contains("No saved credentials"))
                                {
                                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteLoginRequired));
                                }
                            }

                            break;
                    }
                }
            }
        }

        if (errorDisplayed)
        {
            await playniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateFailure,
                new Dictionary<string, IFluentType>
                {
                    ["companyAccount"] = (FluentString)"EA",
                    ["reason"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog)
                }));
        }
        else
        {
            await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.LegendaryGamesActivateSuccess,
                new Dictionary<string, IFluentType> { ["companyAccount"] = (FluentString)"EA" }));
        }
    }
}