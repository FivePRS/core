using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace FivePRS.Server.Database
{
    public sealed class MigrationResult
    {
        public MigrationResult(int fromVersion, int toVersion, int latestVersion, IReadOnlyList<Migration> applied)
        {
            FromVersion = fromVersion;
            ToVersion = toVersion;
            LatestVersion = latestVersion;
            Applied = applied;
        }

        public int FromVersion { get; }

        public int ToVersion { get; }

        public int LatestVersion { get; }

        public IReadOnlyList<Migration> Applied { get; }

        public bool DatabaseIsNewer => FromVersion > LatestVersion;
    }

    public sealed class Migrator
    {
        private const string SqliteSchemaTable = @"CREATE TABLE IF NOT EXISTS fiveprs_schema (
                version    INTEGER PRIMARY KEY,
                name       TEXT    NOT NULL,
                applied_at TEXT    NOT NULL)";

        private const string MySqlSchemaTable = @"CREATE TABLE IF NOT EXISTS `fiveprs_schema` (
                `version`    INT UNSIGNED NOT NULL,
                `name`       VARCHAR(100) NOT NULL,
                `applied_at` DATETIME(6)  NOT NULL,
                PRIMARY KEY (`version`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci";

        private readonly IDatabaseProvider _db;
        private readonly IReadOnlyList<Migration> _migrations;

        public Migrator(IDatabaseProvider db) : this(db, Migrations.All)
        {
        }

        public Migrator(IDatabaseProvider db, IReadOnlyList<Migration> migrations)
        {
            var previous = 0;
            foreach (var migration in migrations)
            {
                if (migration.Version <= previous)
                    throw new ArgumentException($"Migration {migration.Version} ({migration.Name}) must have a higher version than {previous}.");
                previous = migration.Version;
            }

            _db = db;
            _migrations = migrations;
        }

        public int LatestVersion => _migrations.Count == 0 ? 0 : _migrations[_migrations.Count - 1].Version;

        public async Task<int> CurrentVersionAsync()
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();
            await EnsureSchemaTableAsync(conn);
            return await ReadVersionAsync(conn);
        }

        public async Task<MigrationResult> MigrateAsync(Func<int, Task>? beforeMigrating = null)
        {
            using var conn = _db.CreateConnection();
            await conn.OpenAsync();
            await EnsureSchemaTableAsync(conn);

            var from = await ReadVersionAsync(conn);
            var current = from;
            var applied = new List<Migration>();

            foreach (var migration in _migrations)
            {
                if (migration.Version <= current) continue;

                if (applied.Count == 0 && beforeMigrating is not null)
                    await beforeMigrating(from);

                await ApplyAsync(conn, migration);
                applied.Add(migration);
                current = migration.Version;
            }

            return new MigrationResult(from, current, LatestVersion, applied);
        }

        private async Task ApplyAsync(DbConnection conn, Migration migration)
        {
            using var transaction = _db.Dialect == SqlDialect.Sqlite ? conn.BeginTransaction() : null;

            foreach (var step in migration.Steps)
                await step.ApplyAsync(conn, transaction, _db.Dialect);

            using (var cmd = conn.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = "INSERT INTO fiveprs_schema (version, name, applied_at) VALUES (@version, @name, @now)";
                AddParameter(cmd, "@version", migration.Version);
                AddParameter(cmd, "@name", migration.Name);
                AddParameter(cmd, "@now", DateTime.UtcNow);
                await cmd.ExecuteNonQueryAsync();
            }

            transaction?.Commit();
        }

        private Task EnsureSchemaTableAsync(DbConnection conn) =>
            MigrationStep.ExecuteAsync(conn, null, _db.Dialect == SqlDialect.MySql ? MySqlSchemaTable : SqliteSchemaTable);

        private static async Task<int> ReadVersionAsync(DbConnection conn)
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(MAX(version), 0) FROM fiveprs_schema";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        private static void AddParameter(DbCommand cmd, string name, object value)
        {
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            cmd.Parameters.Add(parameter);
        }
    }
}
