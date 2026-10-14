using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
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
            if (!NuiData.TryGetString(data, "status", out var value) ||
                !Enum.TryParse<UnitStatus>(value, true, out var status) ||
                (status != UnitStatus.Available && status != UnitStatus.Busy))
            {
                return;
            }

            ClientEvents.TriggerServer(EventNames.ServerSetUnitStatus, (int)status);
        }

        public static void ClearCall(IDictionary<string, object> data)
        {
            if (NuiData.TryGetString(data, "callId", out var callId))
                ClientEvents.TriggerServer(EventNames.ServerCallClear, callId);
        }

        public static void Attach(IDictionary<string, object> data)
        {
            if (NuiData.TryGetString(data, "callId", out var callId))
                ClientEvents.TriggerServer(EventNames.ServerAttachToCall, callId);
        }

        public static void Waypoint(IDictionary<string, object> data)
        {
            if (!NuiData.TryGetString(data, "callId", out var callId)) return;

            var call = DispatchClient.Snapshot.Calls.FirstOrDefault(c => c.Id == callId);
            if (call is null || (call.X == 0f && call.Y == 0f)) return;

            API.SetNewWaypoint(call.X, call.Y);
            ClientBrain.ShowNotification($"~b~Waypoint set~w~ to call ~y~#{call.Id}~w~.");
        }

        public static void RequestBackup(IDictionary<string, object> data)
        {
            var code = NuiData.TryGetInt(data, "code", out var value) && value == 2 ? 2 : 3;
            BackupClient.Request(BackupMode.Any, code);
        }

        public static double UnixNowMs() => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        public static object? BuildOffer()
        {
            var offer = CalloutDispatcher.PendingOffer;
            if (offer is null) return null;

            var hasLocation = offer.LocationX != 0f || offer.LocationY != 0f;
            return new
            {
                offer.Id,
                offer.Name,
                Code      = (int)offer.Priority,
                offer.Description,
                DistanceM = hasLocation
                    ? (int)Vector3.Distance(Game.PlayerPed.Position, new Vector3(offer.LocationX, offer.LocationY, offer.LocationZ))
                    : (int?)null,
                ExpiresAt = UnixNowMs() + Math.Max(0, CalloutDispatcher.PendingOfferExpiresAt - API.GetGameTimer()),
                WindowMs  = ConfigManager.Settings.AcceptWindowSeconds * 1000,
            };
        }

        public static object? BuildStatus()
        {
            var self = DispatchClient.LocalUnit;
            if (self is null) return null;

            return new
            {
                self.Callsign,
                Agency = ConfigManager.Territories.FindAgency(self.Agency)?.Name ?? self.Department.ToString(),
                Status = self.Status.ToString(),
            };
        }

        public static object? BuildView()
        {
            var self = DispatchClient.LocalUnit;
            if (self is null) return null;

            var snapshot = DispatchClient.Snapshot;
            var map      = ConfigManager.Territories;
            var active   = CalloutDispatcher.ActiveCall;
            var position = Game.PlayerPed.Position;

            return new
            {
                offer = BuildOffer(),
                activeCall = active is null ? null : new
                {
                    active.Id,
                    active.Name,
                    Code = (int)active.Priority,
                    active.Description,
                    Units = snapshot.Calls.FirstOrDefault(call => call.Id == active.Id)?.Units
                        .Select(id => snapshot.Units.FirstOrDefault(u => u.ServerId == id)?.Callsign)
                        .Where(callsign => callsign is not null)
                        .ToList(),
                },
                self = new
                {
                    self.ServerId,
                    self.Callsign,
                    self.Name,
                    Department = self.Department.ToString(),
                    Agency     = map.FindAgency(self.Agency)?.Name ?? self.Department.ToString(),
                    Status     = self.Status.ToString(),
                    self.CallId,
                    Territory  = map.FindTerritory(self.Territory)?.Name,
                    self.AiCallouts,
                },
                calls = snapshot.Calls.Select(call => new
                {
                    call.Id,
                    call.Name,
                    Code        = (int)call.Priority,
                    Department  = call.Department.ToString(),
                    Territory   = map.FindTerritory(call.Territory)?.Name,
                    HasLocation = call.X != 0f || call.Y != 0f,
                    DistanceM   = call.X != 0f || call.Y != 0f
                        ? (int)Vector3.Distance(position, new Vector3(call.X, call.Y, call.Z))
                        : (int?)null,
                    Units       = call.Units
                        .Select(id => snapshot.Units.FirstOrDefault(u => u.ServerId == id)?.Callsign)
                        .Where(callsign => callsign is not null)
                        .ToList(),
                    IsPrimary   = call.PrimaryUnit == self.ServerId,
                    call.IsEmergency,
                    call.Description,
                    call.Caller,
                    CanClear    = call.IsEmergency && self.CallId == call.Id,
                }),
                units = snapshot.Units
                    .OrderBy(u => u.Callsign, StringComparer.OrdinalIgnoreCase)
                    .Select(unit => new
                    {
                        unit.ServerId,
                        unit.Callsign,
                        unit.Name,
                        Department = unit.Department.ToString(),
                        Agency     = map.FindAgency(unit.Agency)?.Name ?? unit.Department.ToString(),
                        Status     = unit.Status.ToString(),
                        unit.CallId,
                        Territory  = map.FindTerritory(unit.Territory)?.Name,
                    }),
                icons = ConfigManager.Settings.Branding.Departments,
            };
        }
    }
}
