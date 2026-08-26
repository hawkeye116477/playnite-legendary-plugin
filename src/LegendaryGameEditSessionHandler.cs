using CommonPlugin;
using Playnite;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace LegendaryLibraryNS
{
    public class LegendaryGameEditSessionHandler(Game game) : GameEditSessionHandler
    {
        private LegendaryGameSettingsViewModel? gameSettingsViewModel;

        public override async Task<List<GameEditSessionSection>> GetEditSectionsAsync(GetEditSectionsAsyncArgs args)
        {
            gameSettingsViewModel = new LegendaryGameSettingsViewModel(game);
            var gameSettingsView = new LegendaryGameSettingsView
            {
                DataContext = gameSettingsViewModel
            };
            return
            [
                new GameEditSessionSection(LocalizationManager.Instance.GetString(LOC.ThirdPartyPlayniteGameLaunching), gameSettingsView)
            ];
        }

        public override async Task EndEditAsync(EndEditArgs args)
        {
            gameSettingsViewModel?.Save();
        }

        public override bool GetHasUnsavedChanges(GetHasUnsavedChangesArgs args)
        {
            if (gameSettingsViewModel == null)
            {
                return false;
            }

            var oldGameSettings = LegendaryGameSettingsViewModel.LoadGameSettings(game.LibraryGameId!);
            var newGameSettings = gameSettingsViewModel.PrepareNewGameSettings();
            return Serialization.ToJson(newGameSettings) != Serialization.ToJson(oldGameSettings);
        }
    }
}