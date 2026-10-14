using CitizenFX.Core;
using FivePRS.Client;
using FivePRS.Core.Civilian;
using FivePRS.Core.Models;

namespace FivePRS.Fire
{
    internal static class FireReport
    {
        public static void Draft(CalloutData data, Vector3 position, string narrative, string? plate = null) =>
            Reports.Draft(new ReportDraft
            {
                Kind      = ReportKind.Incident,
                CallId    = data.Id,
                Title     = data.Name,
                Location  = Reports.Describe(position),
                Narrative = narrative,
                Plate     = plate,
            });

        public static string Fires(int count) => count == 1 ? "1 fire" : $"{count} fires";
    }
}
