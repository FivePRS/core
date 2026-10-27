using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Core.Police;
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
            EventHandlers[EventNames.ServerRunPlate]     += new Action<Player, string, string, int, bool, bool>(OnRunPlate);
            EventHandlers[EventNames.ServerRunPerson]    += new Action<Player, int, bool>(OnRunPerson);
            EventHandlers[EventNames.ServerPlateRead]    += new Action<Player, string>(OnPlateRead);
        }

        private async void OnPlateRead([FromSource] Player player, string plate)
        {
            if (!ConfigManager.Settings.Radar.PlateReader || _lookup is null) return;
            if (!Allow(player, "plateReads", l => l.PlateReads) || !TryGetOfficer(player, out _)) return;
            if (!CivilianRules.TryNormalizePlate(plate, out var normalized)) return;

            try
            {
                var check = await _lookup.CheckPlateAsync(normalized);
                TriggerClientEvent(player, EventNames.ClientPlateRead, JsonConvert.SerializeObject(check));
            }
            catch (Exception ex)
            {
                Log.Warn($"Plate read failed for {player.Name}", ex);
            }
        }

        private void OnRunPlate([FromSource] Player player, string plate, string model, int driverSeed, bool driverFemale, bool hasDriver) =>
            RunLookup(player, Lookups, (_, __) => _street is null
                ? Task.FromResult<(LookupResult?, string?)>((null, "Records are not available yet."))
                : _street.PlateAsync(plate, model, hasDriver ? StreetProfiles.Person(driverSeed, driverFemale) : null));

        private void OnRunPerson([FromSource] Player player, int seed, bool female) =>
            RunLookup(player, Lookups, (_, __) => _street is null
                ? Task.FromResult<(LookupResult?, string?)>((null, "Records are not available yet."))
                : _street.IdentifyAsync(seed, female));

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
                var json = result is null ? string.Empty : JsonConvert.SerializeObject(RecordAccess.Filter(result, Department.Police));

                if (result is not null) TriggerClientEvent(player, EventNames.ClientLookupResult, json);
                TriggerClientEvent(player, EventNames.ClientStopResult, kind, json, error ?? string.Empty);
            }
            catch (Exception ex)
            {
                Log.Error($"Traffic stop check failed for {player.Name}", ex);
                TriggerClientEvent(player, EventNames.ClientStopResult, kind, string.Empty, "Something went wrong. Please try again.");
            }
        }
    }
}
