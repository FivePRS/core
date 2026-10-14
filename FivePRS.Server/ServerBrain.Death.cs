using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const float ReviveRange = 6f;
        private const string PersonDown = "Person down, unconscious and unresponsive.";

        private readonly HashSet<int> _downed = new();

        private void RegisterDeathEvents()
        {
            EventHandlers[EventNames.ServerPlayerDowned]    += new Action<Player>(OnPlayerDowned);
            EventHandlers[EventNames.ServerPlayerRespawned] += new Action<Player>(OnPlayerRespawned);
            EventHandlers[EventNames.ServerRevivePlayer]    += new Action<Player, int>(OnRevivePlayer);
            RegisterAdminCommand("fprs_revive", "<player id>", 1, AdminReviveAsync);
        }

        private void OnPlayerDowned([FromSource] Player player)
        {
            if (!ConfigManager.Settings.Death.Enabled || !Allow(player, "requests", l => l.Requests)) return;

            var id = ServerId(player);
            if (!_downed.Add(id)) return;

            TriggerClientEvent(EventNames.ClientPlayerDowned, id, true);
            if (ConfigManager.Settings.Death.NotifyEms) _ = AlertEmsAsync(player);
        }

        private void OnPlayerRespawned([FromSource] Player player)
        {
            if (!Allow(player, "requests", l => l.Requests)) return;
            ClearDowned(ServerId(player));
        }

        private async void OnRevivePlayer([FromSource] Player medic, int targetId)
        {
            if (!Allow(medic, "dispatch", l => l.Dispatch)) return;
            if (!_downed.Contains(targetId) || !TryGetPlayer(targetId, out var target)) return;

            var isMedic = TryGetCached(medic, out var license, out var data) && data.IsOnDuty && data.Department == Department.EMS;
            if (!isMedic && !_permissions.IsAdmin(medic.Handle))
            {
                Notify(medic, "~r~Only on-duty EMS can revive people.");
                return;
            }

            var distance = Vector3.Distance(API.GetEntityCoords(API.GetPlayerPed(medic.Handle)), API.GetEntityCoords(API.GetPlayerPed(target.Handle)));
            if (distance > ReviveRange)
            {
                Notify(medic, "~r~Get closer to the patient first.");
                return;
            }

            Revive(target, medic.Name);
            Notify(medic, $"~g~{target.Name} revived.");
            Audit(AuditActions.PlayerRevived, medic, GetLicense(target), $"revived {target.Name}");

            var xp = ConfigManager.Settings.Death.ReviveXp;
            if (isMedic && xp > 0)
            {
                try
                {
                    await GrantXpAsync(medic, data, xp);
                    Notify(medic, $"~y~+{xp} XP");
                }
                catch (Exception ex)
                {
                    Log.Error($"Revive XP failed for {medic.Name}", ex);
                }
            }
        }

        private Task<CommandResult> AdminReviveAsync(Player? caller, string[] args)
        {
            if (!int.TryParse(args[0], out var id) || !TryGetPlayer(id, out var target)) return Task.FromResult(TargetNotFound(args[0]));
            if (!_downed.Contains(id)) return Task.FromResult(CommandResult.Failed($"{target.Name} is not down."));

            Revive(target, "an administrator");
            Audit(AuditActions.PlayerRevived, caller, GetLicense(target), $"admin revive {target.Name}");
            return Task.FromResult(CommandResult.Done($"{target.Name} revived."));
        }

        private void Revive(Player target, string by)
        {
            ClearDowned(ServerId(target));
            TriggerClientEvent(target, EventNames.ClientRevived, by);
        }

        private void ClearDowned(int id)
        {
            if (_downed.Remove(id))
                TriggerClientEvent(EventNames.ClientPlayerDowned, id, false);
        }

        private void SendDownedPlayers(Player player) =>
            TriggerClientEvent(player, EventNames.ClientDownedPlayers, JsonConvert.SerializeObject(_downed.ToList()));

        private async Task AlertEmsAsync(Player player)
        {
            var license = GetLicense(player);
            var name    = player.Name;
            if (license is not null && _civilians is not null)
            {
                try
                {
                    name = (await _civilians.GetActiveCharacterAsync(license))?.FullName ?? name;
                }
                catch (Exception ex)
                {
                    Log.Error($"Looking up {player.Name}'s character failed", ex);
                }
            }

            var position = API.GetEntityCoords(API.GetPlayerPed(player.Handle));
            var (callId, error) = _dispatch.CreateEmergencyCall(ServerId(player), name, Department.EMS, PersonDown, position.X, position.Y, position.Z);
            if (error is not null) return;

            foreach (var unitId in _dispatch.UnitsInDepartment(Department.EMS))
            {
                if (TryGetPlayer(unitId, out var unit))
                    Notify(unit, $"~r~Person down~w~ | Call #{callId}: {name} is unconscious.");
            }
        }
    }
}
