using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Civilian;
using FivePRS.Core.Events;
using Newtonsoft.Json;

namespace FivePRS.Client
{
    public static class Reports
    {
        public static void Draft(ReportDraft draft) =>
            ClientEvents.TriggerServer(EventNames.ServerReportDraft, JsonConvert.SerializeObject(draft));

        public static string Describe(Vector3 position)
        {
            uint street = 0, crossing = 0;
            API.GetStreetNameAtCoord(position.X, position.Y, position.Z, ref street, ref crossing);

            var road = API.GetStreetNameFromHashKey(street);
            var zone = API.GetLabelText(API.GetNameOfZone(position.X, position.Y, position.Z));
            if (string.IsNullOrEmpty(zone) || zone == "NULL") return road;
            return string.IsNullOrEmpty(road) ? zone : $"{road}, {zone}";
        }
    }
}
