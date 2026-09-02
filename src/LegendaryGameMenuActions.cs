using System.IO;
using System.Windows;
using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using Linguini.Shared.Types.Bundle;
using Playnite;
using MessageBoxResult = Playnite.MessageBoxResult;

namespace LegendaryLibraryNS;

public static class LegendaryGameMenuActions
{
    private static readonly ILogger Logger = LogManager.GetLogger();
    private static IPlayniteApi PlayniteApi { get; set; } = LegendaryLibrary.PlayniteApi;

    public static async Task OpenCheckForGamesUpdatesWindow(Game game)
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            await LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var legendaryUpdateController = new LegendaryUpdateController();
        var gamesToUpdate = new Dictionary<string, UpdateInfo>();
        var updateCheckProgressOptions =
            new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonCheckingForUpdates), false)
                { IsIndeterminate = true };
        await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(updateCheckProgressOptions,
            async a => { gamesToUpdate = await legendaryUpdateController.CheckGameUpdates(game.Name, game.LibraryGameId!); });
        

        var window = PlayniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false
        });
        window.DataContext = gamesToUpdate;
        window.Title = $"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteExtensionsUpdates)}";
        window.Content = new LegendaryUpdater();
        window.Owner = PlayniteApi.GetLastActiveWindow();
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.MinWidth = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog();
    }

    public static async Task OpenImportGameWindow(Game game)
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            await LegendaryLauncher.ShowNotInstalledError();
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
                            new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)game.Name }), false)
                    { IsIndeterminate = true };
            await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(importProgressOptions,
                async a =>
                {
                    var importCmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                             .WithArguments(["-y", "import", game.LibraryGameId!, path!])
                                             .WithEnvironmentVariables(
                                                  LegendaryLauncher
                                                     .GetDefaultEnvironmentVariables())
                                             .AddCommandToLog()
                                             .WithValidation(CommandResultValidation.None)
                                             .ExecuteBufferedAsync();
                    Logger.Debug($"[Legendary Cli] {importCmd.StandardError}");
                    if (importCmd.StandardError.Contains("has been imported"))
                    {
                        var installedAppList = LegendaryLauncher.GetInstalledAppList();
                        if (installedAppList.TryGetValue(game.LibraryGameId!, out var installedGameInfo))
                        {
                            game.InstallDirectory = installedGameInfo.Install_path;
                            game.InstallSize = (ulong)installedGameInfo.Install_size;
                            game.InstallState = InstallState.Installed;
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

    public static async Task OpenDlcManagerWindow(Game game)
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            await LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var window = PlayniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false
        });
        window.Title = $"{LocalizationManager.Instance.GetString(LOC.CommonManageDlcs)} - {game.Name}";
        window.DataContext = game;
        window.Content = new LegendaryDlcManager();
        window.Owner = PlayniteApi.GetLastActiveWindow();
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.MinWidth = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog();
    }

    public static async Task OpenMoveGameWindow(Game game)
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            await LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var newPaths = await PlayniteApi.Dialogs.SelectFolderAsync();
        if (newPaths?.FirstOrDefault() != "")
        {
            var newPath = newPaths?.FirstOrDefault();
            var oldPath = game.InstallDirectory;
            if (Directory.Exists(oldPath) && Directory.Exists(newPath))
            {
                var sepChar = Path.DirectorySeparatorChar.ToString();
                var altChar = Path.AltDirectorySeparatorChar.ToString();
                if (!oldPath.EndsWith(sepChar) && !oldPath.EndsWith(altChar))
                {
                    oldPath += sepChar;
                }

                var folderName = Path.GetFileName(Path.GetDirectoryName(oldPath));
                newPath = Path.Combine(newPath, folderName!);
                var moveFluentArgs = new Dictionary<string, IFluentType>
                {
                    ["appName"] = (FluentString)game.Name,
                    ["path"] = (FluentString)newPath
                };
                var moveConfirm = await PlayniteApi.Dialogs.ShowMessageAsync(
                    LocalizationManager.Instance.GetString(LOC.CommonMoveConfirm,
                        moveFluentArgs), LocalizationManager.Instance.GetString(LOC.CommonMove),
                    MessageBoxButtons.YesNo, MessageBoxSeverity.Question);
                if (moveConfirm == MessageBoxResult.Yes)
                {
                    var globalProgressOptions =
                        new GlobalProgressOptions(LocalizationManager.Instance.GetString(LOC.CommonMovingGame, moveFluentArgs), false);
                    await PlayniteApi.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async a =>
                    {
                        a.SetProgressMaxValue(3);
                        a.SetCurrentProgressValue(0);
                        _ = Application.Current.Dispatcher?.BeginInvoke((Action)async delegate
                        {
                            try
                            {
                                var canContinue = await LegendaryLibrary.Instance.StopDownloadManager(true);
                                if (!canContinue)
                                {
                                    return;
                                }

                                await LegendaryDownloadLogic.WaitUntilLegendaryCloses();
                                Directory.Move(oldPath, newPath);
                                a.SetCurrentProgressValue(1);
                                var rewriteResult = await Cli
                                                         .Wrap(LegendaryLauncher.ClientExecPath)
                                                         .WithArguments(["move", game.LibraryGameId!, newPath, "--skip-move"])
                                                         .WithEnvironmentVariables(LegendaryLauncher
                                                             .GetDefaultEnvironmentVariables())
                                                         .AddCommandToLog()
                                                         .ExecuteBufferedAsync();
                                var errorMessage = rewriteResult.StandardError;
                                if (rewriteResult.ExitCode != 0
                                    || errorMessage.Contains("ERROR")
                                    || errorMessage.Contains("CRITICAL")
                                    || errorMessage.Contains("Error"))
                                {
                                    Logger.Error($"[Legendary Cli] {errorMessage}");
                                    Logger.Error(
                                        $"[Legendary Cli] exit code: {rewriteResult.ExitCode}");
                                }

                                a.SetCurrentProgressValue(2);
                                game.InstallDirectory = newPath;
                                await PlayniteApi.Library.Games.UpdateAsync(game);
                                a.SetCurrentProgressValue(3);
                                await PlayniteApi.Dialogs.ShowMessageAsync(
                                    LocalizationManager.Instance.GetString(
                                        LOC.CommonMoveGameSuccess, moveFluentArgs));
                            }
                            catch (Exception e)
                            {
                                a.SetCurrentProgressValue(3);
                                await PlayniteApi.Dialogs.ShowErrorMessageAsync(
                                    LocalizationManager.Instance.GetString(
                                        LOC.CommonMoveGameError, moveFluentArgs));
                                Logger.Error(e.Message);
                            }
                        });
                    });
                }
            }
        }
    }

    public static void OpenRepairWindow(List<Game> games)
    {
        var installData = new List<DownloadManagerData.Download>();
        foreach (var game in games)
        {
            var installProperties = new DownloadProperties
                { DownloadAction = DownloadAction.Repair };
            installData.Add(new DownloadManagerData.Download
            {
                GameId = game.LibraryGameId!,
                Name = game.Name,
                DownloadProperties = installProperties
            });
        }

        var window = PlayniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false
        });
        window.DataContext = installData;
        window.Content = new LegendaryGameInstaller();
        window.Owner = PlayniteApi.GetLastActiveWindow();
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.MinWidth = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var title = LocalizationManager.Instance.GetString(LOC.CommonRepair);
        if (games.Count == 1)
        {
            title = games[0].Name;
        }

        window.Title = title;
        window.ShowDialog();
    }

    public static void OpenInstallerWindow(List<Game> games)
    {
        var installData = new List<DownloadManagerData.Download>();
        foreach (var notInstalledLegendaryGame in games)
        {
            var installProperties = new DownloadProperties
                { DownloadAction = DownloadAction.Install };
            installData.Add(new DownloadManagerData.Download
            {
                GameId = notInstalledLegendaryGame.LibraryGameId ?? "",
                Name = notInstalledLegendaryGame.Name,
                DownloadProperties = installProperties
            });
        }

        LegendaryInstallController.LaunchInstaller(installData);
    }
}