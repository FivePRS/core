using System;
using System.Threading.Tasks;
using MySqlConnector;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    /// <summary>
    /// MySQL / MariaDB persistence layer using MySqlConnector (async-first, MIT-licensed).
    /// Connection string is read from the fiveprs_db_connection convar.
    /// </summary>
    public sealed class MySqlProvider : IDatabaseProvider
    {
        private readonly string _connectionString;

        public MySqlProvider(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InitializeAsync()
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
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
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM `ers_players` WHERE `license` = @license LIMIT 1;";
            cmd.Parameters.AddWithValue("@license", license);

            await using var reader = await cmd.ExecuteReaderAsync();
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

        public async Task UpsertPlayerAsync(PlayerData player)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO `ers_players`
                    (`license`, `name`, `department`, `is_on_duty`, `xp`, `rank_level`, `last_seen`)
                VALUES
                    (@license, @name, @department, 0, @xp, @rank, NOW())
                ON DUPLICATE KEY UPDATE
                    `name`      = VALUES(`name`),
                    `last_seen` = VALUES(`last_seen`);";

            cmd.Parameters.AddWithValue("@license",    player.License);
            cmd.Parameters.AddWithValue("@name",       player.Name);
            cmd.Parameters.AddWithValue("@department", (byte)player.Department);
            cmd.Parameters.AddWithValue("@xp",         player.XP);
            cmd.Parameters.AddWithValue("@rank",       player.Rank);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDutyStatusAsync(string license, bool isOnDuty)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE `ers_players` SET `is_on_duty` = @val WHERE `license` = @license;";
            cmd.Parameters.AddWithValue("@val",     isOnDuty);
            cmd.Parameters.AddWithValue("@license", license);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task AddXPAsync(string license, int xpAmount)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                UPDATE `ers_players`
                SET
                    `xp`         = `xp` + @xp,
                    -- Auto rank-up: every 100*rank^2 XP the rank increments once.
                    `rank_level` = `rank_level` + FLOOR((`xp` + @xp) / (100 * POW(`rank_level`, 2)))
                WHERE `license` = @license;";
            cmd.Parameters.AddWithValue("@xp",      Math.Max(0, xpAmount));
            cmd.Parameters.AddWithValue("@license", license);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDepartmentAsync(string license, Department department)
        {
            await using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE `ers_players` SET `department` = @dept WHERE `license` = @license;";
            cmd.Parameters.AddWithValue("@dept",    (byte)department);
            cmd.Parameters.AddWithValue("@license", license);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var conn = new MySqlConnection(_connectionString);
                await conn.OpenAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
