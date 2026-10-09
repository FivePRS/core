using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Agency;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FivePRS.Client.Entry
{
    public class EntryClient : BaseScript
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private PlayerData? _profile;
        private List<Department>? _allowed;
        private bool _open;

        public EntryClient()
        {
            EventHandlers[EventNames.ClientReceivePlayerData] += new Action<string>(OnPlayerData);
            EventHandlers[EventNames.ClientEntryOptions]      += new Action<string>(OnEntryOptions);
            EventHandlers[EventNames.ClientEntryRejected]     += new Action<string>(OnRejected);
            EventHandlers[EventNames.LocalDutyChanged]        += new Action<bool, int>(OnDutyChanged);
            EventHandlers["onClientResourceStop"]             += new Action<string>(OnResourceStop);

            API.RegisterCommand("duty", new Action<int, List<object>, string>((_, __, ___) => OnDutyCommand()), false);
            API.RegisterKeyMapping("duty", "FivePRS: Duty menu", "keyboard", "F5");

            RegisterCallback("entryClose", _    => Close());
            RegisterCallback("entryEnter", data => Enter(data));
        }

        private void OnPlayerData(string json)
        {
            try
            {
                _profile = JsonConvert.DeserializeObject<PlayerData>(json) ?? _profile;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EntryClient] Failed to parse player data: {ex.Message}");
                return;
            }

            if (_open && _profile is not null && !_profile.IsOnDuty)
                Send("open", BuildView());
        }

        private void OnEntryOptions(string json)
        {
            try
            {
                _allowed = JsonConvert.DeserializeObject<List<int>>(json)?
                    .Where(id => Enum.IsDefined(typeof(Department), id))
                    .Select(id => (Department)id)
                    .ToList();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EntryClient] Failed to parse entry options: {ex.Message}");
                return;
            }

            if (_open)
                Send("open", BuildView());
        }

        private void OnDutyCommand()
        {
            if (_open)
            {
                Close();
                return;
            }

            if (_profile is null || _allowed is null)
            {
                ClientBrain.ShowNotification("~r~Your profile hasn't loaded yet. Please wait.");
                return;
            }

            if (_profile.IsOnDuty)
            {
                ClientEvents.TriggerServer(EventNames.ServerToggleDuty);
                return;
            }

            Open();
        }

        private void Open()
        {
            if (!_open)
                Tick += HoldFocusAsync;

            _open = true;
            NuiFocus.Take();
            Send("open", BuildView());
        }

        private void Close()
        {
            if (!_open) return;

            _open = false;
            Tick -= HoldFocusAsync;
            NuiFocus.Release();
            Send("close", null);
        }

        private async Task HoldFocusAsync()
        {
            if (_open)
                NuiFocus.EnsureTaken();

            await Delay(250);
        }

        private void Enter(IDictionary<string, object> data)
        {
            if (!data.TryGetValue("departmentId", out var rawDepartment) ||
                !int.TryParse(rawDepartment?.ToString(), out var departmentId))
            {
                return;
            }

            var agencyId    = data.TryGetValue("agencyId", out var rawAgency) ? rawAgency?.ToString() ?? string.Empty : string.Empty;
            var rawCallsign = data.TryGetValue("callsign", out var callsignValue) ? callsignValue?.ToString() : null;

            if (!Callsign.TryNormalize(rawCallsign, out var callsign))
            {
                Send("rejected", $"Callsigns can be up to {Callsign.MaxLength} letters, numbers or hyphens.");
                return;
            }

            ClientEvents.TriggerServer(EventNames.ServerEnterService, departmentId, agencyId, callsign);
        }

        private void OnRejected(string message)
        {
            if (_open)
                Send("rejected", message);
            else
                ClientBrain.ShowNotification($"~r~{message}");
        }

        private void OnDutyChanged(bool isOnDuty, int departmentId)
        {
            if (_profile is not null)
                _profile.IsOnDuty = isOnDuty;

            if (isOnDuty)
                Close();
        }

        private void OnResourceStop(string resourceName)
        {
            if (resourceName == API.GetCurrentResourceName() && _open)
                NuiFocus.Release();
        }

        private object BuildView()
        {
            var profile = _profile ?? new PlayerData();
            var map     = ConfigManager.Territories;

            var departments = (_allowed ?? new List<Department>())
                .Where(d => d != Department.None && BaseAgency.IsDepartmentLoaded(d))
                .Select(d => new
                {
                    Id       = (int)d,
                    Name     = d.ToString(),
                    Agencies = map.AgenciesFor(d).Select(a => new { a.Id, a.Name, a.CallsignPrefix }).ToList(),
                })
                .ToList();

            return new
            {
                profile.Name,
                profile.Rank,
                profile.XP,
                XpToNext          = profile.XPToNextRank,
                DepartmentId      = (int)profile.Department,
                AgencyId          = profile.Agency,
                profile.Callsign,
                CallsignMaxLength = Callsign.MaxLength,
                Departments       = departments,
            };
        }

        private static void Send(string type, object? payload) =>
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen = "entry", type, payload }, JsonSettings));

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
                    Debug.WriteLine($"[EntryClient] NUI callback '{name}' failed: {ex.Message}");
                }

                callback("ok");
            });
        }
    }
}
