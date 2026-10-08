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
        private bool _shownOnJoin;
        private bool _open;

        public EntryClient()
        {
            EventHandlers[EventNames.ClientReceivePlayerData] += new Action<string>(OnPlayerData);
            EventHandlers[EventNames.ClientEntryOptions]      += new Action<string>(OnEntryOptions);
            EventHandlers[EventNames.ClientEntryRejected]     += new Action<string>(OnRejected);
            EventHandlers[EventNames.LocalDutyChanged]        += new Action<bool, int>(OnDutyChanged);
            EventHandlers["onClientResourceStop"]             += new Action<string>(OnResourceStop);

            API.RegisterCommand("fiveprs", new Action<int, List<object>, string>((_, __, ___) => OpenFromCommand()), false);

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

            _ = ShowOnJoinAsync();
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

            _ = ShowOnJoinAsync();
        }

        private async Task ShowOnJoinAsync()
        {
            if (_shownOnJoin || _profile is null || _allowed is null) return;
            _shownOnJoin = true;

            if (!ConfigManager.Settings.ShowEntryScreen || _profile.IsOnDuty) return;

            while (API.GetIsLoadingScreenActive() || API.IsScreenFadedOut() || !Game.PlayerPed.Exists())
                await Delay(250);

            await Delay(1000);
            if (_profile is not null && !_profile.IsOnDuty)
                Open();
        }

        private void OpenFromCommand()
        {
            if (_profile is null || _allowed is null)
            {
                ClientBrain.ShowNotification("~r~Your profile hasn't loaded yet. Please wait.");
                return;
            }

            if (_profile.IsOnDuty)
            {
                ClientBrain.ShowNotification("~r~Go off duty before changing your department or agency.");
                return;
            }

            Open();
        }

        private void Open()
        {
            _open = true;
            API.SetNuiFocus(true, true);
            Send("open", BuildView());
        }

        private void Close()
        {
            if (!_open) return;

            _open = false;
            API.SetNuiFocus(false, false);
            Send("close", null);
        }

        private static void Enter(IDictionary<string, object> data)
        {
            if (!data.TryGetValue("departmentId", out var rawDepartment) ||
                !int.TryParse(rawDepartment?.ToString(), out var departmentId))
            {
                return;
            }

            var agencyId = data.TryGetValue("agencyId", out var rawAgency) ? rawAgency?.ToString() ?? string.Empty : string.Empty;
            TriggerServerEvent(EventNames.ServerEnterService, departmentId, agencyId);
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
                API.SetNuiFocus(false, false);
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
                XpToNext     = profile.XPToNextRank,
                DepartmentId = (int)profile.Department,
                AgencyId     = profile.Agency,
                Departments  = departments,
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
