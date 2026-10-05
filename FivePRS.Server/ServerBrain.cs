using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public class ServerBrain : BaseScript
    {
        private static readonly TimeSpan MinCalloutInterval = TimeSpan.FromSeconds(30);

        private readonly ConcurrentDictionary<string, PlayerData> _cache = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastCalloutReward = new();
        private readonly DatabaseManager _db = new();

        public ServerBrain()
        {
            EventHandlers["playerConnecting"] += new Action<Player, string, dynamic, dynamic>(OnPlayerConnecting);
            EventHandlers["playerDropped"]    += new Action<Player, string>(OnPlayerDropped);

            EventHandlers[EventNames.ServerPlayerConnected]  += new Action<Player>(OnPlayerReady);
            EventHandlers[EventNames.ServerToggleDuty]       += new Action<Player>(OnToggleDuty);
            EventHandlers[EventNames.ServerSetDepartment]    += new Action<Player, int>(OnSetDepartment);
            EventHandlers[EventNames.ServerCalloutCompleted] += new Action<Player, string, int>(OnCalloutCompleted);

            _ = InitDbAsync();
        }

        private async Task InitDbAsync()
        {
            try
            {
                var dbTypeRaw  = API.GetConvar("fiveprs_db_type", "sqlite").ToLowerInvariant();
                var connString = API.GetConvar("fiveprs_db_connection", "");

                var dbType = dbTypeRaw == "mysql" ? DatabaseType.MySQL : DatabaseType.SQLite;
                await _db.InitializeAsync(dbType, string.IsNullOrEmpty(connString) ? null : connString);

                Debug.WriteLine("[FivePRS] ServerBrain online.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] FATAL: DB init failed: {ex}");
            }
        }

        private async void OnPlayerConnecting(
            [FromSource] Player player,
            string playerName,
            dynamic setKickReason,
            dynamic deferrals)
        {
            deferrals.defer();
            await Delay(0);

            if (!_db.IsReady)
            {
                deferrals.done("FivePRS database is not ready. Please try again in a moment.");
                return;
            }

            var license = GetLicense(player);
            if (license is null)
            {
                deferrals.done("A valid FiveM license identifier is required.");
                return;
            }

            deferrals.update($"Loading your FivePRS profile, {playerName}...");

            try
            {
                await LoadProfileAsync(license, playerName);
                deferrals.done();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error loading {playerName}: {ex.Message}");
                deferrals.done("Error loading your profile. Please try again.");
            }
        }

        private void OnPlayerDropped([FromSource] Player player, string reason)
        {
            var license = GetLicense(player);
            if (license is null) return;

            _lastCalloutReward.TryRemove(license, out _);
            if (_cache.TryRemove(license, out var data) && data.IsOnDuty)
                _ = _db.UpdateDutyStatusAsync(license, false);
        }

        private async void OnPlayerReady([FromSource] Player player)
        {
            var license = GetLicense(player);
            if (license is null) return;

            try
            {
                if (!_cache.TryGetValue(license, out var data))
                {
                    if (!_db.IsReady) return;
                    data = await LoadProfileAsync(license, player.Name);
                }

                SendPlayerData(player, data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error sending profile to {player.Name}: {ex.Message}");
            }
        }

        private async void OnToggleDuty([FromSource] Player player)
        {
            if (!TryGetCached(player, out var license, out var data)) return;
            if (!data.IsOnDuty && data.Department == Department.None) return;

            data.IsOnDuty = !data.IsOnDuty;

            try
            {
                await _db.UpdateDutyStatusAsync(license, data.IsOnDuty);

                TriggerClientEvent(player, EventNames.ClientDutyStatusChanged,
                    data.IsOnDuty, (int)data.Department);

                Debug.WriteLine($"[FivePRS] {data.Name} is now {(data.IsOnDuty ? "ON" : "OFF")} duty.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error toggling duty for {player.Name}: {ex.Message}");
            }
        }

        private async void OnSetDepartment([FromSource] Player player, int departmentId)
        {
            if (!Enum.IsDefined(typeof(Department), departmentId) || departmentId == (int)Department.None) return;
            if (!TryGetCached(player, out _, out var data)) return;

            try
            {
                if (data.IsOnDuty)
                {
                    data.IsOnDuty = false;
                    TriggerClientEvent(player, EventNames.ClientDutyStatusChanged, false, (int)data.Department);
                }

                data.Department = (Department)departmentId;
                await _db.SavePlayerAsync(data);
                SendPlayerData(player, data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error setting department for {player.Name}: {ex.Message}");
            }
        }

        private async void OnCalloutCompleted([FromSource] Player player, string calloutId, int xpClaim)
        {
            if (!TryGetCached(player, out var license, out var data) || !data.IsOnDuty) return;

            var now = DateTime.UtcNow;
            if (_lastCalloutReward.TryGetValue(license, out var last) && now - last < MinCalloutInterval)
            {
                Debug.WriteLine($"[FivePRS] Ignored callout reward from {player.Name}: too soon after the last one.");
                return;
            }
            _lastCalloutReward[license] = now;

            var awardedXP = (int)Math.Min(Math.Max(xpClaim * ReadXpMultiplier(), 0), ReadMaxXp());

            try
            {
                var rankedUp = data.AddXP(awardedXP);
                await _db.SavePlayerAsync(data);

                SendPlayerData(player, data);
                if (rankedUp)
                {
                    TriggerClientEvent(player, EventNames.ClientRankedUp, data.Rank);
                    Debug.WriteLine($"[FivePRS] {data.Name} ranked up to Rank {data.Rank}.");
                }

                Debug.WriteLine($"[FivePRS] +{awardedXP} XP to {player.Name} (callout: {calloutId})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error completing callout {calloutId}: {ex.Message}");
            }
        }

        private async Task<PlayerData> LoadProfileAsync(string license, string playerName)
        {
            var data = await _db.GetPlayerAsync(license);
            if (data is null)
            {
                data = new PlayerData { License = license };
                Debug.WriteLine($"[FivePRS] New player registered: {playerName} ({license})");
            }

            data.Name     = playerName;
            data.IsOnDuty = false;
            await _db.SavePlayerAsync(data);

            _cache[license] = data;
            return data;
        }

        private bool TryGetCached(Player player, out string license, out PlayerData data)
        {
            license = GetLicense(player) ?? string.Empty;
            data    = null!;
            return license.Length > 0 && _cache.TryGetValue(license, out data!);
        }

        private void SendPlayerData(Player player, PlayerData data) =>
            TriggerClientEvent(player, EventNames.ClientReceivePlayerData, JsonConvert.SerializeObject(data));

        private static string? GetLicense(Player player)
        {
            var license = player.Identifiers["license"];
            return string.IsNullOrEmpty(license) ? null : license;
        }

        private static int ReadMaxXp() =>
            int.TryParse(API.GetConvar("fiveprs_max_xp", "500"), out var maxXp) && maxXp > 0 ? maxXp : 500;

        private static float ReadXpMultiplier() =>
            float.TryParse(API.GetConvar("fiveprs_xp_multiplier", "1.0"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var multiplier) && multiplier > 0 ? multiplier : 1.0f;
    }
}
