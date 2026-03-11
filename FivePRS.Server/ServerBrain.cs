using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    /// <summary>
    /// Central server-side controller. Manages the database lifecycle, player session
    /// cache, duty toggling, department selection, and XP rewards. All heavy I/O is
    /// async so the main thread is never blocked.
    /// </summary>
    public class ServerBrain : BaseScript
    {
        private readonly ConcurrentDictionary<string, PlayerData> _cache = new();
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
                Debug.WriteLine($"[FivePRS] FATAL — DB init failed: {ex.Message}");
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

            var license = player.Identifiers["license"];
            if (string.IsNullOrEmpty(license))
            {
                deferrals.done("A valid FiveM license identifier is required.");
                return;
            }

            deferrals.update($"Welcome back, {playerName}! Loading your ERS profile…");

            try
            {
                var data = await _db.GetPlayerAsync(license);

                if (data is null)
                {
                    data = new PlayerData { License = license, Name = playerName };
                    await _db.UpsertPlayerAsync(data);
                    Debug.WriteLine($"[FivePRS] New player registered: {playerName} ({license})");
                }
                else
                {
                    data.Name = playerName;
                    await _db.UpsertPlayerAsync(data);
                }

                data.IsOnDuty = false;
                _cache[license] = data;

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
            var license = player.Identifiers["license"];
            if (!string.IsNullOrEmpty(license) && _cache.TryRemove(license, out var data) && data.IsOnDuty)
                _ = _db.UpdateDutyStatusAsync(license, false);
        }

        private void OnPlayerReady([FromSource] Player player)
        {
            var license = player.Identifiers["license"];
            if (string.IsNullOrEmpty(license) || !_cache.TryGetValue(license, out var data)) return;

            TriggerClientEvent(player, EventNames.ClientReceivePlayerData, JsonConvert.SerializeObject(data));
        }

        private async void OnToggleDuty([FromSource] Player player)
        {
            var license = player.Identifiers["license"];
            if (string.IsNullOrEmpty(license) || !_cache.TryGetValue(license, out var data)) return;

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
            if (!Enum.IsDefined(typeof(Department), departmentId)) return;

            var license = player.Identifiers["license"];
            if (string.IsNullOrEmpty(license) || !_cache.TryGetValue(license, out var data)) return;

            data.Department = (Department)departmentId;

            try
            {
                await _db.UpdateDepartmentAsync(license, data.Department);
                TriggerClientEvent(player, EventNames.ClientReceivePlayerData, JsonConvert.SerializeObject(data));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error setting department for {player.Name}: {ex.Message}");
            }
        }

        private async void OnCalloutCompleted([FromSource] Player player, string calloutId, int xpClaim)
        {
            var license = player.Identifiers["license"];
            if (string.IsNullOrEmpty(license)) return;

            int.TryParse(API.GetConvar("fiveprs_max_xp", "500"), out var maxXp);
            if (maxXp <= 0) maxXp = 500;
            float.TryParse(API.GetConvar("fiveprs_xp_multiplier", "1.0"), out var xpMultiplier);
            if (xpMultiplier <= 0) xpMultiplier = 1.0f;
            var awardedXP = (int)Math.Clamp(xpClaim * xpMultiplier, 0, maxXp);

            try
            {
                await _db.AddXPAsync(license, awardedXP);

                if (_cache.TryGetValue(license, out var data))
                {
                    data.XP += awardedXP;
                    if (data.TryRankUp())
                    {
                        await _db.UpsertPlayerAsync(data);
                        TriggerClientEvent(player, EventNames.ClientRankedUp, data.Rank);
                        Debug.WriteLine($"[FivePRS] {data.Name} ranked up to Rank {data.Rank}!");
                    }
                }

                Debug.WriteLine($"[FivePRS] +{awardedXP} XP → {player.Name} (callout: {calloutId})");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error completing callout {calloutId}: {ex.Message}");
            }
        }
    }
}
