using Playnite.Common;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

#if Vanara || PlayniteDeps
using Vanara.PInvoke;
using static Vanara.PInvoke.Kernel32;
#endif

namespace Playnite;

public static class CmdLineTools
{
    public const string TaskKill = "taskkill";
    public const string Cmd = "cmd";
    public const string IPConfig = "ipconfig";
}

// UseShellExecute set excplicitly because it used to be default on Framework but no longer is on Core.
// To preserve the same behavior as in P10. Also it's way more lenient to running things that are not actuall exes.
public static partial class ProcessStarter
{
    private static readonly ILogger logger = LogManager.GetLogger();

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

#if PlayniteDeps
        if (url.StartsWith("{DocsRootUrl}", StringComparison.OrdinalIgnoreCase))
            url = AppConfig.Config.DocsRootUrl.UriCombine(url.Replace("{DocsRootUrl}", "", StringComparison.OrdinalIgnoreCase));

        url = url.Replace("{AppBranch}", AppConfig.AppBranch, StringComparison.OrdinalIgnoreCase);
#endif

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

        logger.Debug($"Opening URL: {url}");
        try
        {
            return Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception e)
        {
            // There are some crash report with 0x80004005 error when opening standard URL.
            logger.Error(e, "Failed to open URL.");
            return Process.Start(CmdLineTools.Cmd, $"/C start {url}");
        }
    }

    public static Process? StartProcess(string path, string? arguments = null, string? workDir = null, bool asAdmin = false, bool noWindow = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        logger.Debug($"Starting process: {path}, {arguments}, {workDir}, {asAdmin}");

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

        return Process.Start(info);
    }

    public static int StartProcessWait(string path, string? arguments = null, string? workDir = null, bool asAdmin = false, bool noWindow = false)
    {
        using var proc = StartProcess(path, arguments, workDir, asAdmin, noWindow) ?? throw new Exception("Failed to start process, no process was started.");
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
        logger.Debug($"Starting process: {path}, {arguments}, {workDir}");
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

#if Vanara || PlayniteDeps
    public static uint ShellExecute(string cmdLine)
    {
        logger.Debug($"Executing shell command: {cmdLine}");

        if (CreateProcess(
                null,
                new StringBuilder(cmdLine),
                default,
                default,
                false,
                CREATE_PROCESS.NORMAL_PRIORITY_CLASS,
                default,
                null,
                STARTUPINFO.Default,
                out var procInfo))
        {
            using (procInfo)
                return procInfo.dwProcessId;
        }
        else
        {
            Win32Error.GetLastError().ThrowIfFailed();
            return 0;
        }
    }
#endif
}
