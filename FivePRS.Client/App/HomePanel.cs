using CitizenFX.Core;
using FivePRS.Client.Dispatch;

namespace FivePRS.Client.App
{
    internal static class HomePanel
    {
        public static object BuildView()
        {
            var snapshot = DispatchClient.Snapshot;
            return new
            {
                Offer          = DispatchPanel.BuildOffer(),
                Units          = snapshot.Units.Count,
                Calls          = snapshot.Calls.Count,
                ShiftStartedAt = DutyPanel.ShiftStartedAt,
                Location       = CurrentLocation(),
            };
        }

        public static string CurrentLocation() => Reports.Describe(Game.PlayerPed.Position);
    }
}
