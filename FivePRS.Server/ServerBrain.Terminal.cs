using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private void RegisterTerminalEvents()
        {
            EventHandlers[EventNames.ServerSetWallpaper] += new Action<Player, string>(OnSetWallpaper);
            EventHandlers[EventNames.ServerSaveSettings] += new Action<Player, string>(OnSaveSettings);
        }

        private async void OnSetWallpaper([FromSource] Player player, string value)
        {
            if (!Allow(player, "civilian", l => l.Civilian)) return;

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
                await SendPreferencesAsync(player, license);
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not save {player.Name}'s wallpaper", ex);
            }
        }

        private async void OnSaveSettings([FromSource] Player player, string json)
        {
            if (!Allow(player, "civilian", l => l.Civilian)) return;

            var license = GetLicense(player);
            if (license is null || !_db.IsReady) return;

            try
            {
                await _db.Preferences.SetSettingsAsync(license, TerminalPreferences.Parse(json).ToJson());
                await SendPreferencesAsync(player, license);
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not save {player.Name}'s terminal settings", ex);
            }
        }

        private async Task SendPreferencesAsync(Player player, string license)
        {
            if (!_db.IsReady) return;

            var wallpaper = await _db.Preferences.GetWallpaperAsync(license);
            var settings  = TerminalPreferences.Parse(await _db.Preferences.GetSettingsAsync(license));
            TriggerClientEvent(player, EventNames.ClientPreferences, wallpaper, settings.ToJson());
        }
    }
}
