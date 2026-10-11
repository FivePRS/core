using System;
using System.Threading.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public sealed class PlayerStore : SqlStore
    {
        public PlayerStore(IDatabaseProvider db) : base(db)
        {
        }

        public async Task<PlayerData?> GetAsync(string license)
        {
            var rows = await QueryAsync(
                "SELECT license, name, department, is_on_duty, xp, rank_level, last_seen, agency, callsign FROM ers_players WHERE license = @license LIMIT 1",
                row => new PlayerData
                {
                    License    = row.GetString("license"),
                    Name       = row.GetString("name"),
                    Department = (Department)row.GetInt32("department"),
                    IsOnDuty   = row.GetBoolean("is_on_duty"),
                    XP         = row.GetInt32("xp"),
                    Rank       = row.GetInt32("rank_level"),
                    LastSeen   = row.IsNull("last_seen") ? DateTime.UtcNow : row.GetDateTime("last_seen"),
                    Agency     = row.GetString("agency"),
                    Callsign   = row.GetString("callsign"),
                },
                ("@license", license));
            return rows.Count > 0 ? rows[0] : null;
        }

        public Task SaveAsync(PlayerData player) =>
            ExecuteAsync(
                "INSERT INTO ers_players (license, name, department, agency, callsign, is_on_duty, xp, rank_level, last_seen) " +
                "VALUES (@license, @name, @department, @agency, @callsign, @onDuty, @xp, @rank, @now) " +
                (IsMySql
                    ? "ON DUPLICATE KEY UPDATE name = VALUES(name), department = VALUES(department), agency = VALUES(agency), callsign = VALUES(callsign), " +
                      "is_on_duty = VALUES(is_on_duty), xp = VALUES(xp), rank_level = VALUES(rank_level), last_seen = VALUES(last_seen)"
                    : "ON CONFLICT(license) DO UPDATE SET name = excluded.name, department = excluded.department, agency = excluded.agency, callsign = excluded.callsign, " +
                      "is_on_duty = excluded.is_on_duty, xp = excluded.xp, rank_level = excluded.rank_level, last_seen = excluded.last_seen"),
                ("@license", player.License), ("@name", player.Name), ("@department", (int)player.Department),
                ("@agency", player.Agency), ("@callsign", player.Callsign), ("@onDuty", player.IsOnDuty ? 1 : 0),
                ("@xp", player.XP), ("@rank", player.Rank), ("@now", DateTime.UtcNow));

        public Task UpdateDutyStatusAsync(string license, bool isOnDuty) =>
            ExecuteAsync("UPDATE ers_players SET is_on_duty = @onDuty WHERE license = @license",
                ("@onDuty", isOnDuty ? 1 : 0), ("@license", license));

        public Task AddAuditAsync(AuditEntry entry) =>
            ExecuteAsync(
                "INSERT INTO fiveprs_audit (created_at, action, actor_license, actor_name, target_license, details) " +
                "VALUES (@now, @action, @actorLicense, @actorName, @targetLicense, @details)",
                ("@now", DateTime.UtcNow), ("@action", entry.Action), ("@actorLicense", entry.ActorLicense),
                ("@actorName", entry.ActorName), ("@targetLicense", entry.TargetLicense),
                ("@details", entry.Details.Length <= 255 ? entry.Details : entry.Details.Substring(0, 255)));
    }
}
