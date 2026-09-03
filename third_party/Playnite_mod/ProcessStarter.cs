using System.Diagnostics;
using System.IO;
using Playnite;

namespace PlayniteMod;

public static class CmdLineTools
{
    public const string TaskKill = "taskkill";
    public const string Cmd = "cmd";
    public const string IPConfig = "ipconfig";
}

// UseShellExecute set excplicitly because it used to be default on Framework but no longer is on Core.
// To preserve the same behavior as in P10. Also it's way more lenient to running things that are not actuall exes.
public static class ProcessStarter
{
    private static readonly ILogger Logger = LogManager.GetLogger(typeof(ProcessStarter));

    public static Process? StartUrl(WebLink webLink)
    {
        return StartUrl(webLink.Url!);
    }

    public static Process? StartUrl(Uri uri)
    {
        return StartUrl(uri.OriginalString);
    }

    public static Process? StartUrl(string url)
    {
        ArgumentException.ThrowIfNullOrEmpty(url);
        if (!url.IsUri(UriKind.Absolute))
        {
            if (Paths.IsFullPath(url))
            {
                // Do nothing, some people put local file paths to link fields: #2562
            }
            else if (url.IsUri(UriKind.Relative))
                url = "https://" + url;
            else
                return null;
        }

        Logger.Debug($"Opening URL: {url}");
        try
        {
            return Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            // There are some crash report with 0x80004005 error when opening standard URL.
            Logger.Error(e, "Failed to open URL.");
            return Process.Start(CmdLineTools.Cmd, $"/C start {url}");
        }
    }

    public static Process? StartProcess(
        string path, string? arguments = null, string? workDir = null, bool asAdmin = false, bool noWindow = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var startupPath = path;
        if (path.Contains("..", StringComparison.Ordinal))
            startupPath = Path.GetFullPath(path);

        var info = new ProcessStartInfo(startupPath) { UseShellExecute = true };
        if (!arguments.IsNullOrWhiteSpace())
            info.Arguments = arguments;

        if (!workDir.IsNullOrWhiteSpace())
            info.WorkingDirectory = workDir;
        else
            info.WorkingDirectory = new FileInfo(startupPath).Directory?.FullName;

        if (noWindow)
        {
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
        }

        if (asAdmin)
            info.Verb = "runas";
        AddCommandToLog(info);

        return Process.Start(info);
    }

    public static int StartProcessWait(
        string path, string? arguments = null, string? workDir = null, bool asAdmin = false, bool noWindow = false)
    {
        using var proc = StartProcess(path, arguments, workDir, asAdmin, noWindow) ??
                         throw new Exception("Failed to start process, no process was started.");
        proc.WaitForExit();
        return proc.ExitCode;
    }

    public static int StartProcessWait(
        string path,
        string arguments,
        string workDir,
        out string stdOutput,
        out string stdError)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);

        var startupPath = path;
        if (path.Contains("..", StringComparison.Ordinal))
            startupPath = Path.GetFullPath(path);

        var info = new ProcessStartInfo(startupPath)
        {
            Arguments = arguments,
            WorkingDirectory = string.IsNullOrEmpty(workDir) ? new FileInfo(startupPath).Directory!.FullName : workDir,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        AddCommandToLog(info);

        var stdout = string.Empty;
        var stderr = string.Empty;
        using var proc = new Process();
        proc.StartInfo = info;
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stdout += e.Data + Environment.NewLine;
        };

        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
                stderr += e.Data + Environment.NewLine;
        };

        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        proc.WaitForExit();
        stdOutput = stdout;
        stdError = stderr;
        return proc.ExitCode;
    }

    private static void AddCommandToLog(ProcessStartInfo command, Dictionary<string, string>? environmentVariables = null)
    {
        var allEnvironmentVariables = "";
        var sensitiveValues = new HashSet<string> { "secret", "password", "token", "user" };

        if (environmentVariables?.Count > 0)
        {
            foreach (var env in environmentVariables)
            {
                if (sensitiveValues.Any(s => env.Key!.Contains(s, StringComparison.OrdinalIgnoreCase)))
                {
                    allEnvironmentVariables += $"{env.Key}=*** ";
                }
                else
                {
                    allEnvironmentVariables += $"{env.Key}={env.Value} ";
                }
            }
        }

        var tokens = (command.Arguments ?? "").Split(' ').ToList();
        if (tokens.Count == 0)
        {
            tokens = [.. command.ArgumentList];
        }

        for (var i = 0; i < tokens.Count - 1; i++)
        {
            var current = tokens[i];

            if ((current.StartsWith("--") || current.StartsWith('-'))
                && sensitiveValues.Any(s => current.Contains(s, StringComparison.OrdinalIgnoreCase)))
            {
                tokens[i + 1] = "***";
            }
        }

        var safeArguments = string.Join(" ", tokens);

        var debugLog = $"Executing command: {allEnvironmentVariables}{command.FileName} {safeArguments} .";
        if (!command.WorkingDirectory.IsNullOrEmpty())
        {
            debugLog += $"\nWorking directory: {command.WorkingDirectory}) .";
        }

        if (!command.Verb.IsNullOrEmpty())
        {
            debugLog += $"\nVerb: {command.Verb} .";
        }

        Logger.Debug(debugLog);
    }

    public static Process? StartProcess(ProcessStartInfo processStartInfo, Dictionary<string, string>? environmentVariables = null)
    {
        AddCommandToLog(processStartInfo, environmentVariables);
        if (environmentVariables is { Count: > 0 })
        {
            processStartInfo.UseShellExecute = false;
            processStartInfo.Environment!.AddRangeIfNotNull(environmentVariables);
        }

        if (processStartInfo.WorkingDirectory.IsNullOrEmpty())
        {
            processStartInfo.WorkingDirectory = new FileInfo(processStartInfo.FileName).Directory?.FullName;
        }

        return Process.Start(processStartInfo);
    }
}