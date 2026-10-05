using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public sealed class SQLiteProvider : IDatabaseProvider
    {
        private readonly string _connectionString;

        public SQLiteProvider(string dbPath)
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _connectionString = $"Data Source={Path.GetFullPath(dbPath)};";
        }

        public async Task InitializeAsync()
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous  = NORMAL;

                CREATE TABLE IF NOT EXISTS ers_players (
                    license     TEXT    NOT NULL PRIMARY KEY,
                    name        TEXT    NOT NULL,
                    department  INTEGER DEFAULT 0,
                    is_on_duty  INTEGER DEFAULT 0,
                    xp          INTEGER DEFAULT 0,
                    rank_level  INTEGER DEFAULT 1,
                    last_seen   TEXT    DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
                );";

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<PlayerData?> GetPlayerAsync(string license)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT license, name, department, is_on_duty, xp, rank_level, last_seen
                FROM ers_players WHERE license = $license LIMIT 1;";
            cmd.Parameters.AddWithValue("$license", license);

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new PlayerData
            {
                License    = reader.GetString(0),
                Name       = reader.GetString(1),
                Department = (Department)reader.GetInt32(2),
                IsOnDuty   = reader.GetInt32(3) == 1,
                XP         = reader.GetInt32(4),
                Rank       = reader.GetInt32(5),
                LastSeen   = DateTime.TryParse(reader.GetString(6), CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt : DateTime.UtcNow
            };
        }

        public async Task SavePlayerAsync(PlayerData player)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ers_players (license, name, department, is_on_duty, xp, rank_level, last_seen)
                VALUES ($license, $name, $dept, $onDuty, $xp, $rank, $seen)
                ON CONFLICT(license) DO UPDATE SET
                    name       = excluded.name,
                    department = excluded.department,
                    is_on_duty = excluded.is_on_duty,
                    xp         = excluded.xp,
                    rank_level = excluded.rank_level,
                    last_seen  = excluded.last_seen;";

            cmd.Parameters.AddWithValue("$license", player.License);
            cmd.Parameters.AddWithValue("$name",    player.Name);
            cmd.Parameters.AddWithValue("$dept",    (int)player.Department);
            cmd.Parameters.AddWithValue("$onDuty",  player.IsOnDuty ? 1 : 0);
            cmd.Parameters.AddWithValue("$xp",      player.XP);
            cmd.Parameters.AddWithValue("$rank",    player.Rank);
            cmd.Parameters.AddWithValue("$seen",    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDutyStatusAsync(string license, bool isOnDuty)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ers_players SET is_on_duty = $val WHERE license = $license;";
            cmd.Parameters.AddWithValue("$val",     isOnDuty ? 1 : 0);
            cmd.Parameters.AddWithValue("$license", license);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
