using CommonPlugin;
using LegendaryLibraryNS.Models;
using Playnite.Common;
using Playnite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class EpicMetadataProvider : MetadataProvider
    {
        // ReSharper disable once AsyncMethodWithoutAwait
        public override async Task<MetadataProviderGameSession?> CreateGameSessionAsync(CreateGameMetadataSessionArgs args)
        {
            if (args.Game.LibraryId != LegendaryLibrary.PluginId)
                return null;

            return new EpicMetadataGameSession(args.Game);
        }
    }

    public class EpicMetadataGameSession(Game game) : MetadataProviderGameSession(game)
    {
        private ImportableFile? GetIconImage()
        {
            // There's not icon available on Epic servers so we will load one from EXE
            if (Game.InstallState == InstallState.Installed)
            {
                var installedAppList = LegendaryLauncher.GetInstalledAppList();
                if (Game.LibraryGameId != null && installedAppList.TryGetValue(Game.LibraryGameId, out var value))
                {
                    var exePath = Path.Combine(value.Install_path, value.Executable);
                    if (File.Exists(exePath))
                    {
                        return new ImportableFile(BuiltInGameDataId.DesktopIcon, exePath);
                    }
                }
            }
            return null;
        }
        
        public override async Task<object?> GetDataAsync(GetDataArgs dataArgs)
        {
            var gameFeatures = new List<NameImportableProperty>();
            var metadatafile = Path.Combine(LegendaryLauncher.ConfigPath, "metadata", Game.LibraryGameId + ".json");
            if (File.Exists(metadatafile))
            {
                if (Serialization.TryFromJson(FileSystem.ReadFileAsStringSafe(metadatafile), out LegendaryMetadata? legendaryMetadata))
                {
                    if (legendaryMetadata != null)
                    {
                        if (legendaryMetadata.metadata.customAttributes?.CloudSaveFolder != null)
                        {
                            gameFeatures.Add(new NameImportableProperty(LocalizationManager.Instance.GetString(LOC.CommonCloudSaves)));
                        }
                        if (legendaryMetadata.metadata.mainGameItem != null)
                        {
                            gameFeatures.Add(new NameImportableProperty(LocalizationManager.Instance.GetString(LOC.CommonExtraContent)));
                        }
                        if (legendaryMetadata.metadata.customAttributes?.CanRunOffline?.value == "true")
                        {
                            gameFeatures.Add(new NameImportableProperty(LocalizationManager.Instance.GetString(LOC.LegendaryOfflineMode)));
                        }
                    }
                }
            }

            
            return dataArgs.DataId switch
            {
                BuiltInGameDataId.Name => Game.Name,
                BuiltInGameDataId.DesktopIcon => GetIconImage(),
                BuiltInGameDataId.Features => gameFeatures,
                _ => null
            };
        }
    }
}
