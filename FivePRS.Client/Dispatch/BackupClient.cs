using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Menu;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Core.Text;

namespace FivePRS.Client.Dispatch
{
    public enum BackupMode
    {
        Any,
        Npc,
        Players,
    }

    public class BackupClient : BaseScript
    {
        private const int TickMs            = 500;
        private const int NoUnitsWaitMs     = 5_000;
        private const int ConfirmTimeoutMs  = 10_000;
        private const int AlertBlipMs       = 90_000;

        private sealed class AlertBlip
        {
            public AlertBlip(string callId, Blip blip, int expiresAt)
            {
                CallId    = callId;
                Blip      = blip;
                ExpiresAt = expiresAt;
            }

            public string CallId    { get; }
            public Blip   Blip      { get; }
            public int    ExpiresAt { get; }
        }

        private static readonly List<NpcBackupUnit> Leaving = new();
        private static readonly List<AlertBlip>     Alerts  = new();

        private static NpcBackupUnit? _npc;
        private static bool           _spawning;
        private static bool           _npcOnly;
        private static BackupMode     _mode;
        private static int            _code = 3;
        private static int?           _requestedAt;

        public BackupClient()
        {
            EventHandlers[EventNames.ClientBackupRequested] += new Action<string, string, int, float, float, float>(OnBackupRequested);
            EventHandlers[EventNames.LocalDutyChanged]      += new Action<bool, int>((onDuty, _) => { if (!onDuty) Reset(false); });
            EventHandlers["onClientResourceStop"]           += new Action<string>(name =>
            {
                if (name == API.GetCurrentResourceName()) Reset(true);
            });

            API.RegisterCommand("backup", new Action<int, List<object>, string>((_, args, __) => OnCommand(args)), false);
            API.RegisterCommand(KeyCommands.Backup, new Action<int, List<object>, string>((_, __, ___) => Toggle()), false);
            API.RegisterKeyMapping(KeyCommands.Backup, "FivePRS: Request or cancel backup (Code 3)", "keyboard", "");

            Tick += OnTickAsync;
        }

        public static bool IsActive => _npcOnly || _npc is not null || OwnRequest() is not null;

        public static void Request(BackupMode mode, int code)
        {
            var settings = ConfigManager.Settings.Backup;
            if (!settings.Enabled)
            {
                ClientBrain.ShowNotification("~r~Backup requests are turned off on this server.");
                return;
            }

            if (DispatchClient.LocalUnit is null)
            {
                ClientBrain.ShowNotification("~r~You need to be on duty to request backup.");
                return;
            }

            if (mode != BackupMode.Players && !settings.NpcEnabled)
            {
                if (mode == BackupMode.Npc)
                {
                    ClientBrain.ShowNotification("~r~NPC backup is turned off on this server.");
                    return;
                }
                mode = BackupMode.Players;
            }

            _mode = mode;
            _code = code == 2 ? 2 : 3;

            if (mode == BackupMode.Npc)
            {
                if (_npc is not null || _spawning)
                {
                    ClientBrain.ShowNotification("~b~Backup~w~ | An NPC unit is already on the way.");
                    return;
                }

                _npcOnly = true;
                ClientBrain.ShowNotification($"~b~Backup~w~ | NPC unit responding {CodeLabel(_code)}.");
                _ = SpawnNpcAsync();
                return;
            }

            _npcOnly     = false;
            _requestedAt = API.GetGameTimer();
            ClientEvents.TriggerServer(EventNames.ServerRequestBackup, _code);
        }

        public static void Cancel()
        {
            if (OwnRequest() is not null) ClientEvents.TriggerServer(EventNames.ServerCancelBackup);
            else if (_npcOnly || _npc is not null) ClientBrain.ShowNotification("~b~Backup~w~ | Backup cancelled.");

            _npcOnly     = false;
            _requestedAt = null;
            DismissNpc();
        }

        public static MenuItem MenuEntry() => IsActive
            ? new MenuItem
            {
                Label  = "Cancel backup",
                Detail = _npc is not null ? "NPC unit on scene or en route" : CodeLabel(_code),
                Select = () =>
                {
                    Cancel();
                    CompactMenu.Close();
                    return Task.FromResult(0);
                },
            }
            : new MenuItem { Label = "Request backup", Submenu = ModePage };

        private static MenuPage ModePage()
        {
            var npc  = ConfigManager.Settings.Backup.NpcEnabled;
            var page = new MenuPage { Title = "Request backup", Subtitle = "Who should respond?" };

            page.Items.Add(new MenuItem
            {
                Label   = "Any unit",
                Detail  = npc ? "Players, then NPC if nobody comes" : "On-duty players",
                Submenu = () => CodePage(BackupMode.Any),
            });
            page.Items.Add(new MenuItem
            {
                Label    = "NPC unit",
                Detail   = npc ? "Sent straight away" : "Turned off",
                Disabled = !npc,
                Submenu  = () => CodePage(BackupMode.Npc),
            });
            if (npc)
            {
                page.Items.Add(new MenuItem
                {
                    Label   = "Players only",
                    Detail  = "No NPC unit",
                    Submenu = () => CodePage(BackupMode.Players),
                });
            }

            return page;
        }

        private static MenuPage CodePage(BackupMode mode)
        {
            var page = new MenuPage { Title = "Request backup", Subtitle = mode == BackupMode.Npc ? "NPC unit" : mode == BackupMode.Players ? "Players only" : "Any unit" };

            page.Items.Add(CodeItem(mode, 3, "Lights and sirens"));
            page.Items.Add(CodeItem(mode, 2, "Urgent, no lights"));
            return page;
        }

        private static MenuItem CodeItem(BackupMode mode, int code, string detail) => new()
        {
            Label  = $"Code {code}",
            Detail = detail,
            Select = () =>
            {
                Request(mode, code);
                CompactMenu.Close();
                return Task.FromResult(0);
            },
        };

        private static void Toggle()
        {
            if (IsActive) Cancel();
            else Request(BackupMode.Any, 3);
        }

        private static void OnCommand(List<object> args)
        {
            var words = args.Select(arg => arg?.ToString()?.ToLowerInvariant() ?? string.Empty).ToList();
            if (words.Contains("cancel"))
            {
                Cancel();
                return;
            }

            var mode = words.Contains("npc") ? BackupMode.Npc : words.Contains("players") ? BackupMode.Players : BackupMode.Any;
            Request(mode, words.Contains("2") ? 2 : 3);
        }

        private static CallInfo? OwnRequest()
        {
            var unit = DispatchClient.LocalUnit;
            if (unit?.CallId is null) return null;
            return DispatchClient.Snapshot.Calls.FirstOrDefault(call => call.Id == unit.CallId && call.BackupCode > 0);
        }

        private async Task OnTickAsync()
        {
            await Delay(TickMs);

            var now    = API.GetGameTimer();
            var player = Game.PlayerPed;

            ExpireAlerts(now);
            UpdateLeaving(player);

            var own = OwnRequest();
            if (own is not null) _code = own.BackupCode;

            if (!_npcOnly && _requestedAt is not null && own is null && now - _requestedAt.Value > ConfirmTimeoutMs)
            {
                _requestedAt = null;
                DismissNpc();
            }

            if (_requestedAt is not null && own is not null && _mode == BackupMode.Any && _npc is null && !_spawning &&
                ConfigManager.Settings.Backup.NpcEnabled && !PlayerResponded(own) && now - _requestedAt.Value >= NpcWaitMs())
            {
                ClientBrain.ShowNotification(AnyOtherUnits()
                    ? "~b~Backup~w~ | No units have responded. An NPC unit is on the way."
                    : "~b~Backup~w~ | No other units are on duty. An NPC unit is on the way.");
                _ = SpawnNpcAsync();
            }

            if (_npc is null) return;

            if (!_npcOnly && own is null && _requestedAt is null)
            {
                DismissNpc();
                return;
            }

            _npc.Update(player);
            if (_npc.Released)
            {
                _npc     = null;
                _npcOnly = false;
            }
        }

        private static bool PlayerResponded(CallInfo call) =>
            call.Units.Any(id => id != Game.Player.ServerId);

        private static bool AnyOtherUnits()
        {
            var me = DispatchClient.LocalUnit;
            return DispatchClient.Snapshot.Units.Any(unit =>
                unit.ServerId != Game.Player.ServerId &&
                (unit.Department == Department.Police || (me is not null && unit.Department == me.Department)));
        }

        private static int NpcWaitMs() =>
            AnyOtherUnits() ? Math.Max(0, ConfigManager.Settings.Backup.NpcWaitSeconds) * 1000 : NoUnitsWaitMs;

        private static async Task SpawnNpcAsync()
        {
            _spawning = true;
            try
            {
                var unit = await NpcBackupUnit.SpawnAsync(Game.PlayerPed.Position, _code == 3, ConfigManager.Settings.Backup);
                if (unit is null)
                {
                    ClientBrain.ShowNotification("~r~No NPC unit could reach you. Try again somewhere with a nearby road.");
                    _npcOnly = false;
                    return;
                }

                if (!_npcOnly && _requestedAt is null)
                {
                    unit.Delete();
                    return;
                }

                _npc = unit;
            }
            finally
            {
                _spawning = false;
            }
        }

        private static void DismissNpc()
        {
            if (_npc is null) return;

            _npc.Dismiss();
            Leaving.Add(_npc);
            _npc = null;
        }

        private static void UpdateLeaving(Ped player)
        {
            foreach (var unit in Leaving.ToList())
            {
                unit.Update(player);
                if (unit.Released) Leaving.Remove(unit);
            }
        }

        private void OnBackupRequested(string callId, string callsign, int code, float x, float y, float z)
        {
            var blip = World.CreateBlip(new Vector3(x, y, z));
            blip.Color      = code == 3 ? BlipColor.Red : BlipColor.Yellow;
            blip.IsFlashing = true;
            blip.Name       = $"Backup · {callsign} · #{callId}";
            Alerts.Add(new AlertBlip(callId, blip, API.GetGameTimer() + AlertBlipMs));
            API.PlaySoundFrontend(-1, "TIMER_STOP", "HUD_MINI_GAME_SOUNDSET", true);
        }

        private static void ExpireAlerts(int now)
        {
            var callIds = new HashSet<string>(DispatchClient.Snapshot.Calls.Where(c => c.BackupCode > 0).Select(c => c.Id));
            foreach (var alert in Alerts.ToList())
            {
                var recent = alert.ExpiresAt - now > AlertBlipMs - ConfirmTimeoutMs;
                if (now < alert.ExpiresAt && (recent || callIds.Contains(alert.CallId)) && alert.Blip.Exists()) continue;

                if (alert.Blip.Exists()) alert.Blip.Delete();
                Alerts.Remove(alert);
            }
        }

        private static void Reset(bool delete)
        {
            _npcOnly     = false;
            _requestedAt = null;

            if (delete)
            {
                _npc?.Delete();
                _npc = null;
                foreach (var unit in Leaving) unit.Delete();
                Leaving.Clear();
            }
            else
            {
                DismissNpc();
            }

            foreach (var alert in Alerts.Where(a => a.Blip.Exists())) alert.Blip.Delete();
            Alerts.Clear();
        }

        private static string CodeLabel(int code) => code == 3 ? "~r~Code 3~w~" : "~y~Code 2~w~";
    }
}
