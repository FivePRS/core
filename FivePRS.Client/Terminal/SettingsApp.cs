using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core.Native;
using FivePRS.Client.App;
using FivePRS.Client.Dispatch;
using FivePRS.Core.Config;
using FivePRS.Core.Models;
using FivePRS.Core.Text;

namespace FivePRS.Client.Terminal
{
    public class SettingsApp : TerminalApp
    {
        private static readonly string[][] Bindings =
        {
            new[] { KeyCommands.Menu, "Open the terminal" },
            new[] { KeyCommands.Interact, "Interact" },
            new[] { KeyCommands.Accept, "Accept a call" },
            new[] { KeyCommands.Decline, "Decline a call" },
            new[] { KeyCommands.EndCall, "End your call" },
            new[] { KeyCommands.Backup, "Request backup" },
            new[] { KeyCommands.TrafficStop, "Traffic stop" },
            new[] { KeyCommands.Radar, "Toggle radar" },
            new[] { KeyCommands.RadarLock, "Lock front radar" },
            new[] { KeyCommands.RadarRearLock, "Lock rear radar" },
            new[] { KeyCommands.RadarRemote, "Radar operator menu" },
            new[] { KeyCommands.Lidar, "Toggle lidar gun" },
            new[] { KeyCommands.Cuff, "Cuff" },
            new[] { KeyCommands.Uncuff, "Uncuff" },
            new[] { KeyCommands.Escort, "Escort to vehicle" },
            new[] { KeyCommands.AiCallouts, "Toggle AI callouts" },
        };

        public override string Id => "settings";

        public override string Label => "Settings";

        public override string Color => "#334155";

        public override string Icon =>
            "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M12 2v3 M12 19v3 M4.9 4.9l2.1 2.1 M17 17l2.1 2.1 M2 12h3 M19 12h3 M4.9 19.1L7 17 M17 7l2.1-2.1";

        public override int Order => 1000;

        public override string? Page => "nui/apps/settings/index.html";

        public override object BuildState()
        {
            var terminal = ConfigManager.Settings.Terminal;
            var unit     = DispatchClient.LocalUnit;
            var version  = API.GetResourceMetadata(API.GetCurrentResourceName(), "version", 0);
            var selected = TerminalPanel.Wallpaper.Length > 0 ? TerminalPanel.Wallpaper : $"id:{terminal.DefaultWallpaper}";

            return new
            {
                Wallpapers    = terminal.Wallpapers.Select(option => new { Value = $"id:{option.Id}", option.Label, option.Url }),
                Wallpaper     = selected,
                AllowCustom   = terminal.AllowCustomWallpapers,
                Preferences   = TerminalPanel.Preferences,
                OnDuty        = unit is not null,
                AiCallouts    = unit?.AiCallouts ?? AiCallouts.Enabled,
                Version       = version,
                Nameplate     = ConfigManager.Settings.Branding.Nameplate,
                PreRelease    = version.Contains("-"),
                Bindings      = Bindings.Select(binding => new { Label = binding[1], Key = KeyLabel(binding[0]) }),
            };
        }

        public override void OnAction(string action, IDictionary<string, object> data)
        {
            switch (action)
            {
                case "aiCallouts":
                    AiCallouts.Set(NuiData.GetBool(data, "enabled"));
                    break;

                case "wallpaper":
                    TerminalPanel.ChooseWallpaper(NuiData.GetString(data, "value"));
                    break;

                case "wallpaperUrl":
                    var url = NuiData.GetString(data, "url").Trim();
                    if (url.Length > 0) TerminalPanel.ChooseWallpaper($"url:{url}");
                    break;

                case "preferences":
                    var current = TerminalPanel.Preferences;
                    TerminalPanel.SavePreferences(new TerminalPreferences
                    {
                        TextSize       = NuiData.TryGetString(data, "textSize", out var size) ? size : current.TextSize,
                        Clock24        = data.ContainsKey("clock24") ? NuiData.GetBool(data, "clock24") : current.Clock24,
                        ReduceMotion   = data.ContainsKey("reduceMotion") ? NuiData.GetBool(data, "reduceMotion") : current.ReduceMotion,
                        TerminalVolume = NuiData.TryGetInt(data, "terminalVolume", out var terminalVolume) ? terminalVolume : current.TerminalVolume,
                        DispatchVolume = NuiData.TryGetInt(data, "dispatchVolume", out var dispatchVolume) ? dispatchVolume : current.DispatchVolume,
                        HoldChannel    = data.ContainsKey("holdChannel") ? NuiData.GetBool(data, "holdChannel") : current.HoldChannel,
                    });
                    break;
            }
        }

        private static string KeyLabel(string command)
        {
            var button = API.GetControlInstructionalButton(0, unchecked((int)((uint)API.GetHashKey(command) | 0x80000000)), 1);
            return button is not null && button.StartsWith("t_") ? button.Substring(2) : "Not set";
        }
    }
}
