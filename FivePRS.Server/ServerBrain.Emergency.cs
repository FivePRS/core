using System;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Civilian;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const string AnonymousCaller = "Anonymous";

        private void RegisterEmergencyEvents()
        {
            EventHandlers[EventNames.ServerEmergencyCall]   += new Action<Player, int, string, bool>(OnEmergencyCall);
            EventHandlers[EventNames.ServerEmergencyCancel] += new Action<Player, string>(OnEmergencyCancel);
            EventHandlers[EventNames.ServerEmergencyStatus] += new Action<Player>(OnEmergencyStatusRequest);
            EventHandlers[EventNames.ServerCallClear]       += new Action<Player, string>(OnCallClear);
        }

        private async void OnEmergencyCall([FromSource] Player player, int departmentId, string description, bool anonymous)
        {
            if (!Allow(player, "emergency", l => l.Emergency))
            {
                TriggerClientEvent(player, EventNames.ClientEmergencyError, "You're calling too often. Wait a moment and try again.");
                return;
            }

            var license = GetLicense(player);
            if (license is null) return;

            if (!Enum.IsDefined(typeof(Department), departmentId) || departmentId == (int)Department.None)
            {
                TriggerClientEvent(player, EventNames.ClientEmergencyError, "Choose which service you need.");
                return;
            }

            var text = (description ?? string.Empty).Trim();
            if (text.Length == 0 || text.Length > CivilianRules.MaxEmergencyLength)
            {
                TriggerClientEvent(player, EventNames.ClientEmergencyError,
                    $"Describe the emergency in up to {CivilianRules.MaxEmergencyLength} characters.");
                return;
            }

            try
            {
                var callerName = anonymous ? AnonymousCaller : player.Name;
                if (!anonymous && _civilians is not null)
                    callerName = (await _civilians.GetActiveCharacterAsync(license))?.FullName ?? player.Name;

                var position = API.GetEntityCoords(API.GetPlayerPed(player.Handle));
                var department = (Department)departmentId;
                var (callId, error) = _dispatch.CreateEmergencyCall(ServerId(player), callerName, department, text,
                    position.X, position.Y, position.Z);

                if (error is not null)
                {
                    TriggerClientEvent(player, EventNames.ClientEmergencyError, error);
                    return;
                }

                Audit(AuditActions.EmergencyCall, player, license, $"call #{callId} {department}");

                foreach (var unitId in _dispatch.UnitsInDepartment(department))
                {
                    if (TryGetPlayer(unitId, out var unit))
                        Notify(unit, $"~r~911 Call #{callId}~w~ | {callerName}: {text}");
                }

                SendEmergencyStatus(ServerId(player), $"~b~911~w~ | Your call #{callId} has been sent to {department}.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] 911 call failed for {player.Name}: {ex}");
                TriggerClientEvent(player, EventNames.ClientEmergencyError, "Something went wrong. Please try again.");
            }
        }

        private void OnEmergencyCancel([FromSource] Player player, string callId)
        {
            if (!Allow(player, "emergency", l => l.Emergency) || !IsCallId(callId)) return;

            if (_dispatch.CancelEmergencyCall(ServerId(player), callId))
                SendEmergencyStatus(ServerId(player), $"~b~911~w~ | Your call #{callId} was cancelled.");
        }

        private void OnEmergencyStatusRequest([FromSource] Player player)
        {
            if (Allow(player, "requests", l => l.Requests)) SendEmergencyStatus(ServerId(player), null);
        }

        private void OnCallClear([FromSource] Player player, string callId)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;
            if (!IsCallId(callId)) return;

            var caller = _dispatch.ClearEmergencyCall(ServerId(player), callId);
            if (caller is null)
            {
                Notify(player, "~r~You can only clear 911 calls you are attached to.");
                return;
            }

            Notify(player, $"~g~Call ~y~#{callId}~g~ cleared.");
            Audit(AuditActions.EmergencyCleared, player, null, $"call #{callId}");
            SendEmergencyStatus(caller.Value, $"~g~911~w~ | Your call #{callId} has been resolved.");
        }

        private void SendEmergencyStatus(int callerId, string? message)
        {
            if (!TryGetPlayer(callerId, out var player)) return;

            if (message is not null) Notify(player, message);
            TriggerClientEvent(player, EventNames.ClientEmergencyStatus, JsonConvert.SerializeObject(_dispatch.GetEmergencyStatus(callerId)));
        }

        private bool TryGetPlayer(int serverId, out Player player)
        {
            player = null!;
            if (serverId <= 0 || string.IsNullOrEmpty(API.GetPlayerName(serverId.ToString()))) return false;

            player = Players[serverId];
            return player is not null;
        }
    }
}
