using Playnite.Common;
using Playnite.SDK;
using System.Diagnostics;
using System.Linq;

namespace LegendaryLibraryNS
{
    public class LegendaryClient : LibraryClient
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        public override string Icon => LegendaryLauncher.Icon;

        public override bool IsInstalled => LegendaryLauncher.IsInstalled;

        public override void Open()
        {
            LegendaryLauncher.StartClient();
        }

        public override void Shutdown()
        {
            var mainProc = Process.GetProcessesByName("Legendary").FirstOrDefault();
            if (mainProc == null)
            {
                logger.Info("Legendary is no longer running, no need to shut it down.");
                return;
            }

            var procRes = ProcessStarter.StartProcessWait(CmdLineTools.TaskKill, $"/f /pid {mainProc.Id}", null, out var stdOut, out var stdErr);
            if (procRes != 0)
            {
                logger.Error($"Failed to close Legendary: {procRes}, {stdErr}");
            }
        }
    }
}
