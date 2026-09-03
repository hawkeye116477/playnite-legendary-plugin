using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using ByteAether.Ulid;
using Microsoft.Win32;
using Playnite;

namespace PlayniteMod;

public class Program
{
    public string? Path { get; set; }
    public string? Arguments { get; set; }
    public string? Icon { get; set; }
    public int IconIndex { get; set; }
    public string? WorkDir { get; set; }
    public string? Name { get; set; }
    public string? AppId { get; set; }
    public string OriginalParsedPath { get; }

    public Program(string parsedPath)
    {
        OriginalParsedPath = parsedPath;
    }

    public override string? ToString()
    {
        return Name ?? base.ToString();
    }
}

public class UninstallProgram
{
    public string? DisplayIcon { get; set; }
    public string? DisplayName { get; set; }
    public string? DisplayVersion { get; set; }
    public string? InstallLocation { get; set; }
    public string? Publisher { get; set; }
    public string? UninstallString { get; set; }
    public string? URLInfoAbout { get; set; }
    public string? RegistryKeyName { get; set; }
    public string? Path { get; set; }

    public override string? ToString()
    {
        return DisplayName ?? RegistryKeyName ?? base.ToString();
    }
}

public static class Programs
{
    private static readonly string[] scanFileExclusionMasks =
    [
        "uninst",
        "setup",
        @"unins\d+",
        "Config",
        "DXSETUP",
        @"vc_redist\.x64",
        @"vc_redist\.x86",
        @"^UnityCrashHandler32\.exe$",
        @"^UnityCrashHandler64\.exe$",
        @"^notification_helper\.exe$",
        @"^python\.exe$",
        @"^pythonw\.exe$",
        @"^zsync\.exe$",
        @"^zsyncmake\.exe$"
    ];

    private static readonly string[] shortcutsFolderExceptions =
    [
        @"\Accessibility\",
        @"\Accessories\",
        @"\Administrative Tools\",
        @"\Maintenance\",
        @"\StartUp\",
        @"\Windows ",
        @"\Microsoft ",
    ];

    private static readonly string[] shortcutsPathExceptions =
    [
        @"\system32\",
        @"\windows\",
    ];

    private static readonly ILogger Logger = LogManager.GetLogger(typeof(Programs));

    public static readonly string[] ImportableFileExtensions = [".exe", ".bat", ".lnk", ".url"];
    public static readonly string[] ImportableFileExtensionsPattern = ["*.exe", "*.bat", "*.lnk", "*.url"];

    public static bool IsFileScanExcluded(string path)
    {
        return scanFileExclusionMasks.Any(a => Regex.IsMatch(path, a, RegexOptions.IgnoreCase));
    }

    public static void CreateUrlShortcut(string url, string iconPath, string shortcutPath)
    {
        FileSystem.PrepareSaveFile(shortcutPath);
        var content = """
                      [InternetShortcut]
                      IconIndex=0
                      """;
        if (!iconPath.IsNullOrEmpty())
        {
            content += Environment.NewLine + $"IconFile={iconPath}";
        }

        content += Environment.NewLine + $"URL={url}";
        File.WriteAllText(shortcutPath, content);
    }

    private static List<UninstallProgram> GetUninstallProgsFromView(RegistryView view)
    {
        var rootString = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\";

        void SearchRoot(RegistryHive hive, List<UninstallProgram> programs)
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            var keyList = root.OpenSubKey(rootString);
            if (keyList is null)
            {
                return;
            }

            foreach (var key in keyList.GetSubKeyNames())
            {
                try
                {
                    using var prog = root.OpenSubKey(rootString + key);
                    if (prog is null)
                    {
                        continue;
                    }

                    var program = new UninstallProgram
                    {
                        DisplayIcon = prog.GetValue("DisplayIcon")?.ToString(),
                        DisplayVersion = prog.GetValue("DisplayVersion")?.ToString(),
                        DisplayName = prog.GetValue("DisplayName")?.ToString(),
                        InstallLocation = prog.GetValue("InstallLocation")?.ToString(),
                        Publisher = prog.GetValue("Publisher")?.ToString(),
                        UninstallString = prog.GetValue("UninstallString")?.ToString(),
                        URLInfoAbout = prog.GetValue("URLInfoAbout")?.ToString(),
                        Path = prog.GetValue("Path")?.ToString(),
                        RegistryKeyName = key
                    };

                    programs.Add(program);
                }
                catch (SecurityException e)
                {
                    Logger.Warn(e, $"Failed to read registry key {rootString + key}");
                }
            }
        }

        var progs = new List<UninstallProgram>();
        SearchRoot(RegistryHive.LocalMachine, progs);
        SearchRoot(RegistryHive.CurrentUser, progs);
        return progs;
    }

    public static List<UninstallProgram> GetUnistallProgramsList()
    {
        var progs = new List<UninstallProgram>();
        progs.AddRange(GetUninstallProgsFromView(RegistryView.Registry64));
        progs.AddRange(GetUninstallProgsFromView(RegistryView.Registry32));
        return progs;
    }

    public static ImportableGame? ProgramToGame(this Program program)
    {
        if (program.Path.IsNullOrWhiteSpace())
            return null;

        var game = new ImportableGame(
            program.Name ?? "uknown",
            "Playnite",
            program.AppId ?? Ulid.New().ToString())
        {
            InstallState = InstallState.Installed,
            InstallDirectory = program.WorkDir.IsNullOrWhiteSpace() ? Path.GetDirectoryName(program.OriginalParsedPath) : program.WorkDir
        };

        if (!program.Icon.IsNullOrWhiteSpace())
            game.MediaFiles = [new ImportableFile(BuiltInGameDataId.DesktopIcon, program.Icon)];

        if (program.OriginalParsedPath.EndsWith(".url", StringComparison.OrdinalIgnoreCase))
        {
            game.Actions =
            [
                new UrlGameAction
                {
                    Url = program.Path,
                    IsPlayAction = true,
                    Name = game.Name
                }
            ];
        }
        else
        {
            var action = new FileGameAction
            {
                Path = program.Path.Replace(
                    game.InstallDirectory?.EndWithDirSeparator() ?? "",
                    ExpandableVariables.InstallationDirectory.EndWithDirSeparator(),
                    StringComparison.OrdinalIgnoreCase),
                WorkingDir = ExpandableVariables.InstallationDirectory,
                Arguments = program.Arguments,
                IsPlayAction = true,
                Name = game.Name,
                TrackingOptions = new GameTrackingOptions
                {
                    Mode = TrackingMode.Directory,
                    TrackingValue = ExpandableVariables.InstallationDirectory
                }
            };

            if (action.Path.Equals("explorer.exe", StringComparison.OrdinalIgnoreCase) &&
                action.Arguments?.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase) == true)
            {
                action.TrackingOptions.Mode = TrackingMode.Directory;
                action.TrackingOptions.TrackingValue = "{InstallDir}";
            }

            game.Actions = [action];
        }

        return game;
    }
}