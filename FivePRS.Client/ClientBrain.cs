using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Agency;
using FivePRS.Client.Arrest;
using FivePRS.Client.Callouts;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Client
{
    public class ClientBrain : BaseScript
    {
        public static PlayerData LocalPlayerData { get; private set; } = new();

        private bool _profileLoaded = false;

        public ClientBrain()
        {
            ConfigManager.Log = message => Debug.WriteLine(message);

            var res = API.GetCurrentResourceName();
            ConfigManager.LoadSettings(      API.LoadResourceFile(res, "config/settings.json"));
            ConfigManager.LoadPoliceVehicles( API.LoadResourceFile(res, "config/police_vehicles.json"));
            ConfigManager.LoadPoliceLoadouts( API.LoadResourceFile(res, "config/police_loadouts.json"));

            EventHandlers[EventNames.ClientReceivePlayerData] += new Action<string>(OnReceivePlayerData);
            EventHandlers[EventNames.ClientDutyStatusChanged] += new Action<bool, int>(OnDutyStatusChanged);
            EventHandlers[EventNames.ClientCalloutStarted]    += new Action<string>(OnCalloutStarted);
            EventHandlers[EventNames.ClientRankedUp]          += new Action<int>(OnRankedUp);

            API.RegisterCommand("duty",      new Action<int, List<object>, string>(OnDutyCommand),    false);
            API.RegisterCommand("setdept",   new Action<int, List<object>, string>(OnSetDeptCommand), false);

            API.RegisterCommand("er_accept",  new Action<int, List<object>, string>((_, __, ___) =>
            {
                CalloutDispatcher.AcceptPressed = true;
            }), false);
            API.RegisterCommand("er_decline", new Action<int, List<object>, string>((_, __, ___) =>
            {
                CalloutDispatcher.DeclinePressed = true;
            }), false);
            API.RegisterCommand("er_end_callout", new Action<int, List<object>, string>((_, __, ___) =>
            {
                CalloutDispatcher.EndCalloutPressed = true;
            }), false);

            API.RegisterCommand("er_cuff", new Action<int, List<object>, string>(async (_, __, ___) =>
            {
                await ArrestManager.TryCuffNearestAsync();
            }), false);
            API.RegisterCommand("er_uncuff", new Action<int, List<object>, string>((_, __, ___) =>
            {
                ArrestManager.Uncuff();
            }), false);
            API.RegisterCommand("er_escort", new Action<int, List<object>, string>(async (_, __, ___) =>
            {
                var vehicle = Game.PlayerPed.CurrentVehicle;
                if (vehicle == null || !vehicle.Exists())
                {
                    ShowNotification("~r~You must be inside a vehicle to escort a suspect.");
                    return;
                }
                await ArrestManager.EscortToVehicleAsync(vehicle);
            }), false);
            API.RegisterCommand("er_profile", new Action<int, List<object>, string>((_, __, ___) =>
            {
                var d = LocalPlayerData;
                ShowNotification(
                    $"~b~{d.Name}~w~ | {d.Department} | Rank {d.Rank}~n~~g~XP: {d.XP}");
            }), false);
            API.RegisterCommand("er_help", new Action<int, List<object>, string>((_, __, ___) =>
            {
                ShowNotification(
                    "~y~FivePRS Commands~w~~n~" +
                    "~b~/duty~w~ — Toggle on/off duty~n~" +
                    "~b~/setdept [id]~w~ — Set department~n~" +
                    "~b~/er_profile~w~ — View rank and XP~n~" +
                    "~b~/er_accept~w~ — Accept incoming callout~n~" +
                    "~b~/er_decline~w~ — Decline incoming callout~n~" +
                    "~b~/er_end_callout~w~ — End active callout~n~" +
                    "~b~/er_cuff~w~ — Cuff nearest suspect~n~" +
                    "~b~/er_uncuff~w~ — Release cuffed suspect~n~" +
                    "~b~/er_escort~w~ — Place suspect in your vehicle");
            }), false);

            API.RegisterKeyMapping("duty",          "FivePRS: Toggle on/off duty",          "keyboard", "F5");
            API.RegisterKeyMapping("er_accept",      "FivePRS: Accept callout",              "keyboard", "Y");
            API.RegisterKeyMapping("er_decline",     "FivePRS: Decline callout",             "keyboard", "N");
            API.RegisterKeyMapping("er_end_callout", "FivePRS: End active callout",          "keyboard", "END");
            API.RegisterKeyMapping("er_cuff",        "FivePRS: Cuff nearest suspect",        "keyboard", "G");
            API.RegisterKeyMapping("er_uncuff",      "FivePRS: Release cuffed suspect",      "keyboard", "");
            API.RegisterKeyMapping("er_escort",      "FivePRS: Escort suspect to vehicle",   "keyboard", "H");
            API.RegisterKeyMapping("er_profile",     "FivePRS: View rank and XP",            "keyboard", "F6");
            API.RegisterKeyMapping("er_help",        "FivePRS: Show command list",           "keyboard", "");

            Tick += WaitForSpawnTick;
        }

        private async Task WaitForSpawnTick()
        {
            if (Game.PlayerPed.Exists() && Game.PlayerPed.Handle != 0)
            {
                Tick -= WaitForSpawnTick;
                TriggerServerEvent(EventNames.ServerPlayerConnected);
            }
            await Delay(500);
        }

        private void OnReceivePlayerData(string json)
        {
            try
            {
                var data = JsonConvert.DeserializeObject<PlayerData>(json);
                if (data is null) return;

                LocalPlayerData = data;
                _profileLoaded  = true;
                Debug.WriteLine($"[FivePRS] Profile loaded: {data.Name} | Rank {data.Rank} | {data.Department}");

                if (data.Department == Department.None)
                    ShowNotification($"~y~Welcome to FivePRS!~w~ Choose your department with ~b~/setdept~w~.~n~{DepartmentOptions()}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Failed to parse player data: {ex.Message}");
            }
        }

        private void OnDutyStatusChanged(bool isOnDuty, int departmentId)
        {
            LocalPlayerData.IsOnDuty   = isOnDuty;
            LocalPlayerData.Department = (Department)departmentId;

            TriggerEvent(EventNames.LocalDutyChanged, isOnDuty, departmentId);
        }

        private void OnRankedUp(int newRank)
        {
            LocalPlayerData.Rank = newRank;
            ShowNotification($"~g~RANK UP!~w~ You are now Rank {newRank}. Keep it up!");
        }

        private void OnCalloutStarted(string calloutJson)
        {
            try
            {
                _ = JsonConvert.DeserializeObject<CalloutData>(calloutJson)
                    ?? throw new InvalidOperationException("Callout JSON was null after deserialisation.");

                TriggerEvent(EventNames.LocalCalloutReceived, calloutJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Malformed callout payload discarded: {ex.Message}");
            }
        }

        private void OnDutyCommand(int source, List<object> args, string raw)
        {
            if (!_profileLoaded)
            {
                ShowNotification("~r~Your profile hasn't loaded yet. Please wait.");
                return;
            }

            if (!LocalPlayerData.IsOnDuty)
            {
                var dept = LocalPlayerData.Department;
                if (dept == Department.None)
                {
                    ShowNotification($"~r~No department selected.~w~ Use ~b~/setdept~w~ first.~n~{DepartmentOptions()}");
                    return;
                }
                if (!BaseAgency.IsDepartmentLoaded(dept))
                {
                    ShowNotification($"~r~The {dept} module is not installed on this server.");
                    return;
                }
            }

            TriggerServerEvent(EventNames.ServerToggleDuty);
        }

        private void OnSetDeptCommand(int source, List<object> args, string raw)
        {
            if (args.Count < 1 || !int.TryParse(args[0]?.ToString(), out int deptId)
                || !Enum.IsDefined(typeof(Department), deptId) || deptId == (int)Department.None)
            {
                ShowNotification($"~r~Usage: ~w~/setdept [id]~n~{DepartmentOptions()}");
                return;
            }

            var dept = (Department)deptId;
            if (!BaseAgency.IsDepartmentLoaded(dept))
            {
                ShowNotification($"~r~The {dept} module is not installed on this server.~n~{DepartmentOptions()}");
                return;
            }

            TriggerServerEvent(EventNames.ServerSetDepartment, deptId);
        }

        private static string DepartmentOptions()
        {
            var options = Enum.GetValues(typeof(Department))
                .Cast<Department>()
                .Where(d => d != Department.None && BaseAgency.IsDepartmentLoaded(d))
                .Select(d => $"~b~{(int)d}~w~={d}");
            return "Available: " + string.Join("  ", options);
        }

        public static void ShowNotification(string message)
        {
            API.SetNotificationTextEntry("STRING");
            AddLongText(message);
            API.DrawNotification(false, true);
        }

        public static void ShowHelp(string message, int durationMs)
        {
            API.BeginTextCommandDisplayHelp("STRING");
            AddLongText(message);
            API.EndTextCommandDisplayHelp(0, false, false, durationMs);
        }

        private static void AddLongText(string text)
        {
            const int ChunkSize = 64;
            for (var i = 0; i < text.Length; i += ChunkSize)
                API.AddTextComponentSubstringPlayerName(text.Substring(i, Math.Min(ChunkSize, text.Length - i)));
        }
    }
}
