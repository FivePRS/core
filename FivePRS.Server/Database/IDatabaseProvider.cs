using System.Threading.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public interface IDatabaseProvider
    {
        Task InitializeAsync();

        Task<PlayerData?> GetPlayerAsync(string license);

        Task UpsertPlayerAsync(PlayerData player);

        Task UpdateDutyStatusAsync(string license, bool isOnDuty);

        Task AddXPAsync(string license, int xpAmount);

        Task UpdateDepartmentAsync(string license, Department department);
    }
}
