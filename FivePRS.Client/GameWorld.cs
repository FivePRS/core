using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;

namespace FivePRS.Client
{
    public class GameWorld : BaseScript
    {
        private const int DefaultMaxWantedLevel = 5;
        private const int DispatchServiceCount = 15;
        private const int CheckIntervalMs = 1_000;

        public GameWorld()
        {
            EventHandlers["onClientResourceStop"] += new Action<string>(name =>
            {
                if (name == API.GetCurrentResourceName()) Restore();
            });

            Tick += OnTickAsync;
        }

        private static WorldSettings Settings => ConfigManager.Settings.World;

        private async Task OnTickAsync()
        {
            var settings = Settings;
            var player   = API.PlayerId();

            if (settings.DisableWantedLevel)
            {
                API.SetMaxWantedLevel(0);
                if (API.GetPlayerWantedLevel(player) > 0)
                {
                    API.ClearPlayerWantedLevel(player);
                    API.SetPlayerWantedLevelNow(player, false);
                }
            }

            if (settings.DisableDispatchServices)
            {
                for (var service = 1; service <= DispatchServiceCount; service++)
                    API.EnableDispatchService(service, false);
            }

            if (settings.DisableRandomCops)
            {
                API.SetCreateRandomCops(false);
                API.SetCreateRandomCopsNotOnScenarios(false);
                API.SetCreateRandomCopsOnScenarios(false);
            }

            await Delay(CheckIntervalMs);
        }

        private static void Restore()
        {
            var settings = Settings;

            if (settings.DisableWantedLevel) API.SetMaxWantedLevel(DefaultMaxWantedLevel);

            if (settings.DisableDispatchServices)
            {
                for (var service = 1; service <= DispatchServiceCount; service++)
                    API.EnableDispatchService(service, true);
            }

            if (settings.DisableRandomCops)
            {
                API.SetCreateRandomCops(true);
                API.SetCreateRandomCopsNotOnScenarios(true);
                API.SetCreateRandomCopsOnScenarios(true);
            }
        }
    }
}
