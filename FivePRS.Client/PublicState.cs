using System.Collections.Generic;
using System.Linq;
using FivePRS.Client.Callouts;
using FivePRS.Client.Dispatch;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.Client
{
    internal static class PublicState
    {
        public static IDictionary<string, object> Build()
        {
            var player = ClientBrain.LocalPlayerData;
            var map    = ConfigManager.Territories;

            var state = new Dictionary<string, object>
            {
                ["name"]       = player.Name,
                ["onDuty"]     = player.IsOnDuty,
                ["department"] = player.Department.ToString(),
                ["rank"]       = player.Rank,
            };

            if (!player.IsOnDuty) return state;

            state["agency"]   = map.FindAgency(player.Agency)?.Name ?? player.Department.ToString();
            state["callsign"] = player.Callsign;

            var unit = DispatchClient.LocalUnit;
            if (unit is null) return state;

            state["status"] = unit.Status.ToString();

            var territory = map.FindTerritory(unit.Territory)?.Name;
            if (territory is not null) state["territory"] = territory;

            var call = DispatchClient.Snapshot.Calls.FirstOrDefault(c => c.Id == unit.CallId);
            var callName = CalloutDispatcher.ActiveCall?.Name ?? call?.Name;
            if (unit.CallId is not null && callName is not null)
            {
                state["callId"] = unit.CallId;
                state["call"]   = callName;
            }

            return state;
        }
    }
}
