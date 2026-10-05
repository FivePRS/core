using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Client.Dispatch
{
    public class DispatchClient : BaseScript
    {
        public static DispatchSnapshot Snapshot { get; private set; } = new();

        public static event Action? SnapshotUpdated;

        public static UnitInfo? LocalUnit =>
            Snapshot.Units.FirstOrDefault(u => u.ServerId == Game.Player.ServerId);

        private string? _lastCallId;

        public DispatchClient()
        {
            EventHandlers[EventNames.ClientDispatchSnapshot] += new Action<string>(OnSnapshot);
            EventHandlers[EventNames.ClientNotify]           += new Action<string>(ClientBrain.ShowNotification);

            API.RegisterCommand("er_calls",  new Action<int, List<object>, string>(OnCallsCommand),  false);
            API.RegisterCommand("er_attach", new Action<int, List<object>, string>(OnAttachCommand), false);
            API.RegisterCommand("er_status", new Action<int, List<object>, string>(OnStatusCommand), false);
        }

        private void OnSnapshot(string json)
        {
            try
            {
                Snapshot = JsonConvert.DeserializeObject<DispatchSnapshot>(json) ?? new DispatchSnapshot();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DispatchClient] Malformed snapshot discarded: {ex.Message}");
                return;
            }

            var callId = LocalUnit?.CallId;
            if (callId is not null && callId != _lastCallId)
            {
                var call = Snapshot.Calls.FirstOrDefault(c => c.Id == callId);
                if (call is not null && call.PrimaryUnit != Game.Player.ServerId && (call.X != 0f || call.Y != 0f))
                    API.SetNewWaypoint(call.X, call.Y);
            }
            _lastCallId = callId;

            SnapshotUpdated?.Invoke();
        }

        private static void OnCallsCommand(int source, List<object> args, string raw)
        {
            if (LocalUnit is null)
            {
                ClientBrain.ShowNotification("~r~You must be on duty to view active calls.");
                return;
            }

            if (Snapshot.Calls.Count == 0)
            {
                ClientBrain.ShowNotification("~y~[ DISPATCH ]~w~ No active calls.");
                return;
            }

            var lines = Snapshot.Calls.Select(call =>
            {
                var callsigns = call.Units
                    .Select(id => Snapshot.Units.FirstOrDefault(u => u.ServerId == id)?.Callsign)
                    .Where(c => c is not null);
                var territory = ConfigManager.Territories.FindTerritory(call.Territory)?.Name;
                var area      = territory is null ? string.Empty : $" {territory}";
                return $"~y~#{call.Id}~w~ {call.Name} (Code {(int)call.Priority}){area} ~b~{string.Join(", ", callsigns)}";
            });

            ClientBrain.ShowNotification("~y~[ DISPATCH ]~w~ Active calls~n~" + string.Join("~n~", lines));
        }

        private static void OnAttachCommand(int source, List<object> args, string raw)
        {
            var callId = args.Count > 0 ? args[0]?.ToString()?.TrimStart('#') : null;
            if (string.IsNullOrEmpty(callId))
            {
                ClientBrain.ShowNotification("~r~Usage: ~w~/er_attach [call id]");
                return;
            }

            TriggerServerEvent(EventNames.ServerAttachToCall, callId);
        }

        private static void OnStatusCommand(int source, List<object> args, string raw)
        {
            var input = args.Count > 0 ? args[0]?.ToString() : null;
            if (!Enum.TryParse<UnitStatus>(input, true, out var status) ||
                (status != UnitStatus.Available && status != UnitStatus.Busy))
            {
                ClientBrain.ShowNotification("~r~Usage: ~w~/er_status [available|busy]");
                return;
            }

            TriggerServerEvent(EventNames.ServerSetUnitStatus, (int)status);
        }
    }
}
