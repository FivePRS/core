using System;
using System.Threading.Tasks;
using MySqlConnector;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public sealed class MySqlProvider : IDatabaseProvider
    {
        private readonly string _connectionString;

        public MySqlProvider(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InitializeAsync()
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS `ers_players` (
                    `license`     VARCHAR(60)  NOT NULL,
                    `name`        VARCHAR(100) NOT NULL,
                    `department`  TINYINT UNSIGNED DEFAULT 0,
                    `is_on_duty`  TINYINT(1)   DEFAULT 0,
                    `xp`          INT UNSIGNED  DEFAULT 0,
                    `rank_level`  TINYINT UNSIGNED DEFAULT 1,
                    `agency`      VARCHAR(40)  NOT NULL DEFAULT '',
                    `callsign`    VARCHAR(16)  NOT NULL DEFAULT '',
                    `last_seen`   DATETIME     DEFAULT CURRENT_TIMESTAMP
                                               ON UPDATE CURRENT_TIMESTAMP,
                    PRIMARY KEY (`license`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

                CREATE TABLE IF NOT EXISTS `fiveprs_audit` (
                    `id`             BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
                    `created_at`     DATETIME     NOT NULL,
                    `action`         VARCHAR(40)  NOT NULL,
                    `actor_license`  VARCHAR(60)  NULL,
                    `actor_name`     VARCHAR(100) NOT NULL,
                    `target_license` VARCHAR(60)  NULL,
                    `details`        VARCHAR(255) NOT NULL,
                    PRIMARY KEY (`id`),
                    KEY `idx_fiveprs_audit_target` (`target_license`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            await cmd.ExecuteNonQueryAsync();

            await EnsureColumnAsync(conn, "ers_players", "agency", "VARCHAR(40) NOT NULL DEFAULT ''");
            await EnsureColumnAsync(conn, "ers_players", "callsign", "VARCHAR(16) NOT NULL DEFAULT ''");
        }

        public async Task<PlayerData?> GetPlayerAsync(string license)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT `license`, `name`, `department`, `is_on_duty`, `xp`, `rank_level`, `last_seen`, `agency`, `callsign`
                FROM `ers_players` WHERE `license` = @license LIMIT 1;";
            cmd.Parameters.AddWithValue("@license", license);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new PlayerData
            {
                License    = reader.GetString("license"),
                Name       = reader.GetString("name"),
                Department = (Department)reader.GetByte("department"),
                IsOnDuty   = reader.GetBoolean("is_on_duty"),
                XP         = reader.GetInt32("xp"),
                Rank       = reader.GetByte("rank_level"),
                LastSeen   = reader.GetDateTime("last_seen"),
                Agency     = reader.GetString("agency"),
                Callsign   = reader.GetString("callsign"),
            };
        }

        public async Task SavePlayerAsync(PlayerData player)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO `ers_players`
                    (`license`, `name`, `department`, `agency`, `callsign`, `is_on_duty`, `xp`, `rank_level`, `last_seen`)
                VALUES
                    (@license, @name, @department, @agency, @callsign, @onDuty, @xp, @rank, UTC_TIMESTAMP())
                ON DUPLICATE KEY UPDATE
                    `name`       = VALUES(`name`),
                    `department` = VALUES(`department`),
                    `agency`     = VALUES(`agency`),
                    `callsign`   = VALUES(`callsign`),
                    `is_on_duty` = VALUES(`is_on_duty`),
                    `xp`         = VALUES(`xp`),
                    `rank_level` = VALUES(`rank_level`),
                    `last_seen`  = VALUES(`last_seen`);";

            cmd.Parameters.AddWithValue("@license",    player.License);
            cmd.Parameters.AddWithValue("@name",       player.Name);
            cmd.Parameters.AddWithValue("@department", (byte)player.Department);
            cmd.Parameters.AddWithValue("@agency",     player.Agency);
            cmd.Parameters.AddWithValue("@callsign",   player.Callsign);
            cmd.Parameters.AddWithValue("@onDuty",     player.IsOnDuty);
            cmd.Parameters.AddWithValue("@xp",         player.XP);
            cmd.Parameters.AddWithValue("@rank",       player.Rank);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task AddAuditAsync(AuditEntry entry)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO `fiveprs_audit`
                    (`created_at`, `action`, `actor_license`, `actor_name`, `target_license`, `details`)
                VALUES
                    (UTC_TIMESTAMP(), @action, @actorLicense, @actorName, @targetLicense, @details);";

            cmd.Parameters.AddWithValue("@action",        entry.Action);
            cmd.Parameters.AddWithValue("@actorLicense",  entry.ActorLicense);
            cmd.Parameters.AddWithValue("@actorName",     entry.ActorName);
            cmd.Parameters.AddWithValue("@targetLicense", entry.TargetLicense);
            cmd.Parameters.AddWithValue("@details",       Truncate(entry.Details, 255));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDutyStatusAsync(string license, bool isOnDuty)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE `ers_players` SET `is_on_duty` = @val WHERE `license` = @license;";
            cmd.Parameters.AddWithValue("@val",     isOnDuty);
            cmd.Parameters.AddWithValue("@license", license);

            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task EnsureColumnAsync(MySqlConnection conn, string table, string column, string definition)
        {
            using (var check = conn.CreateCommand())
            {
                check.CommandText = @"
                    SELECT COUNT(*) FROM information_schema.COLUMNS
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table AND COLUMN_NAME = @column;";
                check.Parameters.AddWithValue("@table",  table);
                check.Parameters.AddWithValue("@column", column);
                if (Convert.ToInt64(await check.ExecuteScalarAsync()) > 0) return;
            }

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition};";
            await alter.ExecuteNonQueryAsync();
        }

        private static string Truncate(string value, int maxLength) =>
            value.Length <= maxLength ? value : value.Substring(0, maxLength);
    }
}
