using System.Collections.Generic;
using FivePRS.Core.Config;
using FivePRS.Core.Events;

namespace FivePRS.Client.App
{
    internal static class TerminalPanel
    {
        private static string _wallpaper = string.Empty;

        public static void SetWallpaper(string wallpaper) => _wallpaper = wallpaper ?? string.Empty;

        public static void ChooseWallpaper(IDictionary<string, object> data) =>
            ClientEvents.TriggerServer(EventNames.ServerSetWallpaper, NuiData.GetString(data, "value"));

        public static object BuildView()
        {
            var settings = ConfigManager.Settings.Terminal;
            return new
            {
                Wallpaper   = settings.ResolveWallpaperUrl(_wallpaper),
                Selected    = _wallpaper,
                Default     = settings.DefaultWallpaper,
                AllowCustom = settings.AllowCustomWallpapers,
                Wallpapers  = settings.Wallpapers,
            };
        }
    }
}
