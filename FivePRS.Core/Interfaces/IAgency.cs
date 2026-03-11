using System.Threading.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Core.Interfaces
{
    /// <summary>
    /// Contract that every department module (Police, EMS, Fire) must fulfill.
    /// Concrete implementations extend <c>BaseAgency</c> in FivePRS.Client, which
    /// also inherits from <c>CitizenFX.Core.BaseScript</c> so each module is
    /// auto-instantiated by the FiveM runtime when its dll is loaded.
    /// </summary>
    public interface IAgency
    {
        Department Department { get; }

        string AgencyName { get; }

        Task OnDuty(PlayerData player);

        Task OffDuty(PlayerData player);

        Task OnCalloutReceived(CalloutData callout);
    }
}
