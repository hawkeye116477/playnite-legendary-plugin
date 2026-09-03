using System.IO;
using System.Windows;
using CliWrap;
using CliWrap.Buffered;
using CommonPlugin;
using CommonPlugin.Enums;
using LegendaryLibraryNS.Models;
using Linguini.Shared.Types.Bundle;
using Playnite;

namespace LegendaryLibraryNS;

public class LegendaryInstallController(Game game) : InstallController("legendary_install",
    "Install using Legendary client", game.LibraryGameId!)
{
    public override async Task InstallAsync(InstallActionArgs args)
    {
        var installProperties = new DownloadProperties { DownloadAction = DownloadAction.Install };
        var installData = new List<DownloadManagerData.Download>
        {
            new() { GameId = game.LibraryGameId!, Name = game.Name, DownloadProperties = installProperties }
        };

        LaunchInstaller(installData);
        await GameInstallationCancelledAsync(new GameInstallationCancelledArgs());
    }

    public static void LaunchInstaller(List<DownloadManagerData.Download> installData)
    {
        var playniteApi = LegendaryLibrary.PlayniteApi;
        var window = playniteApi.CreateWindow(new WindowCreationOptions
        {
            ShowMaximizeButton = false
        });
        window.DataContext = installData;
        window.Content = new LegendaryGameInstaller();
        window.Owner = playniteApi.GetLastActiveWindow();
        window.SizeToContent = SizeToContent.WidthAndHeight;
        window.MinWidth = 600;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var title = LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteInstallGame);
        if (installData.Count == 1)
        {
            title = installData[0].Name;
        }

        window.Title = title;
        window.ShowDialog();
    }
}

public class LegendaryUninstallController(Game game) : UninstallController("legendary_uninstall",
    "Uninstall using Legendary client", game.LibraryGameId!)
{
    private static readonly ILogger Logger = LogManager.GetLogger<LegendaryUninstallController>();

    public override async Task UninstallAsync(UninstallActionArgs args)
    {
        var games = new List<Game>
        {
            game
        };
        await LaunchUninstaller(games);
        await GameUninstallationCancelledAsync(new GameUninstallCancelledArgs());
    }

    public static async Task LaunchUninstaller(List<Game> games)
    {
        if (!LegendaryLauncher.IsInstalled)
        {
            await LegendaryLauncher.ShowNotInstalledError();
            return;
        }

        var playniteApi = LegendaryLibrary.PlayniteApi;
        var gamesCombined = string.Join(", ", games.Select(item => item.Name));

        var responses = new List<MessageBoxResponse>
        {
            new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteYesLabel)),
            new(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteNoLabel))
        };

        var removeGameLaunchSettingsCheckbox =
            new MessageBoxOption(LocalizationManager.Instance.GetString(LOC.CommonRemoveGameLaunchSettings), false);

        var result = await playniteApi.Dialogs.ShowMessageAsync(
            LocalizationManager.Instance.GetString(LOC.CommonUninstallGameConfirm,
                new Dictionary<string, IFluentType> { ["gameTitle"] = (FluentString)gamesCombined }),
            LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstallGame),
            MessageBoxSeverity.Question, responses, [removeGameLaunchSettingsCheckbox]);
        if (result?.Title == LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteYesLabel))
        {
            var canContinue = await LegendaryLibrary.Instance.StopDownloadManager(true);
            if (!canContinue)
            {
                return;
            }

            var uninstalledGames = new List<Game>();
            var notUninstalledGames = new List<Game>();
            var globalProgressOptions =
                new GlobalProgressOptions($"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstalling)}... ", false);
            await playniteApi.Dialogs.ShowAsyncBlockingProgressAsync(globalProgressOptions, async a =>
            {
                a.SetProgressMaxValue(games.Count);

                var counter = 0;
                foreach (var game in games)
                {
                    a.SetText($"{LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteUninstalling)} {game.Name}... ");
                    await LegendaryDownloadLogic.WaitUntilLegendaryCloses();
                    var cmd = await Cli.Wrap(LegendaryLauncher.ClientExecPath)
                                       .WithArguments(["-y", "uninstall", game.LibraryGameId!])
                                       .WithEnvironmentVariables(LegendaryLauncher.GetDefaultEnvironmentVariables())
                                       .AddCommandToLog()
                                       .WithValidation(CommandResultValidation.None)
                                       .ExecuteBufferedAsync();
                    if (cmd.StandardError.Contains("has been uninstalled"))
                    {
                        if (removeGameLaunchSettingsCheckbox.IsSelected)
                        {
                            var gameSettingsFile = Path.Combine(Path.Combine(playniteApi.UserDataDir, "GamesSettings",
                                $"{game.LibraryGameId}.json"));
                            if (File.Exists(gameSettingsFile))
                            {
                                File.Delete(gameSettingsFile);
                            }
                        }

                        try
                        {
                            if (Directory.Exists(game.InstallDirectory))
                            {
                                Directory.Delete(game.InstallDirectory, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Debug(ex.Message);
                        }

                        game.InstallState = InstallState.Uninstalled;
                        game.InstallDirectory = "";
                        //game.Version = "";
                        await playniteApi.Library.Games.UpdateAsync(game);
                        uninstalledGames.Add(game);
                    }
                    else
                    {
                        notUninstalledGames.Add(game);
                        Logger.Debug("[Legendary] " + cmd.StandardError);
                        Logger.Error("[Legendary] exit code: " + cmd.ExitCode);
                    }

                    counter += 1;
                    a.SetCurrentProgressValue(counter);
                }
            });
            if (uninstalledGames.Count > 0)
            {
                var uninstalledGamesList = uninstalledGames[0].Name;
                if (uninstalledGames.Count > 1)
                {
                    uninstalledGamesList = string.Join(", ", uninstalledGames.Select(item => item.Name));
                }

                await playniteApi.Dialogs.ShowMessageAsync(LocalizationManager.Instance.GetString(LOC.CommonUninstallSuccess,
                    new Dictionary<string, IFluentType>
                        { ["appName"] = (FluentString)uninstalledGamesList, ["count"] = (FluentNumber)uninstalledGames.Count }));
            }

            if (notUninstalledGames.Count > 0)
            {
                if (notUninstalledGames.Count == 1)
                {
                    await playniteApi.Dialogs.ShowErrorMessageAsync(
                        LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameUninstallError,
                            new Dictionary<string, IFluentType>
                                { ["var0"] = (FluentString)LocalizationManager.Instance.GetString(LOC.CommonCheckLog) }),
                        notUninstalledGames[0].Name);
                }
                else
                {
                    var notUninstalledGamesCombined = string.Join(", ", notUninstalledGames.Select(item => item.Name));
                    await playniteApi.Dialogs.ShowMessageAsync(
                        $"{LocalizationManager.Instance.GetString(LOC.CommonUninstallError, new Dictionary<string, IFluentType> { ["appName"] = (FluentString)notUninstalledGamesCombined, ["count"] = (FluentNumber)notUninstalledGames.Count })} {LocalizationManager.Instance.GetString(LOC.CommonCheckLog)}");
                }
            }
        }
    }
}