using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Police;
using FivePRS.Server.Database;

namespace FivePRS.Server.Civilian
{
    public sealed class StreetService
    {
        private const int MaxModelLength = 40;
        private const string DispatchName = "Dispatch";
        private const string DispatchCallsign = "DISPATCH";
        private const string HospitalName = "Pillbox Hill Medical Center";
        private const string HospitalCallsign = "PILLBOX";

        private readonly CivilianStore _store;
        private readonly LookupService _lookup;

        public StreetService(CivilianStore store, LookupService lookup)
        {
            _store  = store;
            _lookup = lookup;
        }

        public async Task<(LookupResult? Result, string? Error)> IdentifyAsync(int seed, bool female)
        {
            var id = await GetOrCreateAsync(StreetProfiles.Person(seed, female));
            return await _lookup.GetRecordAsync(id);
        }

        public Task<(LookupResult? Result, string? Error)> PlateAsync(string? plate, string? model, int driverSeed, bool driverFemale) =>
            PlateAsync(plate, model, StreetProfiles.Person(driverSeed, driverFemale));

        public async Task<(LookupResult? Result, string? Error)> PlateAsync(string? plate, string? model, StreetPerson? driver)
        {
            if (!CivilianRules.TryNormalizePlate(plate, out var normalized)) return (null, "That plate can't be read.");

            if (await _store.GetVehicleByPlateAsync(normalized) is null && !(_lookup.ExternalPlate is not null && await _lookup.ExternalPlate(normalized)))
            {
                var vehicle = StreetProfiles.Vehicle(normalized);
                if (!vehicle.Registered) return (null, $"No registration found for {normalized}.");

                var owner = vehicle.DriverIsOwner && driver is not null
                    ? driver
                    : StreetProfiles.Person(vehicle.OwnerSeed, vehicle.OwnerFemale);

                await _store.AddVehicleAsync(new VehicleInfo
                {
                    CharacterId = await GetOrCreateAsync(owner),
                    Plate       = normalized,
                    Model       = NormalizeModel(model),
                    Status      = vehicle.Stolen ? VehicleStatus.Stolen : VehicleStatus.Valid,
                });
            }

            return await _lookup.SearchByPlateAsync(normalized);
        }

        private async Task<int> GetOrCreateAsync(StreetPerson person)
        {
            var existing = await _store.FindCharacterAsync(StreetProfiles.NpcOwner, person.FirstName, person.LastName, person.DateOfBirth);
            if (existing is not null) return existing.Id;

            var id = await _store.CreateCharacterAsync(new CharacterInfo
            {
                OwnerLicense = StreetProfiles.NpcOwner,
                FirstName    = person.FirstName,
                LastName     = person.LastName,
                DateOfBirth  = person.DateOfBirth,
                Gender       = person.Gender,
            });

            var license = StreetProfiles.ToLicenseStatus(person.DriverLicense);
            if (license is not null)
                await _store.SetLicenseAsync(id, StreetProfiles.DriverLicenseType, license.Value);

            if (person.Warrant is not null)
            {
                await _store.AddRecordAsync(new RecordInfo
                {
                    CharacterId     = id,
                    Type            = RecordType.Warrant,
                    Description     = person.Warrant,
                    OfficerName     = DispatchName,
                    OfficerCallsign = DispatchCallsign,
                    Active          = true,
                }, StreetProfiles.NpcOwner);
            }

            if (person.Medical is not null)
            {
                await _store.AddRecordAsync(new RecordInfo
                {
                    CharacterId     = id,
                    Type            = RecordType.Medical,
                    Description     = person.Medical,
                    OfficerName     = HospitalName,
                    OfficerCallsign = HospitalCallsign,
                }, StreetProfiles.NpcOwner);
            }

            return id;
        }

        private static string NormalizeModel(string? model)
        {
            var text = (model ?? string.Empty).Trim();
            return text.Length > 0 && text.Length <= MaxModelLength && text.All(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-')
                ? text
                : "Unknown";
        }
    }
}
