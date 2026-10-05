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
                    `last_seen`   DATETIME     DEFAULT CURRENT_TIMESTAMP
                                               ON UPDATE CURRENT_TIMESTAMP,
                    PRIMARY KEY (`license`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;";

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<PlayerData?> GetPlayerAsync(string license)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT `license`, `name`, `department`, `is_on_duty`, `xp`, `rank_level`, `last_seen`
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
                LastSeen   = reader.GetDateTime("last_seen")
            };
        }

        public async Task SavePlayerAsync(PlayerData player)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO `ers_players`
                    (`license`, `name`, `department`, `is_on_duty`, `xp`, `rank_level`, `last_seen`)
                VALUES
                    (@license, @name, @department, @onDuty, @xp, @rank, UTC_TIMESTAMP())
                ON DUPLICATE KEY UPDATE
                    `name`       = VALUES(`name`),
                    `department` = VALUES(`department`),
                    `is_on_duty` = VALUES(`is_on_duty`),
                    `xp`         = VALUES(`xp`),
                    `rank_level` = VALUES(`rank_level`),
                    `last_seen`  = VALUES(`last_seen`);";

            cmd.Parameters.AddWithValue("@license",    player.License);
            cmd.Parameters.AddWithValue("@name",       player.Name);
            cmd.Parameters.AddWithValue("@department", (byte)player.Department);
            cmd.Parameters.AddWithValue("@onDuty",     player.IsOnDuty);
            cmd.Parameters.AddWithValue("@xp",         player.XP);
            cmd.Parameters.AddWithValue("@rank",       player.Rank);

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
    }
}
