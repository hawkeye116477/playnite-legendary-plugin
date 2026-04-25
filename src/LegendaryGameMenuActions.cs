using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using LegendaryLibraryNS.Models;
using Linguini.Shared.Types.Bundle;
using Playnite;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace LegendaryLibraryNS;

public class LegendaryGameMenuActions(IPlayniteApi playniteApi, List<Game> games)
{
    private static readonly ILogger Logger = LogManager.GetLogger();
    private IPlayniteApi PlayniteApi { get; set; } = playniteApi;
    private Game Game { get; set; } = games.First();

    public void OpenLauncherSettingsWindow()
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var window = PlayniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false
        });
        window.DataContext = Game;
        window.Title =
            $"{LocalizationManager.Instance.GetString(LOC.CommonLauncherSettings)} - {Game.Name}";
        window.Content = new LegendaryGameSettingsView();
        window.Owner = PlayniteApi.GetLastActiveWindow();
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.MinWidth = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog();
    }

    public async Task OpenCheckForGamesUpdatesWindow()
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var legendaryUpdateController = new LegendaryUpdateController();
        var gamesToUpdate = new Dictionary<string, UpdateInfo>();
        var updateCheckProgressOptions =
            new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false)
                { IsIndeterminate = true };
        await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(updateCheckProgressOptions,
            async a => { gamesToUpdate = await legendaryUpdateController.CheckGameUpdates(Game.Name, Game.LibraryGameId); });

        var checkedGames = new List<Game>
        {
            Game
        };

        var window = PlayniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false
        });
        window.DataContext = gamesToUpdate;
        window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
        window.Content = new LegendaryUpdater(checkedGames);
        window.Owner = PlayniteApi.GetLastActiveWindow();
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.MinWidth = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog();
    }

    public async Task OpenImportGameWindow()
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var folders = await PlayniteApi.Dialogs.SelectFolderAsync();
        if (folders?.FirstOrDefault() != "")
        {
            var path = folders?.FirstOrDefault();
            var canContinue = await LegendaryLibrary.Instance.StopDownloadManager(true);
            if (!canContinue)
            {
                return;
            }

            await LegendaryDownloadLogic.WaitUntilLegendaryCloses();
            var importProgressOptions =
                new GlobalProgressOptions(
                        LocalizationManager.Instance.GetString(LOC.CommonImportingGame,
                            new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)Game.Name }), false)
                    { IsIndeterminate = true };
            await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(importProgressOptions,
                async a =>
                {
                    var importCmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                             .WithArguments(["-y", "import", Game.LibraryGameId, path]!)
                                             .WithEnvironmentVariables(
                                                  (await LegendaryLauncher
                                                     .GetDefaultEnvironmentVariables())!)
                                             .AddCommandToLog()
                                             .WithValidation(CommandResultValidation.None)
                                             .ExecuteBufferedAsync();
                    Logger.Debug($"[Legendary Cli] {importCmd.StandardError}");
                    if (importCmd.StandardError.Contains("has been imported"))
                    {
                        var installedAppList = LegendaryLauncher.GetInstalledAppList();
                        if (installedAppList.TryGetValue(Game.LibraryGameId, out var installedGameInfo))
                        {
                            Game.InstallDirectory = installedGameInfo.Install_path;
                            //game.Version = installedGameInfo.Version;
                            Game.InstallSize = (ulong)installedGameInfo.Install_size;
                            Game.InstallState = InstallState.Installed;
                        }

                        await PlayniteApi.Dialogs.ShowMessageAsync(
                            LocalizationManager.Instance.GetString(LOC.CommonImportFinished));
                    }
                    else
                    {
                        await PlayniteApi.Dialogs.ShowErrorMessageAsync(LocalizationManager.Instance.GetString(
                            LOC.LegendaryGameImportFailure,
                            new Dictionary<string, IFluentType>
                            {
                                ["reason"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog)
                            }));
                    }
                });
        }
    }

    public void OpenDlcManagerWindow()
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var window = PlayniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false,
        });
        window.Title = $"{LocalizationManager.Instance.GetString(LOC.CommonManageDlcs)} - {Game.Name}";
        window.DataContext = Game;
        window.Content = new LegendaryDlcManager();
        window.Owner = PlayniteApi.GetLastActiveWindow();
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.MinWidth = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog();
    }

    public async Task OpenMoveGameWindow()
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var newPaths = await PlayniteApi.Dialogs.SelectFolderAsync();
        if (newPaths?.FirstOrDefault() != "")
        {
            var newPath = newPaths?.FirstOrDefault();
            var oldPath = Game.InstallDirectory;
            if (Directory.Exists(oldPath) && Directory.Exists(newPath))
            {
                string sepChar = Path.DirectorySeparatorChar.ToString();
                string altChar = Path.AltDirectorySeparatorChar.ToString();
                if (!oldPath.EndsWith(sepChar) && !oldPath.EndsWith(altChar))
                {
                    oldPath += sepChar;
                }

                var folderName = Path.GetFileName(Path.GetDirectoryName(oldPath));
                newPath = Path.Combine(newPath, folderName);
                var moveFluentArgs = new Dictionary<string, IFluentType>
                {
                    ["appName"] = (FluentString)Game.Name,
                    ["path"] = (FluentString)newPath
                };
                var moveConfirm = await PlayniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonMoveConfirm,
                        moveFluentArgs), LocalizationManager.Instance.GetString(LOC.CommonMove),
                    MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                if (moveConfirm == MessageBoxResult.Yes)
                {
                    var globalProgressOptions =
                        new GlobalProgressOptions(
                            LocalizationManager.Instance.GetString(LOC.CommonMovingGame, moveFluentArgs), false);
                    await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async a =>
                    {
                        a.SetProgressMaxValue(3);
                        a.SetCrrentProgressValue(0);
                        _ = (Application.Current.Dispatcher?.BeginInvoke((Action)async delegate
                        {
                            try
                            {
                                bool canContinue = await LegendaryLibrary.Instance.StopDownloadManager(true);
                                if (!canContinue)
                                {
                                    return;
                                }

                                await LegendaryDownloadLogic.WaitUntilLegendaryCloses();
                                Directory.Move(oldPath, newPath);
                                a.SetCrrentProgressValue(1);
                                var rewriteResult = await Cli
                                                         .Wrap(LegendaryLauncher.ClientExecPath)
                                                         .WithArguments(["move", Game.LibraryGameId, newPath, "--skip-move"])
                                                         .WithEnvironmentVariables(await LegendaryLauncher
                                                             .GetDefaultEnvironmentVariables())
                                                         .AddCommandToLog()
                                                         .ExecuteBufferedAsync();
                                var errorMessage = rewriteResult.StandardError;
                                if (rewriteResult.ExitCode != 0 ||
                                    errorMessage.Contains("ERROR") ||
                                    errorMessage.Contains("CRITICAL") ||
                                    errorMessage.Contains("Error"))
                                {
                                    Logger.Error($"[Legendary Cli] {errorMessage}");
                                    Logger.Error(
                                        $"[Legendary Cli] exit code: {rewriteResult.ExitCode}");
                                }

                                a.SetCrrentProgressValue(2);
                                Game.InstallDirectory = newPath;
                                await PlayniteApi.Library.Games.UpdateAsync(Game);
                                a.SetCrrentProgressValue(3);
                                await PlayniteApi.Dialogs.ShowMessageAsync(
                                    LocalizationManager.Instance.GetString(
                                        LOC.CommonMoveGameSuccess, moveFluentArgs));
                            }
                            catch (Exception e)
                            {
                                a.SetCrrentProgressValue(3);
                                await PlayniteApi.Dialogs.ShowErrorMessageAsync(
                                    LocalizationManager.Instance.GetString(
                                        LOC.CommonMoveGameError, moveFluentArgs));
                                Logger.Error(e.Message);
                            }
                        }));
                    });
                }
            }
        }
    }
    
}