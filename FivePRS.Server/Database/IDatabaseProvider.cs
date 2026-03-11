using System.Threading.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    /// <summary>
    /// Abstraction over the underlying SQL engine.
    /// Swap between MySQL and SQLite by changing a single convar — no code changes needed.
    /// </summary>
    public interface IDatabaseProvider
    {
        Task InitializeAsync();

        Task<PlayerData?> GetPlayerAsync(string license);

        Task UpsertPlayerAsync(PlayerData player);

        Task UpdateDutyStatusAsync(string license, bool isOnDuty);

        Task AddXPAsync(string license, int xpAmount);

        Task UpdateDepartmentAsync(string license, Department department);

        Task<bool> TestConnectionAsync();
    }
}
