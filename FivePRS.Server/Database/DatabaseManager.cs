using System;
using System.IO;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
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
        private IDatabaseProvider _provider = null!;

        public bool IsReady { get; private set; }

        public CivilianStore Civilians { get; private set; } = null!;

        public RosterStore Roster { get; private set; } = null!;

        public AppearanceStore Appearances { get; private set; } = null!;

        public PreferencesStore Preferences { get; private set; } = null!;

        public PositionStore Positions { get; private set; } = null!;

        public int SchemaVersion { get; private set; }

        public async Task InitializeAsync(DatabaseType dbType, string? connectionString)
        {
            Func<int, Task>? backup = null;

            if (dbType == DatabaseType.MySQL)
            {
                if (connectionString is null || string.IsNullOrWhiteSpace(connectionString))
                    throw new ArgumentException("[FivePRS] MySQL selected but fiveprs_db_connection is empty.");
                _provider = new MySqlProvider(connectionString);
            }
            else
            {
                var resourcePath = API.GetResourcePath(API.GetCurrentResourceName());
                var nativePath = SqliteNativeLoader.Load(Path.Combine(resourcePath, "server"));
                Debug.WriteLine($"[FivePRS] Native SQLite loaded from {nativePath}.");

                var dbPath = connectionString ?? Path.Combine(resourcePath, "data", "fiveprs.db");
                var sqlite = new SQLiteProvider(dbPath);
                _provider = sqlite;

                if (File.Exists(dbPath) && new FileInfo(dbPath).Length > 0)
                    backup = version => BackupAsync(sqlite, dbPath, version);
            }

            await _provider.InitializeAsync();

            var result = await new Migrator(_provider).MigrateAsync(backup);
            foreach (var migration in result.Applied)
                Debug.WriteLine($"[FivePRS] Applied database migration {migration.Version}: {migration.Name}.");
            if (result.DatabaseIsNewer)
                Debug.WriteLine($"[FivePRS] WARNING: The database is at schema version {result.FromVersion}, but this FivePRS build only knows up to {result.LatestVersion}. Update FivePRS, or restore the database backup taken before you downgraded.");
            SchemaVersion = result.ToVersion;

            Civilians   = new CivilianStore(_provider);
            Roster      = new RosterStore(_provider);
            Appearances = new AppearanceStore(_provider);
            Preferences = new PreferencesStore(_provider);
            Positions   = new PositionStore(_provider);

            IsReady = true;

            Debug.WriteLine($"[FivePRS] Database ({dbType}) ready.");
        }

        private static async Task BackupAsync(SQLiteProvider sqlite, string dbPath, int version)
        {
            var directory = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(dbPath)) ?? ".", "backups");
            var path = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(dbPath)}-schema{version}-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db");
            await sqlite.BackupAsync(path);
            Debug.WriteLine($"[FivePRS] Backed up the database to {path} before updating it.");
        }

        public Task<PlayerData?> GetPlayerAsync(string license)
            => _provider.GetPlayerAsync(license);

        public Task SavePlayerAsync(PlayerData player)
            => _provider.SavePlayerAsync(player);

        public Task UpdateDutyStatusAsync(string license, bool isOnDuty)
            => _provider.UpdateDutyStatusAsync(license, isOnDuty);

        public Task AddAuditAsync(AuditEntry entry)
            => _provider.AddAuditAsync(entry);
    }
}
