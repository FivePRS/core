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
        private bool _ready = false;

        public bool IsReady => _ready;

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
                _provider = new SQLiteProvider(connectionString ?? Path.Combine(resourcePath, "data", "fiveprs.db"));
            }

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
