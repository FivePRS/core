using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Stations;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using Newtonsoft.Json;

namespace FivePRS.Client
{
    public class DeathManager : BaseScript
    {
        private const string DownedDict = "dead";
        private const string DownedClip = "dead_a";
        private const int SettleMs = 1_000;
        private const int SpawnGraceMs = 15_000;
        private const int RespawnControl = 38;
        private const int MaxHealth = 200;
        private const int DownedHealth = 150;

        private static readonly int[] AllowedControls = { 1, 2, 245, 249, 199, 200 };
        private static readonly HashSet<int> Downed = new();

        private readonly int _startedAt = API.GetGameTimer();
        private bool _spawned;
        private bool _down;
        private int _downAt;

        public DeathManager()
        {
            EventHandlers["playerSpawned"] += new Action(() => _spawned = true);
            EventHandlers[EventNames.ClientPlayerDowned] += new Action<int, bool>(OnPlayerDowned);
            EventHandlers[EventNames.ClientDownedPlayers] += new Action<string>(OnDownedPlayers);
            EventHandlers[EventNames.ClientRevived] += new Action<string>(OnRevived);
            EventHandlers["onClientResourceStop"] += new Action<string>(name =>
            {
                if (name == API.GetCurrentResourceName() && _down) GetUp();
            });

            Tick += WatchTick;
        }

        public static bool IsDowned(int serverId) => Downed.Contains(serverId);

        public static IEnumerable<int> DownedPlayers => Downed;

        private static DeathSettings Settings => ConfigManager.Settings.Death;

        private async Task WatchTick()
        {
            await Delay(250);
            if (!Settings.Enabled || _down) return;

            var ready = _spawned || (API.GetGameTimer() - _startedAt > SpawnGraceMs && API.IsScreenFadedIn());
            if (ready && Game.PlayerPed.IsDead)
                await GoDownAsync();
        }

        private async Task GoDownAsync()
        {
            _down = true;
            await Delay(SettleMs);

            var ped = Game.PlayerPed;
            var position = ped.Position;
            API.NetworkResurrectLocalPlayer(position.X, position.Y, position.Z, ped.Heading, true, false);
            await Delay(0);

            ped = Game.PlayerPed;
            ped.Health = DownedHealth;
            ped.IsInvincible = true;
            await PlayDownedAsync(ped);

            _downAt = API.GetGameTimer();
            ClientEvents.TriggerServer(EventNames.ServerPlayerDowned);
            Tick += DownedTick;
        }

        private async Task DownedTick()
        {
            API.DisableAllControlActions(0);
            foreach (var control in AllowedControls)
                API.EnableControlAction(0, control, true);

            var ped = Game.PlayerPed;
            if (!API.IsEntityPlayingAnim(ped.Handle, DownedDict, DownedClip, 3))
                await PlayDownedAsync(ped);

            var remaining = Settings.RespawnSeconds - (API.GetGameTimer() - _downAt) / 1000;
            if (remaining > 0)
            {
                ClientBrain.ShowHelp($"You're unconscious. EMS has been notified.~n~You can respawn in {remaining / 60}:{remaining % 60:00}", 0);
                return;
            }

            ClientBrain.ShowHelp("You're unconscious. EMS has been notified.~n~Press ~INPUT_CONTEXT~ to respawn at the hospital", 0);
            if (API.IsDisabledControlJustPressed(0, RespawnControl))
                await RespawnAsync();
        }

        private async Task RespawnAsync()
        {
            var hospital = Hospitals.Nearest(Game.PlayerPed.Position);
            GetUp();
            ClientEvents.TriggerServer(EventNames.ServerPlayerRespawned);

            if (hospital is not null)
                await Teleporter.ToAsync(StationService.PositionOf(hospital), hospital.Heading);

            ClientBrain.ShowNotification("You were treated at the hospital.", "~b~EMS");
        }

        private void OnRevived(string by)
        {
            if (!_down) return;
            GetUp();
            ClientBrain.ShowNotification($"You were revived by {by}.", "~b~EMS");
        }

        private void GetUp()
        {
            Tick -= DownedTick;
            _down = false;

            var ped = Game.PlayerPed;
            ped.Task.ClearAll();
            ped.IsInvincible = false;
            ped.Health = MaxHealth;
        }

        private static void OnPlayerDowned(int serverId, bool downed)
        {
            if (downed) Downed.Add(serverId);
            else Downed.Remove(serverId);
        }

        private static void OnDownedPlayers(string json)
        {
            Downed.Clear();
            foreach (var id in JsonConvert.DeserializeObject<List<int>>(json) ?? new List<int>())
                Downed.Add(id);
        }

        private static async Task PlayDownedAsync(Ped ped)
        {
            API.RequestAnimDict(DownedDict);
            for (var i = 0; i < 100 && !API.HasAnimDictLoaded(DownedDict); i++)
                await Delay(50);

            await ped.Task.PlayAnimation(DownedDict, DownedClip, 8f, -8f, -1, AnimationFlags.Loop, 0f);
        }
    }
}
