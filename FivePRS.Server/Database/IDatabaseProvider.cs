using System.Data.Common;
using System.Threading.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public interface IDatabaseProvider
    {
        SqlDialect Dialect { get; }

        DbConnection CreateConnection();

        Task InitializeAsync();

        Task<PlayerData?> GetPlayerAsync(string license);

        Task SavePlayerAsync(PlayerData player);

        Task UpdateDutyStatusAsync(string license, bool isOnDuty);

        Task AddAuditAsync(AuditEntry entry);
    }
}
