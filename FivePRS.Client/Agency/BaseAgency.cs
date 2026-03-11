using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Events;
using FivePRS.Core.Interfaces;
using FivePRS.Core.Models;

namespace FivePRS.Client.Agency
{
    /// <summary>
    /// Abstract base for all FivePRS department modules.
    ///
    /// Inheriting from <see cref="BaseScript"/> lets the FiveM runtime auto-instantiate
    /// each concrete subclass when their dll is loaded, giving them access to Tick,
    /// Delay(), TriggerEvent(), and EventHandlers without any manual wiring.
    ///
    /// Subclasses must override <see cref="Department"/>, <see cref="AgencyName"/>,
    /// and <see cref="OnCalloutReceived"/>. The duty lifecycle methods have virtual
    /// defaults so agencies only override what they customise.
    /// </summary>
    public abstract class BaseAgency : BaseScript, IAgency
    {

        public abstract Department Department { get; }
        public abstract string AgencyName { get; }

        protected static PlayerData CurrentPlayer => ClientBrain.LocalPlayerData;

        protected BaseAgency()
        {
            EventHandlers[EventNames.LocalDutyChanged]     += new System.Action<bool, int>(OnLocalDutyChanged);
            EventHandlers[EventNames.LocalCalloutReceived] += new System.Action<string>(OnLocalCalloutReceived);
        }

        public virtual async Task OnDuty(PlayerData player)
        {
            Debug.WriteLine($"[{AgencyName}] {player.Name} is ON DUTY.");
            await Task.CompletedTask;
        }

        public virtual async Task OffDuty(PlayerData player)
        {
            Debug.WriteLine($"[{AgencyName}] {player.Name} is OFF DUTY.");
            await Task.CompletedTask;
        }

        public abstract Task OnCalloutReceived(CalloutData callout);

        private async void OnLocalDutyChanged(bool isOnDuty, int departmentId)
        {
            if ((Department)departmentId != Department) return;

            if (isOnDuty)
                await OnDuty(CurrentPlayer);
            else
                await OffDuty(CurrentPlayer);
        }

        private async void OnLocalCalloutReceived(string calloutJson)
        {
            CalloutData callout;
            try
            {
                callout = Newtonsoft.Json.JsonConvert.DeserializeObject<CalloutData>(calloutJson)!;
            }
            catch
            {
                return;
            }

            if (callout.RequiredDepartment != Department) return;
            await OnCalloutReceived(callout);
        }

        protected static void Notify(string message) => ClientBrain.ShowNotification(message);
    }
}
