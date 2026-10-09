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
using FivePRS.Core.Text;
using Newtonsoft.Json;

namespace FivePRS.Client
{
    public class ClientBrain : BaseScript
    {
        public static PlayerData LocalPlayerData { get; private set; } = new();

        public ClientBrain()
        {
            ConfigManager.Log = message => Debug.WriteLine(message);

            var res = API.GetCurrentResourceName();
            ConfigManager.LoadSettings(      API.LoadResourceFile(res, "config/settings.json"));
            ConfigManager.LoadPoliceVehicles( API.LoadResourceFile(res, "config/police_vehicles.json"));
            ConfigManager.LoadPoliceLoadouts( API.LoadResourceFile(res, "config/police_loadouts.json"));
            ConfigManager.LoadJurisdictions(  API.LoadResourceFile(res, "config/jurisdictions.json"));

            EventHandlers[EventNames.ClientReceivePlayerData] += new Action<string>(OnReceivePlayerData);
            EventHandlers[EventNames.ClientDutyStatusChanged] += new Action<bool, int>(OnDutyStatusChanged);
            EventHandlers[EventNames.ClientCalloutOffered]    += new Action<string>(OnCalloutOffered);
            EventHandlers[EventNames.ClientRankedUp]          += new Action<int>(OnRankedUp);
            EventHandlers[EventNames.ClientEndCallout]        += new Action(() => CalloutDispatcher.EndCalloutPressed = true);

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
                var agency = ConfigManager.Territories.FindAgency(d.Agency)?.Name ?? d.Department.ToString();
                ShowNotification(
                    $"~b~{d.Name}~w~ | {agency} | Rank {d.Rank}~n~~g~XP: {d.XP} / {d.XPToNextRank}");
            }), false);
            API.RegisterCommand("er_help", new Action<int, List<object>, string>((_, __, ___) =>
            {
                ShowNotification(
                    "~y~FivePRS Commands~w~~n~" +
                    "~b~/fiveprs~w~ — Open the FivePRS menu (duty, dispatch and more)~n~" +
                    "~b~/er_profile~w~ — View rank and XP~n~" +
                    "~b~/er_accept~w~ — Accept incoming callout~n~" +
                    "~b~/er_decline~w~ — Decline incoming callout~n~" +
                    "~b~/er_end_callout~w~ — End active callout~n~" +
                    "~b~/er_calls~w~ — List active calls~n~" +
                    "~b~/er_attach [id]~w~ — Respond as backup~n~" +
                    "~b~/er_status [available|busy]~w~ — Set unit status~n~" +
                    "~b~/er_cuff~w~ — Cuff nearest suspect~n~" +
                    "~b~/er_uncuff~w~ — Release cuffed suspect~n~" +
                    "~b~/er_escort~w~ — Place suspect in your vehicle");
            }), false);

            API.RegisterKeyMapping("er_accept",      "FivePRS: Accept callout",              "keyboard", "Y");
            API.RegisterKeyMapping("er_decline",     "FivePRS: Decline callout",             "keyboard", "N");
            API.RegisterKeyMapping("er_end_callout", "FivePRS: End active callout",          "keyboard", "END");
            API.RegisterKeyMapping("er_cuff",        "FivePRS: Cuff nearest suspect",        "keyboard", "G");
            API.RegisterKeyMapping("er_uncuff",      "FivePRS: Release cuffed suspect",      "keyboard", "");
            API.RegisterKeyMapping("er_escort",      "FivePRS: Escort suspect to vehicle",   "keyboard", "H");
            API.RegisterKeyMapping("er_profile",     "FivePRS: View rank and XP",            "keyboard", "F6");
            API.RegisterKeyMapping("er_help",        "FivePRS: Show command list",           "keyboard", "");

            Tick += WaitForSpawnTick;
            Tick += DrawHelpTick;

            CreateNotificationLogo(res, ConfigManager.Settings.Branding.NotificationLogo);
        }

        private async Task WaitForSpawnTick()
        {
            if (Game.PlayerPed.Exists() && Game.PlayerPed.Handle != 0)
            {
                Tick -= WaitForSpawnTick;
                ClientEvents.TriggerServer(EventNames.ServerPlayerConnected);
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
                Debug.WriteLine($"[FivePRS] Profile loaded: {data.Name} | Rank {data.Rank} | {data.Department}");

                if (!data.IsOnDuty)
                    ShowHelp($"~y~Welcome to FivePRS!~w~ Press {Binds.Menu} to open the FivePRS menu.", 10_000);
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

            ClientEvents.TriggerLocal(EventNames.LocalDutyChanged, isOnDuty, departmentId);
        }

        private void OnRankedUp(int newRank)
        {
            LocalPlayerData.Rank = newRank;
            ShowNotification($"~g~RANK UP!~w~ You are now Rank {newRank}. Keep it up!");
        }

        private void OnCalloutOffered(string calloutJson)
        {
            try
            {
                _ = JsonConvert.DeserializeObject<CalloutData>(calloutJson)
                    ?? throw new InvalidOperationException("Callout JSON was null after deserialisation.");

                ClientEvents.TriggerLocal(EventNames.LocalCalloutReceived, calloutJson);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Malformed callout payload discarded: {ex.Message}");
            }
        }

        private static void CreateNotificationLogo(string resource, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            var txd = API.CreateRuntimeTxd(NotificationTxd);
            var dui = API.CreateDui($"https://cfx-nui-{resource}/nui/dui-image.html#{url}", NotificationLogoSize, NotificationLogoSize);
            API.CreateRuntimeTextureFromDuiHandle(txd, NotificationTexture, API.GetDuiHandle(dui));
        }

        public static void ShowNotification(string message, string? subject = null)
        {
            var body = message;
            if (subject is null)
            {
                var split = message.IndexOf(SubjectSeparator, StringComparison.Ordinal);
                subject = split > 0 ? message.Substring(0, split) : string.Empty;
                if (split > 0) body = message.Substring(split + SubjectSeparator.Length);
            }

            API.SetNotificationTextEntry(LongTextEntry);
            AddLongText(body);
            API.SetNotificationMessage(NotificationTxd, NotificationTexture, false, 0, NotificationSender, subject);
            API.DrawNotification(false, true);
        }

        public static void ShowHelp(string message, int durationMs)
        {
            _helpText  = message;
            _helpUntil = API.GetGameTimer() + Math.Max(durationMs, MinHelpDurationMs);
        }

        private Task DrawHelpTick()
        {
            if (_helpText is null) return Delay(50);

            if (API.GetGameTimer() > _helpUntil)
            {
                _helpText = null;
                return Delay(50);
            }

            API.BeginTextCommandDisplayHelp(LongTextEntry);
            AddLongText(_helpText);
            API.EndTextCommandDisplayHelp(0, false, false, -1);
            return Task.FromResult(0);
        }

        private const string LongTextEntry = "CELL_EMAIL_BCON";
        private const string NotificationTxd = "fiveprs_ui";
        private const string NotificationTexture = "logo";
        private const string NotificationSender = "~b~FivePRS";
        private const int NotificationLogoSize = 128;
        private const string SubjectSeparator = " | ";
        private const int MinHelpDurationMs = 100;

        private static string? _helpText;
        private static int _helpUntil;

        private static void AddLongText(string text)
        {
            foreach (var part in GameText.SplitComponents(text))
                API.AddTextComponentSubstringPlayerName(part);
        }
    }
}
