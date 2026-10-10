using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Appearance;
using FivePRS.Client.Callouts;
using FivePRS.Client.Dispatch;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Core.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FivePRS.Client.App
{
    public class AppClient : BaseScript
    {
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private bool _open;

        public AppClient()
        {
            EventHandlers[EventNames.ClientReceivePlayerData] += new Action<string>(OnPlayerData);
            EventHandlers[EventNames.ClientEntryOptions]      += new Action<string>(OnEntryOptions);
            EventHandlers[EventNames.ClientEntryRejected]     += new Action<string>(OnRejected);
            EventHandlers[EventNames.ClientCivilianState]     += new Action<string>(OnCivilianState);
            EventHandlers[EventNames.ClientCivilianError]     += new Action<string>(OnCivilianError);
            EventHandlers[EventNames.ClientLookupResult]      += new Action<string>(OnLookupResult);
            EventHandlers[EventNames.ClientLookupError]       += new Action<string>(OnLookupError);
            EventHandlers[EventNames.ClientEmergencyStatus]   += new Action<string>(OnEmergencyStatus);
            EventHandlers[EventNames.ClientEmergencyError]    += new Action<string>(OnEmergencyError);
            EventHandlers[EventNames.ClientAdminState]        += new Action<string>(OnAdminState);
            EventHandlers[EventNames.LocalDutyChanged]        += new Action<bool, int>(OnDutyChanged);
            EventHandlers[EventNames.LocalOpenMenu]           += new Action<string>(OnOpenRequested);
            EventHandlers["onClientResourceStop"]             += new Action<string>(OnResourceStop);

            DispatchClient.SnapshotUpdated += Refresh;
            CalloutDispatcher.StateChanged += Refresh;

            API.RegisterCommand(KeyCommands.Menu, new Action<int, List<object>, string>((_, __, ___) => Toggle()), false);
            API.RegisterKeyMapping(KeyCommands.Menu, "FivePRS: Open menu", "keyboard", "F5");

            RegisterCallback("appClose",     _    => Close());
            RegisterCallback("dutyEnter",    data => OnDutyEnter(data));
            RegisterCallback("dutyOff",      _    => DutyPanel.GoOffDuty());
            RegisterCallback("setStatus",    data => DispatchPanel.SetStatus(data));
            RegisterCallback("attach",       data => DispatchPanel.Attach(data));
            RegisterCallback("aiCallouts",   data => DispatchPanel.SetAiCallouts(data));
            RegisterCallback("waypoint",     data => DispatchPanel.Waypoint(data));
            RegisterCallback("offerAccept",  _    => CalloutDispatcher.AcceptOffer());
            RegisterCallback("offerDecline", _    => CalloutDispatcher.DeclineOffer());
            RegisterCallback("endCall",      _    => CalloutDispatcher.EndActiveCall());
            RegisterCallback("civCreateCharacter", data => CivilianPanel.CreateCharacter(data));
            RegisterCallback("civSelectCharacter", data => CivilianPanel.SelectCharacter(data));
            RegisterCallback("civDeleteCharacter", data => CivilianPanel.DeleteCharacter(data));
            RegisterCallback("civApplyLicense",    data => CivilianPanel.ApplyLicense(data));
            RegisterCallback("civRegisterVehicle", _    => OnRegisterVehicle());
            RegisterCallback("civRemoveVehicle",   data => CivilianPanel.RemoveVehicle(data));
            RegisterCallback("civAppearance",      _    => OpenCreator());
            RegisterCallback("civSetVehicleStolen", data => CivilianPanel.SetVehicleStolen(data));
            RegisterCallback("recSearchName",   data => RecordsPanel.SearchName(data));
            RegisterCallback("recSearchPlate",  data => RecordsPanel.SearchPlate(data));
            RegisterCallback("recOpen",         data => RecordsPanel.Open(data));
            RegisterCallback("recBack",         _    => { RecordsPanel.CloseRecord(); Refresh(); });
            RegisterCallback("recIssue",        data => RecordsPanel.Issue(data));
            RegisterCallback("recResolve",      data => RecordsPanel.Resolve(data));
            RegisterCallback("recSetLicense",   data => RecordsPanel.SetLicense(data));
            RegisterCallback("recFlagVehicle",  data => RecordsPanel.FlagVehicle(data));
            RegisterCallback("emCall",          data => EmergencyPanel.Call(data));
            RegisterCallback("emCancel",        _    => EmergencyPanel.Cancel());
            RegisterCallback("callClear",       data => EmergencyPanel.ClearCall(data));
            RegisterCallback("adminSearch",     data => AdminPanel.Search(data));
            RegisterCallback("adminRoster",     data => AdminPanel.SetRoster(data));
        }

        private void OnPlayerData(string json)
        {
            try
            {
                var profile = JsonConvert.DeserializeObject<PlayerData>(json);
                if (profile is null) return;
                DutyPanel.SetProfile(profile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppClient] Failed to parse player data: {ex.Message}");
                return;
            }

            Refresh();
        }

        private void OnEntryOptions(string json)
        {
            try
            {
                DutyPanel.SetAllowed(JsonConvert.DeserializeObject<List<int>>(json) ?? new List<int>());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppClient] Failed to parse entry options: {ex.Message}");
                return;
            }

            Refresh();
        }

        private void OnRejected(string message)
        {
            if (_open)
                Send("rejected", message);
            else
                ClientBrain.ShowNotification($"~r~{message}");
        }

        private void OnCivilianState(string json)
        {
            var before = CivilianPanel.CharacterCount;
            try
            {
                CivilianPanel.SetState(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppClient] Failed to parse civilian state: {ex.Message}");
                return;
            }

            if (_open && before is not null && CivilianPanel.CharacterCount > before)
            {
                OpenCreator();
                return;
            }

            Refresh();
        }

        private void OnCivilianError(string message)
        {
            if (_open)
                Send("civilianError", message);
            else
                ClientBrain.ShowNotification($"~r~{message}");
        }

        private void OnLookupResult(string json)
        {
            try
            {
                RecordsPanel.SetResult(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppClient] Failed to parse lookup result: {ex.Message}");
                return;
            }

            Refresh();
        }

        private void OnLookupError(string message)
        {
            if (_open)
                Send("recordsError", message);
            else
                ClientBrain.ShowNotification($"~r~{message}");
        }

        private void OnEmergencyStatus(string json)
        {
            try
            {
                EmergencyPanel.SetStatus(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppClient] Failed to parse 911 status: {ex.Message}");
                return;
            }

            Refresh();
        }

        private void OnEmergencyError(string message)
        {
            if (_open)
                Send("emergencyError", message);
            else
                ClientBrain.ShowNotification($"~r~{message}");
        }

        private void OnAdminState(string json)
        {
            try
            {
                AdminPanel.SetState(json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AppClient] Failed to parse admin state: {ex.Message}");
                return;
            }

            Refresh();
        }

        private void OpenCreator()
        {
            var active = CivilianPanel.ActiveCharacter;
            if (active is null || ClientBrain.LocalPlayerData.IsOnDuty) return;

            Close();
            CharacterCreator.Open(active.Id, active.FullName);
        }

        private void OnRegisterVehicle()
        {
            var error = CivilianPanel.RegisterVehicle();
            if (error is not null)
                Send("civilianError", error);
        }

        private void OnDutyChanged(bool isOnDuty, int departmentId)
        {
            DutyPanel.SetOnDuty(isOnDuty);
            if (!isOnDuty) RecordsPanel.Clear();
            Close();
        }

        private void OnDutyEnter(IDictionary<string, object> data)
        {
            var error = DutyPanel.Enter(data);
            if (error is not null)
                Send("rejected", error);
        }

        private void Toggle()
        {
            if (_open)
            {
                Close();
                return;
            }

            OpenWhenLoaded(null);
        }

        private void OnOpenRequested(string tab)
        {
            if (!_open) OpenWhenLoaded(tab);
        }

        private void OpenWhenLoaded(string? tab)
        {
            if (!DutyPanel.IsLoaded)
            {
                ClientBrain.ShowNotification("~r~Your profile hasn't loaded yet. Please wait.");
                return;
            }

            Open(tab);
        }

        private void Open(string? tab)
        {
            if (!_open)
                Tick += HoldFocusAsync;

            _open = true;
            NuiFocus.Take();
            Send("open", BuildState(tab));
            CivilianPanel.RequestState();
            EmergencyPanel.RequestStatus();
            AdminPanel.Request();
        }

        private void Close()
        {
            if (!_open) return;

            _open = false;
            Tick -= HoldFocusAsync;
            NuiFocus.Release();
            Send("close", null);
        }

        private void Refresh()
        {
            if (_open)
                Send("update", BuildState(null));
        }

        private async Task HoldFocusAsync()
        {
            if (_open)
                NuiFocus.EnsureTaken();

            await Delay(250);
        }

        private void OnResourceStop(string resourceName)
        {
            if (resourceName == API.GetCurrentResourceName() && _open)
                NuiFocus.Release();
        }

        private static object BuildState(string? requestedTab)
        {
            var dispatch = DispatchPanel.BuildView();
            var tabs     = new List<string> { "duty" };
            var records  = RecordsPanel.BuildView();
            if (dispatch is not null) tabs.Add("dispatch");
            if (records is not null) tabs.Add("records");
            tabs.Add("civilian");
            tabs.Add("emergency");
            var admin = AdminPanel.BuildView();
            if (admin is not null) tabs.Add("admin");

            var defaultTab = requestedTab is not null && tabs.Contains(requestedTab)
                ? requestedTab
                : dispatch is not null ? "dispatch" : DutyPanel.HasDepartment ? "duty" : "civilian";

            return new
            {
                Tabs       = tabs,
                Nameplate  = ConfigManager.Settings.Branding.Nameplate,
                Icons      = ConfigManager.Settings.Branding.Departments,
                DefaultTab = defaultTab,
                Duty       = DutyPanel.BuildView(),
                Dispatch   = dispatch,
                Records    = records,
                Civilian   = CivilianPanel.BuildView(),
                Emergency  = EmergencyPanel.BuildView(),
                Admin      = admin,
            };
        }

        private static void Send(string type, object? payload) =>
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen = "app", type, payload }, JsonSettings));

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
                    Debug.WriteLine($"[AppClient] NUI callback '{name}' failed: {ex.Message}");
                }

                callback("ok");
            });
        }
    }
}
