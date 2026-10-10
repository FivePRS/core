using FivePRS.Core.Config;
using FivePRS.Core.Events;

namespace FivePRS.Client.App
{
    internal static class TerminalPanel
    {
        public static string Wallpaper { get; private set; } = string.Empty;

        public static void SetWallpaper(string wallpaper) => Wallpaper = wallpaper ?? string.Empty;

        public static void ChooseWallpaper(string value) =>
            ClientEvents.TriggerServer(EventNames.ServerSetWallpaper, value);

        public static object BuildView() => new
        {
            Wallpaper = ConfigManager.Settings.Terminal.ResolveWallpaperUrl(Wallpaper),
        };
    }
}
