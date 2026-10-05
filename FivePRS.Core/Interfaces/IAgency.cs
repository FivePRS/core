using System.Threading.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Core.Interfaces
{
    public interface IAgency
    {
        Department Department { get; }

        string AgencyName { get; }

        Task OnDuty(PlayerData player);

        Task OffDuty(PlayerData player);

        Task OnCalloutReceived(CalloutData callout);
    }
}
