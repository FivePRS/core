using System;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Civilian;
using FivePRS.Server.Database;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const bool Lookups = true;
        private const bool Records = false;

        private LookupService? _lookup;

        private void RegisterLookupEvents()
        {
            EventHandlers[EventNames.ServerLookupName]       += new Action<Player, string>(OnLookupName);
            EventHandlers[EventNames.ServerLookupPlate]      += new Action<Player, string>(OnLookupPlate);
            EventHandlers[EventNames.ServerLookupCharacter]  += new Action<Player, int>(OnLookupCharacter);
            EventHandlers[EventNames.ServerRecordIssue]      += new Action<Player, int, int, string, int>(OnRecordIssue);
            EventHandlers[EventNames.ServerRecordResolve]    += new Action<Player, int, bool>(OnRecordResolve);
            EventHandlers[EventNames.ServerLicenseSetStatus] += new Action<Player, int, string, int>(OnLicenseSetStatus);
            EventHandlers[EventNames.ServerVehicleFlag]      += new Action<Player, int, bool>(OnVehicleFlag);
        }

        private void OnLookupName([FromSource] Player player, string term) =>
            RunLookup(player, Lookups, (lookup, _) => lookup.SearchByNameAsync(term));

        private void OnLookupPlate([FromSource] Player player, string plate) =>
            RunLookup(player, Lookups, (lookup, _) => lookup.SearchByPlateAsync(plate));

        private void OnLookupCharacter([FromSource] Player player, int characterId) =>
            RunLookup(player, Lookups, (lookup, _) => lookup.GetRecordAsync(characterId));

        private void OnRecordIssue([FromSource] Player player, int characterId, int type, string description, int fine) =>
            RunLookup(player, Records, async (lookup, officer) =>
            {
                var error = await lookup.IssueRecordAsync(officer, characterId, type, description, fine);
                if (error is not null) return (null, error);

                var label = ((RecordType)type).ToString().ToLowerInvariant();
                Audit(AuditActions.RecordIssued, player, await OwnerLicenseAsync(characterId), $"{label} character {characterId}");
                await NotifyCharacterOwnerAsync(characterId, $"~o~Records update~w~ | A {label} was added to your record by {officer.Callsign}.");
                return await lookup.GetRecordAsync(characterId);
            });

        private void OnRecordResolve([FromSource] Player player, int recordId, bool served) =>
            RunLookup(player, Records, async (lookup, officer) =>
            {
                var (characterId, error) = await lookup.ResolveWarrantAsync(officer, recordId, served);
                if (error is not null || characterId is null) return (null, error ?? "Warrant not found.");

                Audit(AuditActions.WarrantResolved, player, await OwnerLicenseAsync(characterId.Value), $"warrant {recordId} {(served ? "served" : "cleared")}");
                await NotifyCharacterOwnerAsync(characterId.Value, $"~g~Records update~w~ | A warrant on your record was {(served ? "served" : "cleared")}.");
                return await lookup.GetRecordAsync(characterId.Value);
            });

        private void OnLicenseSetStatus([FromSource] Player player, int characterId, string type, int status) =>
            RunLookup(player, Records, async (lookup, officer) =>
            {
                var error = await lookup.SetLicenseStatusAsync(characterId, type, status);
                if (error is not null) return (null, error);

                var label = ((LicenseStatus)status).ToString().ToLowerInvariant();
                Audit(AuditActions.LicenseStatusSet, player, await OwnerLicenseAsync(characterId), $"{type} {label} character {characterId}");
                var name = ConfigManager.Licenses.Find(type)?.Name ?? type;
                await NotifyCharacterOwnerAsync(characterId, $"~y~Records update~w~ | Your {name} is now {label}.");
                return await lookup.GetRecordAsync(characterId);
            });

        private void OnVehicleFlag([FromSource] Player player, int vehicleId, bool stolen) =>
            RunLookup(player, Records, async (lookup, officer) =>
            {
                var (characterId, error) = await lookup.SetVehicleStolenAsync(vehicleId, stolen);
                if (error is not null || characterId is null) return (null, error ?? "Vehicle not found.");

                Audit(AuditActions.VehicleFlagged, player, await OwnerLicenseAsync(characterId.Value), $"vehicle {vehicleId} {(stolen ? "stolen" : "recovered")}");
                await NotifyCharacterOwnerAsync(characterId.Value, null);
                return await lookup.GetRecordAsync(characterId.Value);
            });

        private async void RunLookup(Player player, bool lookups, Func<LookupService, OfficerInfo, Task<(LookupResult? Result, string? Error)>> action)
        {
            if (lookups ? !Allow(player, "lookups", l => l.Lookups) : !Allow(player, "records", l => l.Records))
            {
                TriggerClientEvent(player, EventNames.ClientLookupError, "Slow down. Try again in a few seconds.");
                return;
            }

            if (!TryGetOfficer(player, out var officer))
            {
                TriggerClientEvent(player, EventNames.ClientLookupError, "Records are only available to on-duty police.");
                return;
            }

            if (_lookup is null)
            {
                TriggerClientEvent(player, EventNames.ClientLookupError, "Records are not available yet.");
                return;
            }

            try
            {
                var (result, error) = await action(_lookup, officer);
                if (error is not null)
                    TriggerClientEvent(player, EventNames.ClientLookupError, error);
                else if (result is not null)
                    TriggerClientEvent(player, EventNames.ClientLookupResult, JsonConvert.SerializeObject(result));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Records action failed for {player.Name}: {ex}");
                TriggerClientEvent(player, EventNames.ClientLookupError, "Something went wrong. Please try again.");
            }
        }

        private bool TryGetOfficer(Player player, out OfficerInfo officer)
        {
            officer = null!;
            if (!TryGetCached(player, out var license, out var data)) return false;
            if (!data.IsOnDuty || data.Department != Department.Police) return false;

            officer = new OfficerInfo
            {
                License  = license,
                Name     = data.Name,
                Callsign = _dispatch.GetUnit(ServerId(player))?.Callsign ?? data.Callsign,
            };
            return true;
        }

        private async Task<string?> OwnerLicenseAsync(int characterId) =>
            (await _db.Civilians.GetCharacterAsync(characterId))?.OwnerLicense;

        private async Task NotifyCharacterOwnerAsync(int characterId, string? message)
        {
            if (_civilians is null) return;

            var ownerLicense = await OwnerLicenseAsync(characterId);
            if (ownerLicense is null) return;

            var owner = Players.FirstOrDefault(p => GetLicense(p) == ownerLicense);
            if (owner is null) return;

            if (message is not null) Notify(owner, message);
            var state = await _civilians.GetStateAsync(ownerLicense);
            TriggerClientEvent(owner, EventNames.ClientCivilianState, JsonConvert.SerializeObject(state));
        }
    }
}
