using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    /// <summary>
    /// SQLite persistence layer — zero external dependencies, perfect for singleplayer
    /// or small communities. Drop-in replacement for MySqlProvider via DatabaseManager.
    /// </summary>
    public sealed class SQLiteProvider : IDatabaseProvider
    {
        private readonly string _connectionString;

        public SQLiteProvider(string dbPath)
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _connectionString = $"Data Source={Path.GetFullPath(dbPath)};Cache=Shared;";
        }

        public async Task InitializeAsync()
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
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
                );

                CREATE INDEX IF NOT EXISTS idx_ers_license ON ers_players(license);";

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<PlayerData?> GetPlayerAsync(string license)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM ers_players WHERE license = $license LIMIT 1;";
            cmd.Parameters.AddWithValue("$license", license);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new PlayerData
            {
                License    = reader.GetString(0),
                Name       = reader.GetString(1),
                Department = (Department)reader.GetInt32(2),
                IsOnDuty   = reader.GetInt32(3) == 1,
                XP         = reader.GetInt32(4),
                Rank       = reader.GetInt32(5),
                LastSeen   = DateTime.TryParse(reader.GetString(6), out var dt) ? dt : DateTime.UtcNow
            };
        }

        public async Task UpsertPlayerAsync(PlayerData player)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ers_players (license, name, department, is_on_duty, xp, rank_level, last_seen)
                VALUES ($license, $name, $dept, 0, $xp, $rank, $seen)
                ON CONFLICT(license) DO UPDATE SET
                    name      = excluded.name,
                    last_seen = excluded.last_seen;";

            cmd.Parameters.AddWithValue("$license", player.License);
            cmd.Parameters.AddWithValue("$name",    player.Name);
            cmd.Parameters.AddWithValue("$dept",    (int)player.Department);
            cmd.Parameters.AddWithValue("$xp",      player.XP);
            cmd.Parameters.AddWithValue("$rank",    player.Rank);
            cmd.Parameters.AddWithValue("$seen",    DateTime.UtcNow.ToString("o"));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDutyStatusAsync(string license, bool isOnDuty)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ers_players SET is_on_duty = $val WHERE license = $license;";
            cmd.Parameters.AddWithValue("$val",     isOnDuty ? 1 : 0);
            cmd.Parameters.AddWithValue("$license", license);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task AddXPAsync(string license, int xpAmount)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ers_players SET xp = xp + $xp WHERE license = $license;";
            cmd.Parameters.AddWithValue("$xp",      Math.Max(0, xpAmount));
            cmd.Parameters.AddWithValue("$license", license);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDepartmentAsync(string license, Department department)
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE ers_players SET department = $dept WHERE license = $license;";
            cmd.Parameters.AddWithValue("$dept",    (int)department);
            cmd.Parameters.AddWithValue("$license", license);

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                await using var conn = new SqliteConnection(_connectionString);
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
