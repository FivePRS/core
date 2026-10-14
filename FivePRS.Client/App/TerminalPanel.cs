using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;

namespace FivePRS.Client.App
{
    internal static class TerminalPanel
    {
        public static string Wallpaper { get; private set; } = string.Empty;

        public static TerminalPreferences Preferences { get; private set; } = new();

        public static void SetPreferences(string wallpaper, string? settings)
        {
            Wallpaper   = wallpaper ?? string.Empty;
            Preferences = TerminalPreferences.Parse(settings);
        }

        public static void ChooseWallpaper(string value) =>
            ClientEvents.TriggerServer(EventNames.ServerSetWallpaper, value);

        public static void SavePreferences(TerminalPreferences preferences) =>
            ClientEvents.TriggerServer(EventNames.ServerSaveSettings, TerminalPreferences.Sanitize(preferences).ToJson());

        public static object BuildView() => new
        {
            Wallpaper   = ConfigManager.Settings.Terminal.ResolveWallpaperUrl(Wallpaper),
            Preferences = Preferences,
        };
    }
}
