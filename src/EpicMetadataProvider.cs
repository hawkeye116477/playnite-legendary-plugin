using LegendaryLibraryNS.Models;
using LegendaryLibraryNS.Services;
using Playnite.Common;
using Playnite.SDK;
using Playnite.SDK.Data;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class EpicMetadataProvider : LibraryMetadataProvider
    {
        private readonly IPlayniteAPI api;

        public EpicMetadataProvider(IPlayniteAPI api)
        {
            this.api = api;
        }

        public override GameMetadata GetMetadata(Game game)
        {
            var gameInfo = new GameMetadata() { Links = new List<Link>() };
            var metadatafile = Path.Combine(LegendaryLauncher.ConfigPath, "metadata", game.GameId + ".json");
            if (File.Exists(metadatafile))
            {
                LegendaryMetadata.Rootobject legendaryMetadata = null;
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(metadatafile), out legendaryMetadata))
                {
                    if (legendaryMetadata != null)
                    {
                        gameInfo.Features = new HashSet<MetadataProperty>() { };
                        if (legendaryMetadata.metadata.customAttributes?.CloudSaveFolder != null)
                        {
                            gameInfo.Features.Add(new MetadataNameProperty(ResourceProvider.GetString(LOC.LegendaryCloudSaves)));
                        }
                        if (legendaryMetadata.metadata.mainGameItem != null)
                        {
                            gameInfo.Features.Add(new MetadataNameProperty(ResourceProvider.GetString(LOC.LegendaryExtraContent)));
                        }
                        if (legendaryMetadata.metadata.customAttributes?.CanRunOffline?.value == "true")
                        {
                            gameInfo.Features.Add(new MetadataNameProperty(ResourceProvider.GetString(LOC.LegendaryOfflineMode)));
                        }
                    }
                }
            }

            // There's not icon available on Epic servers so we will load one from EXE
            if (game.IsInstalled && string.IsNullOrEmpty(game.Icon))
            {
                var installedAppList = LegendaryLauncher.GetInstalledAppList();
                if (installedAppList.ContainsKey(game.GameId))
                {
                    var exePath = Path.Combine(installedAppList[game.GameId].Install_path, installedAppList[game.GameId].Executable);
                    if (File.Exists(exePath))
                    {
                        gameInfo.Icon = new MetadataFile(exePath);
                    }
                }
            }

            return gameInfo;
        }
    }
}
