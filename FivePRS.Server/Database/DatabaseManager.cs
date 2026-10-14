using System;
using System.IO;
using System.Threading.Tasks;
using FivePRS.Core.Logging;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public enum DatabaseType
    {
        SQLite,
        MySQL
    }

    public sealed class DatabaseManager
    {
        private static readonly Logger Log = Logger.For("Database");

        private IDatabaseProvider _provider = null!;

        public bool IsReady { get; private set; }

        public PlayerStore Players { get; private set; } = null!;

        public CivilianStore Civilians { get; private set; } = null!;

        public RosterStore Roster { get; private set; } = null!;

        public AppearanceStore Appearances { get; private set; } = null!;

        public PreferencesStore Preferences { get; private set; } = null!;

        public PhotoStore Photos { get; private set; } = null!;

        public PositionStore Positions { get; private set; } = null!;

        public int SchemaVersion { get; private set; }

        public Task InitializeAsync(DatabaseType dbType, string? connectionString) =>
            MainThread.Run(() => InitializeCoreAsync(dbType, connectionString));

        private async Task InitializeCoreAsync(DatabaseType dbType, string? connectionString)
        {
            Func<int, Task>? backup = null;

            if (dbType == DatabaseType.MySQL)
            {
                if (connectionString is not null && !string.IsNullOrWhiteSpace(connectionString))
                    Log.Warn("fiveprs_db_connection is ignored with MySQL. Set mysql_connection_string for oxmysql instead.");
                _provider = new OxMySqlProvider(new OxMySqlBridge());
            }
            else
            {
                if (connectionString is not null && connectionString.Contains("="))
                    throw new DatabaseSetupException(
                        "fiveprs_db_connection is set to a MySQL connection string, but fiveprs_db_type is sqlite. " +
                        "Set fiveprs_db_type \"mysql\" to use MySQL or MariaDB, or clear fiveprs_db_connection to use SQLite.");

                var resourcePath = Resource.Path;
                var nativePath = SqliteNativeLoader.Load(Path.Combine(resourcePath, "server"));
                Log.Debug($"Native SQLite loaded from {nativePath}.");

                var dbPath = connectionString ?? Path.Combine(resourcePath, "data", "fiveprs.db");
                var sqlite = new SQLiteProvider(dbPath);
                _provider = sqlite;

                if (File.Exists(dbPath) && new FileInfo(dbPath).Length > 0)
                    backup = version => BackupAsync(sqlite, dbPath, version);
            }

            await _provider.InitializeAsync();

            var result = await new Migrator(_provider).MigrateAsync(backup);
            if (result.FromVersion == 0 && result.Applied.Count > 0)
            {
                Log.Info($"Created a new database (schema {result.ToVersion}).");
            }
            else
            {
                foreach (var migration in result.Applied)
                    Log.Info($"Database updated to schema {migration.Version} ({migration.Name}).");
            }

            if (result.DatabaseIsNewer)
                Log.Warn($"The database is at schema {result.FromVersion}, newer than this build supports ({result.LatestVersion}). Update FivePRS or restore your backup.");
            SchemaVersion = result.ToVersion;

            Players     = new PlayerStore(_provider);
            Civilians   = new CivilianStore(_provider);
            Roster      = new RosterStore(_provider);
            Appearances = new AppearanceStore(_provider);
            Preferences = new PreferencesStore(_provider);
            Photos      = new PhotoStore(_provider);
            Positions   = new PositionStore(_provider);

            IsReady = true;

            Log.Debug($"Database ({dbType}) ready.");
        }

        private static async Task BackupAsync(SQLiteProvider sqlite, string dbPath, int version)
        {
            var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".", "backups");
            var path = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(dbPath)}-schema{version}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");
            await sqlite.BackupAsync(path);
            Log.Info($"Database backed up to backups/{Path.GetFileName(path)}.");
        }

        public Task<PlayerData?> GetPlayerAsync(string license) => Players.GetAsync(license);

        public Task SavePlayerAsync(PlayerData player) => Players.SaveAsync(player);

        public Task UpdateDutyStatusAsync(string license, bool isOnDuty) => Players.UpdateDutyStatusAsync(license, isOnDuty);

        public Task AddAuditAsync(AuditEntry entry) => Players.AddAuditAsync(entry);
    }
}
