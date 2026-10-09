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
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FivePRS.Client.Mdt
{
    public class MdtClient : BaseScript
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private bool _open;

        public MdtClient()
        {
            API.RegisterCommand("mdt", new Action<int, List<object>, string>((_, __, ___) => Toggle()), false);
            API.RegisterKeyMapping("mdt", "FivePRS: Open MDT", "keyboard", "F7");

            RegisterCallback("mdtClose",  _    => Close());
            RegisterCallback("setStatus", data => SetStatus(data));
            RegisterCallback("attach",    data => Attach(data));
            RegisterCallback("waypoint",  data => Waypoint(data));
            RegisterCallback("offerAccept",  _ => CalloutDispatcher.AcceptOffer());
            RegisterCallback("offerDecline", _ => CalloutDispatcher.DeclineOffer());
            RegisterCallback("endCall",      _ => CalloutDispatcher.EndActiveCall());

            DispatchClient.SnapshotUpdated += OnSnapshotUpdated;
            CalloutDispatcher.StateChanged += OnCalloutStateChanged;
            EventHandlers["onClientResourceStop"] += new Action<string>(OnResourceStop);
        }

        private void Toggle()
        {
            if (_open)
            {
                Close();
                return;
            }

            if (DispatchClient.LocalUnit is null)
            {
                ClientBrain.ShowNotification("~r~You must be on duty to use the MDT.");
                return;
            }

            _open = true;
            NuiFocus.Take();
            Send("open", BuildView());
        }

        private void Close()
        {
            if (!_open) return;

            _open = false;
            NuiFocus.Release();
            Send("close", null);
        }

        private void OnSnapshotUpdated()
        {
            if (!_open) return;

            if (DispatchClient.LocalUnit is null)
            {
                Close();
                return;
            }

            Send("update", BuildView());
        }

        private void OnCalloutStateChanged()
        {
            if (_open && DispatchClient.LocalUnit is not null)
                Send("update", BuildView());
        }

        private static void SetStatus(IDictionary<string, object> data)
        {
            if (!TryGetString(data, "status", out var value) ||
                !Enum.TryParse<UnitStatus>(value, true, out var status) ||
                (status != UnitStatus.Available && status != UnitStatus.Busy))
            {
                return;
            }

            ClientEvents.TriggerServer(EventNames.ServerSetUnitStatus, (int)status);
        }

        private static void Attach(IDictionary<string, object> data)
        {
            if (TryGetString(data, "callId", out var callId))
                ClientEvents.TriggerServer(EventNames.ServerAttachToCall, callId);
        }

        private static void Waypoint(IDictionary<string, object> data)
        {
            if (!TryGetString(data, "callId", out var callId)) return;

            var call = DispatchClient.Snapshot.Calls.FirstOrDefault(c => c.Id == callId);
            if (call is null || (call.X == 0f && call.Y == 0f)) return;

            API.SetNewWaypoint(call.X, call.Y);
            ClientBrain.ShowNotification($"~b~Waypoint set~w~ to call ~y~#{call.Id}~w~.");
        }

        private void OnResourceStop(string resourceName)
        {
            if (resourceName == API.GetCurrentResourceName() && _open)
                NuiFocus.Release();
        }

        private static object BuildView()
        {
            var snapshot  = DispatchClient.Snapshot;
            var self      = DispatchClient.LocalUnit;
            var map       = ConfigManager.Territories;
            var offer     = CalloutDispatcher.PendingOffer;
            var active    = CalloutDispatcher.ActiveCall;

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
                self = self is null ? null : new
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
                    IsPrimary   = self is not null && call.PrimaryUnit == self.ServerId,
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

        private static void Send(string type, object? payload) =>
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen = "mdt", type, payload }, JsonSettings));

        private void RegisterCallback(string name, Action<IDictionary<string, object>> handler)
        {
            API.RegisterNuiCallbackType(name);
            EventHandlers[$"__cfx_nui:{name}"] += new Action<IDictionary<string, object>, CallbackDelegate>((data, callback) =>
            {
                try
                {
                    handler(data);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MdtClient] NUI callback '{name}' failed: {ex.Message}");
                }

                callback("ok");
            });
        }

        private static bool TryGetString(IDictionary<string, object> data, string key, out string value)
        {
            value = data.TryGetValue(key, out var raw) ? raw?.ToString() ?? string.Empty : string.Empty;
            return value.Length > 0;
        }
    }
}
