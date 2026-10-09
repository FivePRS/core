using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core.Native;
using FivePRS.Client.Callouts;
using FivePRS.Client.Dispatch;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;

namespace FivePRS.Client.App
{
    internal static class DispatchPanel
    {
        public static bool IsAvailable => DispatchClient.LocalUnit is not null;

        public static void SetStatus(IDictionary<string, object> data)
        {
            if (!TryGetString(data, "status", out var value) ||
                !Enum.TryParse<UnitStatus>(value, true, out var status) ||
                (status != UnitStatus.Available && status != UnitStatus.Busy))
            {
                return;
            }

            ClientEvents.TriggerServer(EventNames.ServerSetUnitStatus, (int)status);
        }

        public static void Attach(IDictionary<string, object> data)
        {
            if (TryGetString(data, "callId", out var callId))
                ClientEvents.TriggerServer(EventNames.ServerAttachToCall, callId);
        }

        public static void Waypoint(IDictionary<string, object> data)
        {
            if (!TryGetString(data, "callId", out var callId)) return;

            var call = DispatchClient.Snapshot.Calls.FirstOrDefault(c => c.Id == callId);
            if (call is null || (call.X == 0f && call.Y == 0f)) return;

            API.SetNewWaypoint(call.X, call.Y);
            ClientBrain.ShowNotification($"~b~Waypoint set~w~ to call ~y~#{call.Id}~w~.");
        }

        public static object? BuildView()
        {
            var self = DispatchClient.LocalUnit;
            if (self is null) return null;

            var snapshot = DispatchClient.Snapshot;
            var map      = ConfigManager.Territories;
            var offer    = CalloutDispatcher.PendingOffer;
            var active   = CalloutDispatcher.ActiveCall;

            return new
            {
                offer = offer is null ? null : new
                {
                    offer.Id,
                    offer.Name,
                    Code        = (int)offer.Priority,
                    offer.Description,
                    ExpiresInMs = Math.Max(0, CalloutDispatcher.PendingOfferExpiresAt - API.GetGameTimer()),
                    WindowMs    = ConfigManager.Settings.AcceptWindowSeconds * 1000,
                },
                activeCall = active is null ? null : new
                {
                    active.Id,
                    active.Name,
                },
                self = new
                {
                    self.ServerId,
                    self.Callsign,
                    self.Name,
                    Agency    = map.FindAgency(self.Agency)?.Name ?? self.Department.ToString(),
                    Status    = self.Status.ToString(),
                    self.CallId,
                    Territory = map.FindTerritory(self.Territory)?.Name,
                },
                calls = snapshot.Calls.Select(call => new
                {
                    call.Id,
                    call.Name,
                    Code        = (int)call.Priority,
                    Department  = call.Department.ToString(),
                    Territory   = map.FindTerritory(call.Territory)?.Name,
                    HasLocation = call.X != 0f || call.Y != 0f,
                    Units       = call.Units
                        .Select(id => snapshot.Units.FirstOrDefault(u => u.ServerId == id)?.Callsign)
                        .Where(callsign => callsign is not null)
                        .ToList(),
                    IsPrimary   = call.PrimaryUnit == self.ServerId,
                }),
                units = snapshot.Units
                    .OrderBy(u => u.Callsign, StringComparer.OrdinalIgnoreCase)
                    .Select(unit => new
                    {
                        unit.ServerId,
                        unit.Callsign,
                        unit.Name,
                        Agency    = map.FindAgency(unit.Agency)?.Name ?? unit.Department.ToString(),
                        Status    = unit.Status.ToString(),
                        unit.CallId,
                        Territory = map.FindTerritory(unit.Territory)?.Name,
                    }),
            };
        }

        private static bool TryGetString(IDictionary<string, object> data, string key, out string value)
        {
            value = data.TryGetValue(key, out var raw) ? raw?.ToString() ?? string.Empty : string.Empty;
            return value.Length > 0;
        }
    }
}
