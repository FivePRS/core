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
                    agency      TEXT    NOT NULL DEFAULT '',
                    callsign    TEXT    NOT NULL DEFAULT '',
                    last_seen   TEXT    DEFAULT (strftime('%Y-%m-%dT%H:%M:%SZ', 'now'))
                );

                CREATE TABLE IF NOT EXISTS fiveprs_audit (
                    id             INTEGER PRIMARY KEY AUTOINCREMENT,
                    created_at     TEXT    NOT NULL,
                    action         TEXT    NOT NULL,
                    actor_license  TEXT,
                    actor_name     TEXT    NOT NULL,
                    target_license TEXT,
                    details        TEXT    NOT NULL
                );

                CREATE INDEX IF NOT EXISTS idx_fiveprs_audit_target ON fiveprs_audit(target_license);";

            await cmd.ExecuteNonQueryAsync();

            await EnsureColumnAsync(conn, "ers_players", "agency", "TEXT NOT NULL DEFAULT ''");
            await EnsureColumnAsync(conn, "ers_players", "callsign", "TEXT NOT NULL DEFAULT ''");
        }

        public async Task<PlayerData?> GetPlayerAsync(string license)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT license, name, department, is_on_duty, xp, rank_level, last_seen, agency, callsign
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
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt) ? dt : DateTime.UtcNow,
                Agency     = reader.GetString(7),
                Callsign   = reader.GetString(8),
            };
        }

        public async Task SavePlayerAsync(PlayerData player)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO ers_players (license, name, department, agency, callsign, is_on_duty, xp, rank_level, last_seen)
                VALUES ($license, $name, $dept, $agency, $callsign, $onDuty, $xp, $rank, $seen)
                ON CONFLICT(license) DO UPDATE SET
                    name       = excluded.name,
                    department = excluded.department,
                    agency     = excluded.agency,
                    callsign   = excluded.callsign,
                    is_on_duty = excluded.is_on_duty,
                    xp         = excluded.xp,
                    rank_level = excluded.rank_level,
                    last_seen  = excluded.last_seen;";

            cmd.Parameters.AddWithValue("$license", player.License);
            cmd.Parameters.AddWithValue("$name",    player.Name);
            cmd.Parameters.AddWithValue("$dept",    (int)player.Department);
            cmd.Parameters.AddWithValue("$agency",  player.Agency);
            cmd.Parameters.AddWithValue("$callsign", player.Callsign);
            cmd.Parameters.AddWithValue("$onDuty",  player.IsOnDuty ? 1 : 0);
            cmd.Parameters.AddWithValue("$xp",      player.XP);
            cmd.Parameters.AddWithValue("$rank",    player.Rank);
            cmd.Parameters.AddWithValue("$seen",    DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task AddAuditAsync(AuditEntry entry)
        {
            using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO fiveprs_audit (created_at, action, actor_license, actor_name, target_license, details)
                VALUES ($createdAt, $action, $actorLicense, $actorName, $targetLicense, $details);";

            cmd.Parameters.AddWithValue("$createdAt",     DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$action",        entry.Action);
            cmd.Parameters.AddWithValue("$actorLicense",  (object?)entry.ActorLicense ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$actorName",     entry.ActorName);
            cmd.Parameters.AddWithValue("$targetLicense", (object?)entry.TargetLicense ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$details",       entry.Details);

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

        private static async Task EnsureColumnAsync(SqliteConnection conn, string table, string column, string definition)
        {
            using (var check = conn.CreateCommand())
            {
                check.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $column;";
                check.Parameters.AddWithValue("$column", column);
                if (Convert.ToInt64(await check.ExecuteScalarAsync()) > 0) return;
            }

            using var alter = conn.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
            await alter.ExecuteNonQueryAsync();
        }
    }
}
