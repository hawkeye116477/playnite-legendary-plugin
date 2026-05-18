using CommonPlugin;
using Playnite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class LegendaryGameEditSessionHandler(Game game) : GameEditSessionHandler
    {
        private LegendaryGameSettingsView? gameSettingsView;

        public override async Task<List<GameEditSessionSection>> GetEditSectionsAsync(GetEditSectionsAsyncArgs args)
        {
            gameSettingsView = new LegendaryGameSettingsView
            {
                DataContext = game
            };
            return
            [
                new GameEditSessionSection(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameLaunching), gameSettingsView)
            ];
        }

        public override async Task EndEditAsync(EndEditArgs args)
        {
            gameSettingsView?.Save();
            await Task.CompletedTask;
        }

        public override bool GetHasUnsavedChanges(GetHasUnsavedChangesArgs args)
        {
            if (gameSettingsView == null)
            {
                return false;
            }

            var oldGameSettings = LegendaryGameSettingsView.LoadGameSettings(game.LibraryGameId!);
            var newGameSettings = gameSettingsView.PrepareNewGameSettings();
            return Serialization.ToJson(newGameSettings) != Serialization.ToJson(oldGameSettings);
        }
    }
}