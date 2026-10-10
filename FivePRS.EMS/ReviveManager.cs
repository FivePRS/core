using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Agency;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;

namespace FivePRS.EMS
{
    public class ReviveManager : BaseScript
    {
        private const float ReviveRange = 2.5f;
        private const int ReviveMs = 6_000;
        private const int ScanIntervalMs = 200;

        private static int? _target;
        private static bool _reviving;

        public ReviveManager()
        {
            Interactions.Register(TryRevive);
            Tick += PromptTick;
        }

        private static bool CanRevive =>
            ConfigManager.Settings.Death.Enabled && BaseAgency.Active?.Department == Department.EMS && !Game.PlayerPed.IsInVehicle() && !_reviving;

        private async Task PromptTick()
        {
            await Delay(ScanIntervalMs);

            _target = CanRevive ? FindDowned() : null;
            if (_target is not null)
                ClientBrain.ShowHelp($"{Binds.Interact} Revive {API.GetPlayerName(API.GetPlayerFromServerId(_target.Value))}", ScanIntervalMs + 100);
        }

        private static int? FindDowned()
        {
            var own = API.GetPlayerServerId(API.PlayerId());
            foreach (var serverId in DeathManager.DownedPlayers.Where(id => id != own))
            {
                var position = PositionOf(serverId);
                if (position is not null && Vector3.Distance(Game.PlayerPed.Position, position.Value) <= ReviveRange)
                    return serverId;
            }

            return null;
        }

        private static Vector3? PositionOf(int serverId)
        {
            var player = API.GetPlayerFromServerId(serverId);
            if (player < 0) return null;

            var ped = API.GetPlayerPed(player);
            return ped == 0 ? null : API.GetEntityCoords(ped, true);
        }

        private static bool TryRevive()
        {
            if (_target is null || !CanRevive) return false;

            var target = _target.Value;
            _ = ReviveAsync(target);
            return true;
        }

        private static async Task ReviveAsync(int serverId)
        {
            _reviving = true;
            try
            {
                if (await Treatment.RunAsync(() => DeathManager.IsDowned(serverId) ? PositionOf(serverId) : null, ReviveMs, "Reviving"))
                    ClientEvents.TriggerServer(EventNames.ServerRevivePlayer, serverId);
            }
            finally
            {
                _reviving = false;
            }
        }
    }
}
