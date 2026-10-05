using System.Threading.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public interface IDatabaseProvider
    {
        Task InitializeAsync();

        Task<PlayerData?> GetPlayerAsync(string license);

        Task SavePlayerAsync(PlayerData player);

        Task UpdateDutyStatusAsync(string license, bool isOnDuty);
    }
}
