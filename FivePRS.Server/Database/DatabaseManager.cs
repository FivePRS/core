using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public enum DatabaseType
    {
        SQLite,
        MySQL
    }

    /// <summary>
    /// Singleton-style façade that owns the active <see cref="IDatabaseProvider"/>.
    /// All server-side code goes through this class; swapping the engine only requires
    /// changing the fiveprs_db_type convar.
    /// </summary>
    public sealed class DatabaseManager
    {
        private IDatabaseProvider _provider = null!;
        private bool _ready = false;

        public bool IsReady => _ready;

        public async Task InitializeAsync(DatabaseType dbType, string? connectionString)
        {
            _provider = dbType switch
            {
                DatabaseType.MySQL => string.IsNullOrWhiteSpace(connectionString)
                    ? throw new ArgumentException("[FivePRS] MySQL selected but fiveprs_db_connection is empty.")
                    : new MySqlProvider(connectionString),

                _ => new SQLiteProvider(connectionString ?? "FivePRS/fiveprs.db")
            };

            if (!await _provider.TestConnectionAsync())
                throw new Exception($"[FivePRS] {dbType} connection test failed. Check convars.");

            await _provider.InitializeAsync();
            _ready = true;

            Debug.WriteLine($"[FivePRS] Database ({dbType}) ready.");
        }

        public Task<PlayerData?> GetPlayerAsync(string license)
            => _provider.GetPlayerAsync(license);

        public Task UpsertPlayerAsync(PlayerData player)
            => _provider.UpsertPlayerAsync(player);

        public Task UpdateDutyStatusAsync(string license, bool isOnDuty)
            => _provider.UpdateDutyStatusAsync(license, isOnDuty);

        public Task AddXPAsync(string license, int xpAmount)
            => _provider.AddXPAsync(license, xpAmount);

        public Task UpdateDepartmentAsync(string license, Department department)
            => _provider.UpdateDepartmentAsync(license, department);
    }
}
