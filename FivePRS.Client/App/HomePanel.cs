using CitizenFX.Core;
using CitizenFX.Core.Native;
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

        public static string CurrentLocation()
        {
            var position = Game.PlayerPed.Position;
            uint street = 0, crossing = 0;
            API.GetStreetNameAtCoord(position.X, position.Y, position.Z, ref street, ref crossing);

            var road = API.GetStreetNameFromHashKey(street);
            var zone = API.GetLabelText(API.GetNameOfZone(position.X, position.Y, position.Z));
            if (string.IsNullOrEmpty(zone) || zone == "NULL") return road;
            return string.IsNullOrEmpty(road) ? zone : $"{road}, {zone}";
        }
    }
}
