using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Config;
using FivePRS.Core.Events;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private void RegisterTerminalEvents()
        {
            EventHandlers[EventNames.ServerSetWallpaper] += new Action<Player, string>(OnSetWallpaper);
        }

        private async void OnSetWallpaper([FromSource] Player player, string value)
        {
            var license = GetLicense(player);
            if (license is null || !_db.IsReady) return;

            if (!ConfigManager.Settings.Terminal.TryNormalizeWallpaper(value, out var wallpaper))
            {
                Notify(player, "Terminal | ~r~That wallpaper can't be used.~w~ Use an https image link.");
                return;
            }

            try
            {
                await _db.Preferences.SetWallpaperAsync(license, wallpaper);
                TriggerClientEvent(player, EventNames.ClientPreferences, wallpaper);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Saving the wallpaper failed for {player.Name}: {ex.Message}");
            }
        }

        private async Task SendPreferencesAsync(Player player, string license)
        {
            if (!_db.IsReady) return;
            TriggerClientEvent(player, EventNames.ClientPreferences, await _db.Preferences.GetWallpaperAsync(license));
        }
    }
}
