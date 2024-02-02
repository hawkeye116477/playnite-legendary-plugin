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
    }
}
