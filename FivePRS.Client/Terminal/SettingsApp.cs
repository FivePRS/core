using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core.Native;
using FivePRS.Client.App;
using FivePRS.Client.Dispatch;
using FivePRS.Core.Config;

namespace FivePRS.Client.Terminal
{
    public class SettingsApp : TerminalApp
    {
        public override string Id => "settings";

        public override string Label => "Settings";

        public override string Icon =>
            "M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z M12 2v3 M12 19v3 M4.9 4.9l2.1 2.1 M17 17l2.1 2.1 M2 12h3 M19 12h3 M4.9 19.1L7 17 M17 7l2.1-2.1";

        public override int Order => 1000;

        public override AppScreen BuildScreen()
        {
            var screen   = new AppScreen();
            var terminal = ConfigManager.Settings.Terminal;
            var unit     = DispatchClient.LocalUnit;

            if (unit is not null)
            {
                screen.Add(AppBlock.Section("Duty"))
                    .Add(AppBlock.Paragraph("AI callouts. Off pauses AI callouts for you; player 911 calls still reach you.", true))
                    .Add(AppBlock.Buttons(
                        Toggle("On", unit.AiCallouts, true),
                        Toggle("Off", !unit.AiCallouts, false)));
            }

            var selected = TerminalPanel.Wallpaper.Length > 0 ? TerminalPanel.Wallpaper : $"id:{terminal.DefaultWallpaper}";
            screen.Add(AppBlock.Section("Wallpaper"))
                .Add(AppBlock.List(terminal.Wallpapers.Select(option => new AppItem
                {
                    Title  = option.Label,
                    Image  = option.Url,
                    Action = "wallpaper",
                    Data   = new Dictionary<string, object> { ["value"] = $"id:{option.Id}" },
                    Active = selected == $"id:{option.Id}",
                }), grid: true));

            if (terminal.AllowCustomWallpapers)
            {
                screen.Add(AppBlock.Form("wallpaperUrl", "Use image", new AppField
                {
                    Name        = "url",
                    Label       = "Custom image",
                    Placeholder = "https://example.com/wallpaper.png",
                    Value       = selected.StartsWith("url:") ? selected.Substring(4) : null,
                    Type        = "url",
                }));
            }

            var version = API.GetResourceMetadata(API.GetCurrentResourceName(), "version", 0);
            screen.Add(AppBlock.Section("About"))
                .Add(AppBlock.Paragraph($"FivePRS {version}"))
                .Add(AppBlock.Paragraph(version.Contains("-")
                    ? "Public test build. Report problems at github.com/FivePRS/core/issues"
                    : "fiveprs.org", true));

            return screen;
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
            }
        }

        private static AppItem Toggle(string label, bool active, bool enabled) => new()
        {
            Title  = label,
            Action = "aiCallouts",
            Data   = new Dictionary<string, object> { ["enabled"] = enabled },
            Active = active,
        };
    }
}
