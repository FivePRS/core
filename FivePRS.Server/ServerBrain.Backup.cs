using System;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Server.Database;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private void RegisterBackupEvents()
        {
            EventHandlers[EventNames.ServerRequestBackup] += new Action<Player, int>(OnRequestBackup);
            EventHandlers[EventNames.ServerCancelBackup]  += new Action<Player>(OnCancelBackup);
        }

        private void OnRequestBackup([FromSource] Player player, int code)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;

            if (!ConfigManager.Settings.Backup.Enabled)
            {
                Notify(player, "~r~Backup requests are turned off on this server.");
                return;
            }

            var requesterId = ServerId(player);
            var position = API.GetEntityCoords(API.GetPlayerPed(player.Handle));
            var (callId, error) = _dispatch.RequestBackup(requesterId, code, position.X, position.Y, position.Z);
            if (error is not null)
            {
                Notify(player, $"~r~{error}");
                return;
            }

            var unit     = _dispatch.GetUnit(requesterId);
            var callsign = unit?.Callsign ?? player.Name;
            var urgency  = code == 3 ? "~r~Code 3~w~" : "~y~Code 2~w~";
            var notified = 0;

            foreach (var responderId in _dispatch.BackupResponders(requesterId))
            {
                if (!TryGetPlayer(responderId, out var responder)) continue;

                Notify(responder, $"~b~Backup requested~w~ | {callsign} · {urgency} · call ~y~#{callId}~w~. Attach in the Dispatch app or with /er_attach {callId}.");
                TriggerClientEvent(responder, EventNames.ClientBackupRequested, callId, callsign, code, position.X, position.Y, position.Z);
                notified++;
            }

            Audit(AuditActions.BackupRequested, player, null, $"call #{callId} code {code}");
            Notify(player, notified > 0
                ? $"~b~Backup~w~ | {urgency} request sent to {notified} unit{(notified == 1 ? "" : "s")} on call ~y~#{callId}~w~."
                : $"~b~Backup~w~ | {urgency} request logged on call ~y~#{callId}~w~. No other units are on duty.");
        }

        private void OnCancelBackup([FromSource] Player player)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;

            if (_dispatch.CancelBackup(ServerId(player)))
                Notify(player, "~b~Backup~w~ | Request cancelled.");
        }
    }
}
