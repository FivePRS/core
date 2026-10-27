using System;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Police;
using FivePRS.Server.Database;

namespace FivePRS.Server.Civilian
{
    public sealed class LookupService
    {
        private const int SearchLimit = 15;

        private readonly CivilianStore _store;
        private readonly PhotoStore _photos;
        private readonly Func<ResourceSettings> _settings;
        private readonly Func<LicensesConfig> _licenses;

        public LookupService(CivilianStore store, PhotoStore photos, Func<ResourceSettings> settings, Func<LicensesConfig> licenses)
        {
            _store    = store;
            _photos   = photos;
            _settings = settings;
            _licenses = licenses;
        }

        public System.Func<string, Task<bool>>? ExternalPlate { get; set; }

        public bool CollectsFines { get; set; }

        public async Task<(LookupResult? Result, string? Error)> SearchByNameAsync(string? term)
        {
            if (!CivilianRules.TryNormalizeSearch(term, out var normalized))
                return (null, $"Enter at least {CivilianRules.MinSearchLength} letters of a name.");

            var results = await _store.SearchCharactersAsync(normalized, SearchLimit);
            return (new LookupResult { Results = results, Query = normalized }, null);
        }

        public async Task<(LookupResult? Result, string? Error)> SearchByPlateAsync(string? plate)
        {
            if (!CivilianRules.TryNormalizePlate(plate, out var normalized))
                return (null, $"Plates are 1 to {CivilianRules.MaxPlateLength} letters and numbers.");

            var vehicle = await _store.GetVehicleByPlateAsync(normalized);
            if (vehicle is null && ExternalPlate is not null && await ExternalPlate(normalized))
                vehicle = await _store.GetVehicleByPlateAsync(normalized);
            if (vehicle is null) return (null, $"No registration found for {normalized}.");

            var record = await BuildRecordAsync(vehicle.CharacterId);
            if (record is null) return (null, $"No registration found for {normalized}.");

            record.MatchedPlate = normalized;
            return (new LookupResult { Record = record, Query = normalized }, null);
        }

        public async Task<PlateCheck> CheckPlateAsync(string plate)
        {
            var vehicle = await _store.GetVehicleByPlateAsync(plate);
            if (vehicle is null && ExternalPlate is not null && await ExternalPlate(plate))
                vehicle = await _store.GetVehicleByPlateAsync(plate);
            if (vehicle is null) return PlateFlags.FromProfile(plate);

            return PlateFlags.FromRecord(plate, vehicle, await BuildRecordAsync(vehicle.CharacterId));
        }

        public async Task<(LookupResult? Result, string? Error)> GetRecordAsync(int characterId)
        {
            var record = await BuildRecordAsync(characterId);
            return record is null ? (null, "Person not found.") : (new LookupResult { Record = record }, null);
        }

        public async Task<string?> IssueRecordAsync(OfficerInfo officer, int characterId, int type, string? description, int fine)
        {
            if (!Enum.IsDefined(typeof(RecordType), type)) return "Unknown record type.";
            if (!RecordAccess.CanIssue(officer.Department, (RecordType)type)) return "Your department can't add that kind of entry.";
            var character = await _store.GetCharacterAsync(characterId);
            if (character is null) return "Person not found.";

            if (!CivilianRules.TryNormalizeDescription(description, out var text))
                return $"Enter a description of up to {CivilianRules.MaxDescriptionLength} characters.";

            var recordType = (RecordType)type;
            if (recordType == RecordType.Citation)
            {
                if (fine < 0 || fine > _settings().MaxFine) return $"Fines must be between 0 and {_settings().MaxFine}.";
            }
            else
            {
                fine = 0;
            }

            await _store.AddRecordAsync(new RecordInfo
            {
                CharacterId     = characterId,
                Type            = recordType,
                Description     = text,
                Fine            = fine,
                OfficerName     = officer.Name,
                OfficerCallsign = officer.Callsign,
                Active          = recordType == RecordType.Warrant,
                FineStatus      = fine > 0 && CollectsFines && character.ExternalId is not null ? FineStatus.Unpaid : FineStatus.None,
            }, officer.License);

            return null;
        }

        public async Task<(int? CharacterId, string? Error)> ResolveWarrantAsync(OfficerInfo officer, int recordId, bool served)
        {
            var record = await _store.GetRecordAsync(recordId);
            if (record is null || record.Type != RecordType.Warrant) return (null, "Warrant not found.");
            if (!record.Active) return (record.CharacterId, "That warrant is no longer active.");

            await _store.ResolveRecordAsync(recordId, $"{(served ? "Served" : "Cleared")} by {officer.Callsign}");
            return (record.CharacterId, null);
        }

        public async Task<string?> SetLicenseStatusAsync(int characterId, string? type, int status)
        {
            if (await _store.GetCharacterAsync(characterId) is null) return "Person not found.";
            if (!Enum.IsDefined(typeof(LicenseStatus), status)) return "Unknown license status.";

            var definition = _licenses().Find(type);
            if (definition is null) return "Unknown license type.";

            await _store.SetLicenseAsync(characterId, definition.Id, (LicenseStatus)status);
            return null;
        }

        public async Task<(int? CharacterId, string? Error)> SetVehicleStolenAsync(int vehicleId, bool stolen)
        {
            var vehicle = await _store.GetVehicleAsync(vehicleId);
            if (vehicle is null) return (null, "Vehicle not found.");

            await _store.SetVehicleStatusAsync(vehicleId, stolen ? VehicleStatus.Stolen : VehicleStatus.Valid);
            return (vehicle.CharacterId, null);
        }

        private async Task<LookupRecord?> BuildRecordAsync(int characterId)
        {
            var summary = await _store.GetSummaryAsync(characterId);
            if (summary is null) return null;

            var held = await _store.GetLicensesAsync(characterId);
            summary.Photo = await _photos.GetAsync(characterId);

            return new LookupRecord
            {
                Character = summary,
                Licenses  = LicenseViews.Build(characterId, _licenses(), held),
                Vehicles  = await _store.GetVehiclesAsync(characterId),
                Records   = await _store.GetRecordsAsync(characterId),
            };
        }
    }
}
