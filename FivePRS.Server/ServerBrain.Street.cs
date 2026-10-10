using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Civilian;
using FivePRS.Core.Events;
using FivePRS.Server.Civilian;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const string StopPerson = "person";
        private const string StopVehicle = "vehicle";

        private StreetService? _street;

        private void RegisterStreetEvents()
        {
            EventHandlers[EventNames.ServerStopIdentify] += new Action<Player, int, bool>(OnStopIdentify);
            EventHandlers[EventNames.ServerStopPlate]    += new Action<Player, string, string, int, bool>(OnStopPlate);
        }

        private void OnStopIdentify([FromSource] Player player, int seed, bool female) =>
            RunStop(player, StopPerson, street => street.IdentifyAsync(seed, female));

        private void OnStopPlate([FromSource] Player player, string plate, string model, int driverSeed, bool driverFemale) =>
            RunStop(player, StopVehicle, street => street.PlateAsync(plate, model, driverSeed, driverFemale));

        private async void RunStop(Player player, string kind, Func<StreetService, Task<(LookupResult? Result, string? Error)>> action)
        {
            if (!Allow(player, "lookups", l => l.Lookups)) return;

            if (!TryGetOfficer(player, out _) || _street is null)
            {
                TriggerClientEvent(player, EventNames.ClientStopResult, kind, string.Empty, "Records are only available to on-duty police.");
                return;
            }

            try
            {
                var (result, error) = await action(_street);
                var json = result is null ? string.Empty : JsonConvert.SerializeObject(result);

                if (result is not null) TriggerClientEvent(player, EventNames.ClientLookupResult, json);
                TriggerClientEvent(player, EventNames.ClientStopResult, kind, json, error ?? string.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Traffic stop check failed for {player.Name}: {ex}");
                TriggerClientEvent(player, EventNames.ClientStopResult, kind, string.Empty, "Something went wrong. Please try again.");
            }
        }
    }
}
