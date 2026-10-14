using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Agency;
using FivePRS.Client.App;
using FivePRS.Client.Arrest;
using FivePRS.Client.Callouts;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Logging;
using FivePRS.Core.Models;
using FivePRS.Core.Text;
using Newtonsoft.Json;

namespace FivePRS.Client
{
    public class ClientBrain : BaseScript
    {
        private static readonly Logger Log = Logger.For("Client");

        public static PlayerData LocalPlayerData { get; private set; } = new();

        public ClientBrain()
        {
            Logger.DebugEnabled = string.Equals(API.GetConvar("fiveprs_debug", "false"), "true", StringComparison.OrdinalIgnoreCase);
            Logger.ClearSinks();
            Logger.AddSink(new ConsoleSink(message => Debug.WriteLine(message), "Add setr fiveprs_debug true to server.cfg for full error details."));

            var res = API.GetCurrentResourceName();
            ConfigManager.LoadAll(path => API.LoadResourceFile(res, path));

            EventHandlers[EventNames.ClientReceivePlayerData] += new Action<string>(OnReceivePlayerData);
            EventHandlers[EventNames.ClientDutyStatusChanged] += new Action<bool, int>(OnDutyStatusChanged);
            EventHandlers[EventNames.ClientCalloutOffered]    += new Action<string>(OnCalloutOffered);
            EventHandlers[EventNames.ClientRankedUp]          += new Action<int>(OnRankedUp);
            EventHandlers[EventNames.ClientEndCallout]        += new Action(() => CalloutDispatcher.EndCalloutPressed = true);
            EventHandlers["onClientResourceStop"]             += new Action<string>(OnResourceStop);

            API.RegisterCommand(KeyCommands.Accept, new Action<int, List<object>, string>((_, __, ___) =>
            {
                CalloutDispatcher.AcceptPressed = true;
            }), false);
            API.RegisterCommand(KeyCommands.Decline, new Action<int, List<object>, string>((_, __, ___) =>
            {
                CalloutDispatcher.DeclinePressed = true;
            }), false);
            API.RegisterCommand(KeyCommands.EndCall, new Action<int, List<object>, string>((_, __, ___) =>
            {
                CalloutDispatcher.EndCalloutPressed = true;
            }), false);

            API.RegisterCommand(KeyCommands.Cuff, new Action<int, List<object>, string>(async (_, __, ___) =>
            {
                await ArrestManager.TryCuffNearestAsync();
            }), false);
            API.RegisterCommand(KeyCommands.Uncuff, new Action<int, List<object>, string>((_, __, ___) =>
            {
                ArrestManager.Uncuff();
            }), false);
            API.RegisterCommand(KeyCommands.Escort, new Action<int, List<object>, string>(async (_, __, ___) =>
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
                if (!DutyPanel.IsLoaded)
                {
                    ShowNotification("~r~Your profile hasn't loaded yet. Try again in a moment.");
                    return;
                }

                var d = LocalPlayerData;
                var agency = ConfigManager.Territories.FindAgency(d.Agency)?.Name ?? d.Department.ToString();
                ShowNotification(
                    $"~b~{d.Name}~w~ | {agency} | {d.RankName()}~n~~g~" +
                    (d.Ladder().IsTop(d.Rank) ? "Top rank" : $"XP: {d.XP} / {d.Ladder().XpToNext(d.Rank)}"));
            }), false);
            API.RegisterCommand("er_help", new Action<int, List<object>, string>((_, __, ___) =>
            {
                ShowNotification(
                    "~y~FivePRS Commands~w~~n~" +
                    "~b~/fiveprs~w~ — Open the FivePRS menu (duty, dispatch and more)~n~" +
                    "~b~/er_profile~w~ — View rank and XP~n~" +
                    "~b~/er_accept_call~w~ — Accept incoming callout~n~" +
                    "~b~/er_decline~w~ — Decline incoming callout~n~" +
                    "~b~/er_end_callout~w~ — End active callout~n~" +
                    "~b~/er_calls~w~ — List active calls~n~" +
                    "~b~/er_attach [id]~w~ — Respond as backup~n~" +
                    "~b~/backup [2|3] [npc|players]~w~ — Request backup; /backup cancel to stand down~n~" +
                    "~b~/er_status [available|busy]~w~ — Set unit status~n~" +
                    "~b~/er_aicallouts~w~ — Turn AI callouts on or off~n~" +
                    "~b~/er_cuff~w~ — Cuff nearest suspect~n~" +
                    "~b~/er_uncuff~w~ — Release cuffed suspect~n~" +
                    "~b~/er_escort~w~ — Place suspect in your vehicle~n~" +
                    "~b~/fprs_coords [armory|locker|garage|parking]~w~ — Copy your position for stations.json");
            }), false);

            API.RegisterKeyMapping(KeyCommands.Accept,  "FivePRS: Accept callout",            "keyboard", "Y");
            API.RegisterKeyMapping(KeyCommands.Decline, "FivePRS: Decline callout",           "keyboard", "N");
            API.RegisterKeyMapping(KeyCommands.EndCall, "FivePRS: End active callout",        "keyboard", "END");
            API.RegisterKeyMapping(KeyCommands.Cuff,    "FivePRS: Cuff nearest suspect",      "keyboard", "G");
            API.RegisterKeyMapping(KeyCommands.Uncuff,  "FivePRS: Release cuffed suspect",    "keyboard", "");
            API.RegisterKeyMapping(KeyCommands.Escort,  "FivePRS: Escort suspect to vehicle", "keyboard", "H");
            API.RegisterKeyMapping("er_profile",     "FivePRS: View rank and XP",            "keyboard", "F6");
            API.RegisterKeyMapping("er_help",        "FivePRS: Show command list",           "keyboard", "");

            Exports.Add("getState", new Func<IDictionary<string, object>>(PublicState.Build));

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
                Log.Debug($"Profile loaded: {data.Name}, {data.RankName()}, {data.Department}.");

                if (!data.IsOnDuty)
                    ShowHelp($"~y~Welcome to FivePRS!~w~ Press {Binds.Menu} to open the FivePRS menu.", 10_000);
            }
            catch (Exception ex)
            {
                Log.Error("Could not read player data", ex);
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
            ShowNotification($"~g~Promoted!~w~ You are now {LocalPlayerData.RankName()}.");
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
                Log.Warn("Discarded a malformed callout payload", ex);
            }
        }

        private static async void CreateNotificationLogo(string resource, string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            _logoDui = API.CreateDui($"nui://{resource}/nui/dui-image.html#{url}", NotificationLogoSize, NotificationLogoSize);

            var until = API.GetGameTimer() + DuiTimeoutMs;
            while (!API.IsDuiAvailable(_logoDui))
            {
                if (API.GetGameTimer() > until)
                {
                    Log.Warn($"The notification logo didn't load from {url}.");
                    return;
                }

                await Delay(100);
            }

            var name = $"fiveprs_logo_{API.GetGameTimer()}";
            var txd = API.CreateRuntimeTxd(name);
            API.CreateRuntimeTextureFromDuiHandle(txd, name, API.GetDuiHandle(_logoDui));
            _logoTxd = name;
            Log.Debug("Notification logo ready.");
        }

        private void OnResourceStop(string resourceName)
        {
            if (resourceName != API.GetCurrentResourceName() || _logoDui == 0) return;

            API.DestroyDui(_logoDui);
            _logoDui = 0;
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
            API.SetNotificationMessage(_logoTxd, _logoTxd, false, 0, NotificationSender, subject);
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
        private const string NotificationSender = "~b~FivePRS";
        private const int NotificationLogoSize = 128;
        private const int DuiTimeoutMs = 15_000;
        private const string SubjectSeparator = " | ";
        private const int MinHelpDurationMs = 100;

        private static long _logoDui;
        private static string _logoTxd = string.Empty;
        private static string? _helpText;
        private static int _helpUntil;

        private static void AddLongText(string text)
        {
            foreach (var part in GameText.SplitComponents(text))
                API.AddTextComponentSubstringPlayerName(part);
        }
    }
}
