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

        public async Task InitializeAsync(DatabaseType dbType, string? connectionString)
        {
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
                _provider = new SQLiteProvider(connectionString ?? Path.Combine(resourcePath, "data", "fiveprs.db"));
            }

            await _provider.InitializeAsync();

            Civilians = new CivilianStore(_provider);
            await Civilians.InitializeAsync();

            Roster = new RosterStore(_provider);
            await Roster.InitializeAsync();

            Appearances = new AppearanceStore(_provider);
            await Appearances.InitializeAsync();

            IsReady = true;

            Debug.WriteLine($"[FivePRS] Database ({dbType}) ready.");
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
