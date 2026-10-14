using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Appearance;
using FivePRS.Client.Callouts;
using FivePRS.Client.Dispatch;
using FivePRS.Client.Terminal;
using FivePRS.Client.Terminal.Apps;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Logging;
using FivePRS.Core.Models;
using FivePRS.Core.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace FivePRS.Client.App
{
    public class AppClient : BaseScript
    {
        private static readonly Logger Log = Logger.For("Terminal");

        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private const int CreatorAfterDutyMs = 2_000;
        private const int MugshotSettleMs = 250;

        private bool _open;
        private int? _creatorPending;

        public AppClient()
        {
            EventHandlers[EventNames.ClientReceivePlayerData] += new Action<string>(OnPlayerData);
            EventHandlers[EventNames.ClientEntryOptions]      += new Action<string>(OnEntryOptions);
            EventHandlers[EventNames.ClientEntryRejected]     += new Action<string>(OnRejected);
            EventHandlers[EventNames.ClientCivilianState]     += new Action<string>(OnCivilianState);
            EventHandlers[EventNames.ClientCivilianError]     += new Action<string>(OnCivilianError);
            EventHandlers[EventNames.ClientLookupResult]      += new Action<string>(OnLookupResult);
            EventHandlers[EventNames.ClientLookupError]       += new Action<string>(OnLookupError);
            EventHandlers[EventNames.ClientReports]           += new Action<string>(OnReports);
            EventHandlers[EventNames.ClientEmergencyStatus]   += new Action<string>(OnEmergencyStatus);
            EventHandlers[EventNames.ClientEmergencyError]    += new Action<string>(OnEmergencyError);
            EventHandlers[EventNames.ClientAdminState]        += new Action<string>(OnAdminState);
            EventHandlers[EventNames.LocalDutyChanged]        += new Action<bool, int>(OnDutyChanged);
            EventHandlers[EventNames.LocalOpenMenu]           += new Action<string>(OnOpenRequested);
            EventHandlers[EventNames.ClientPreferences]       += new Action<string, string>(OnPreferences);
            EventHandlers[EventNames.ClientAdminTeleport]     += new Action<float, float, float>(OnAdminTeleport);
            EventHandlers[EventNames.ClientShowCard]          += new Action<string>(OnShowCard);
            EventHandlers["onClientResourceStop"]             += new Action<string>(OnResourceStop);

            DispatchClient.SnapshotUpdated += Refresh;
            TerminalApps.Changed           += Refresh;
            CalloutDispatcher.StateChanged += Refresh;
            CivilianPanel.AppearanceRequested += OpenCreator;
            CivilianPanel.MugshotRequested    += TakeMugshot;

            API.RegisterCommand(KeyCommands.Menu, new Action<int, List<object>, string>((_, __, ___) => Toggle()), false);
            API.RegisterKeyMapping(KeyCommands.Menu, "FivePRS: Open menu", "keyboard", "F5");

            RegisterCallback("appClose",     _    => Close());
            RegisterCallback("appOpen",      data => OnAppOpen(data));
            RegisterCallback("appAction",    data => OnAppAction(data));
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
                Log.Error("Could not read player data", ex);
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
                Log.Error("Could not read entry options", ex);
                return;
            }

            Refresh();
        }

        private void OnRejected(string message)
        {
            if (_open)
                DutyApp.ShowError(message);
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
                Log.Error("Could not read civilian state", ex);
                return;
            }

            if (_open && before is not null && CivilianPanel.CharacterCount > before)
            {
                if (!ClientBrain.LocalPlayerData.IsOnDuty)
                {
                    OpenCreator();
                    return;
                }

                _creatorPending = CivilianPanel.ActiveCharacter?.Id;
                var name = CivilianPanel.ActiveCharacter?.FullName ?? "Your character";
                ClientBrain.ShowNotification($"{name} created. Use the locker at your station, or go off duty, to set up their appearance.", "Characters");
            }

            Refresh();
        }

        private void OnCivilianError(string message)
        {
            if (_open)
                CivilianPanel.ShowError(message);
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
                Log.Error("Could not read a lookup result", ex);
                return;
            }

            Refresh();
        }

        private void OnReports(string json)
        {
            try
            {
                RecordsPanel.SetReports(json);
            }
            catch (Exception ex)
            {
                Log.Error("Could not read reports", ex);
                return;
            }

            Refresh();
        }

        private void OnLookupError(string message)
        {
            if (_open)
                RecordsApp.ShowError(message);
            else
                ClientBrain.ShowNotification($"~r~{message}");
        }

        private void OnEmergencyStatus(string json)
        {
            try
            {
                EmergencyApp.SetStatus(json);
            }
            catch (Exception ex)
            {
                Log.Error("Could not read 911 status", ex);
                return;
            }

            Refresh();
        }

        private void OnEmergencyError(string message)
        {
            if (_open)
                EmergencyApp.ShowError(message);
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
                Log.Error("Could not read admin state", ex);
                return;
            }

            Refresh();
        }

        private void OnAppOpen(IDictionary<string, object> data)
        {
            TerminalApps.SetOpen(NuiData.GetString(data, "id"));
            Refresh();
        }

        private void OnAppAction(IDictionary<string, object> data)
        {
            var payload = data.TryGetValue("data", out var raw) && raw is IDictionary<string, object> values
                ? values
                : new Dictionary<string, object>();

            TerminalApps.Act(NuiData.GetString(data, "app"), NuiData.GetString(data, "action"), payload);
            Refresh();
        }

        private void OnPreferences(string wallpaper, string settings)
        {
            TerminalPanel.SetPreferences(wallpaper, settings);
            Refresh();
        }

        private static void OnShowCard(string json)
        {
            try
            {
                var card = JsonConvert.DeserializeObject<LicenseCard>(json);
                if (card is not null) SendTo("card", "show", card);
            }
            catch (Exception ex)
            {
                Log.Warn("Discarded a malformed license card", ex);
            }
        }

        private static async void OnAdminTeleport(float x, float y, float z) =>
            await Teleporter.ToAsync(new Vector3(x, y, z), Game.PlayerPed.Heading);

        private async void TakeMugshot(int characterId)
        {
            if (Game.PlayerPed.IsInVehicle())
            {
                CivilianPanel.ShowError("Step out of your vehicle to take a mugshot.");
                return;
            }

            Close();
            await Delay(MugshotSettleMs);

            var photo = await Mugshot.CaptureAsync();
            if (photo is null)
            {
                ClientBrain.ShowNotification("~r~The mugshot couldn't be taken.~w~ Try again, or use an image link instead.", "Characters");
                return;
            }

            CivilianPanel.SetMugshot(characterId, photo);
            OpenWhenLoaded("characters");
        }

        private void OpenCreator()
        {
            var active = CivilianPanel.ActiveCharacter;
            if (active is null || ClientBrain.LocalPlayerData.IsOnDuty) return;

            Close();
            CharacterCreator.Open(active.Id, active.FullName);
        }

        private async void OnDutyChanged(bool isOnDuty, int departmentId)
        {
            DutyPanel.SetOnDuty(isOnDuty);
            if (!isOnDuty) RecordsPanel.Clear();
            Close();

            if (isOnDuty || _creatorPending is null) return;

            await Delay(CreatorAfterDutyMs);
            var active = CivilianPanel.ActiveCharacter;
            if (active is null || active.Id != _creatorPending || ClientBrain.LocalPlayerData.IsOnDuty) return;

            _creatorPending = null;
            if (AppearanceManager.Current is null || AppearanceManager.CharacterId != active.Id) OpenCreator();
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

        private void OnOpenRequested(string app)
        {
            if (!_open) OpenWhenLoaded(app);
        }

        private void OpenWhenLoaded(string? app)
        {
            if (!DutyPanel.IsLoaded)
            {
                ClientBrain.ShowNotification("~r~Your profile hasn't loaded yet. Please wait.");
                return;
            }

            Open(app);
        }

        private void Open(string? app)
        {
            if (!_open)
                Tick += HoldFocusAsync;

            _open = true;
            NuiFocus.Take();
            Send("open", BuildState(app));
            CivilianPanel.RequestState();
            EmergencyApp.RequestStatus();
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
            if (!_open) return;

            Send("update", BuildState(null));
            TerminalApps.PushOpenState();
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

        private static object BuildState(string? requestedApp)
        {
            return new
            {
                Nameplate  = ConfigManager.Settings.Branding.Nameplate,
                Icons      = ConfigManager.Settings.Branding.Departments,
                Terminal   = TerminalPanel.BuildView(),
                Apps       = TerminalApps.BuildList(),
                AppView    = TerminalApps.BuildOpenView(),
                Context    = PublicState.Build(),
                DefaultApp = requestedApp is not null && TerminalApps.IsAvailable(requestedApp) ? requestedApp : null,
                Identity   = CivilianPanel.ActiveCharacter?.FullName ?? ClientBrain.LocalPlayerData.Name,
                Unit       = DispatchPanel.BuildStatus(),
                Home       = HomePanel.BuildView(),
            };
        }

        private static void Send(string type, object? payload) => SendTo("app", type, payload);

        private static void SendTo(string screen, string type, object? payload) =>
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen, type, payload }, JsonSettings));

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
                    Log.Error($"Terminal action '{name}' failed", ex);
                }

                callback("ok");
            });
        }
    }
}
