using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Police;
using FivePRS.Server.Civilian;
using Xunit;

namespace FivePRS.Tests
{
    public sealed class RadarSettingsTests
    {
        [Fact]
        public void ToDisplaySpeed_ConvertsToTheChosenUnits()
        {
            Assert.Equal(45, new RadarSettings { Units = "mph" }.ToDisplaySpeed(20f));
            Assert.Equal(72, new RadarSettings { Units = "kmh" }.ToDisplaySpeed(20f));
            Assert.Equal(72, new RadarSettings { Units = "KM/H" }.ToDisplaySpeed(20f));
            Assert.Equal(0, new RadarSettings().ToDisplaySpeed(0f));
        }
    }

    public sealed class PlateFlagsTests
    {
        [Fact]
        public void FromProfile_MatchesTheStreetProfile()
        {
            var unregistered = Enumerable.Range(0, 2000).Select(i => $"UR{i}").First(p => !StreetProfiles.Vehicle(p).Registered);
            Assert.Equal(new[] { PlateFlag.Unregistered }, PlateFlags.FromProfile(unregistered).Flags);

            var stolen = Enumerable.Range(0, 5000).Select(i => $"ST{i}").First(p => StreetProfiles.Vehicle(p).Stolen);
            Assert.Contains(PlateFlag.Stolen, PlateFlags.FromProfile(stolen).Flags);

            var wanted = Enumerable.Range(0, 5000).Select(i => $"WA{i}").First(p =>
            {
                var vehicle = StreetProfiles.Vehicle(p);
                return vehicle.Registered && !vehicle.DriverIsOwner && StreetProfiles.Person(vehicle.OwnerSeed, vehicle.OwnerFemale).Warrant is not null;
            });
            Assert.Contains(PlateFlag.Warrant, PlateFlags.FromProfile(wanted).Flags);

            var driverOwned = Enumerable.Range(0, 2000).Select(i => $"DO{i}").First(p => StreetProfiles.Vehicle(p) is { Registered: true, Stolen: false, DriverIsOwner: true });
            Assert.Empty(PlateFlags.FromProfile(driverOwned).Flags);
        }
    }

    [Collection(TestDatabase.Collection)]
    public sealed class PlateCheckTests : IAsyncLifetime
    {
        private TestDatabase _database = null!;
        private LookupService _lookup = null!;

        public async Task InitializeAsync()
        {
            _database = await TestDatabase.CreateAsync();
            _lookup   = new LookupService(_database.Store, _database.Photos, () => new ResourceSettings(), () => new LicensesConfig());
        }

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task CheckPlate_UsesRecordsForKnownPlates()
        {
            var owner = await _database.Store.CreateCharacterAsync(new CharacterInfo
            {
                OwnerLicense = "license:player", FirstName = "Jane", LastName = "Doe", DateOfBirth = "1990-01-01", Gender = "Female",
            });
            var vehicle = await _database.Store.AddVehicleAsync(new VehicleInfo { CharacterId = owner, Plate = "JANE1", Model = "Comet" });
            await _database.Store.SetLicenseAsync(owner, StreetProfiles.DriverLicenseType, LicenseStatus.Suspended);

            var clean = await _lookup.CheckPlateAsync("JANE1");
            Assert.Equal("Jane Doe", clean.Owner);
            Assert.Equal(new[] { PlateFlag.Suspended }, clean.Flags);

            await _database.Store.SetVehicleStatusAsync(vehicle, VehicleStatus.Stolen);
            await _database.Store.AddRecordAsync(new RecordInfo { CharacterId = owner, Type = RecordType.Warrant, Description = "FTA", Active = true }, "license:cop");

            var flagged = await _lookup.CheckPlateAsync("JANE1");
            Assert.Contains(PlateFlag.Stolen, flagged.Flags);
            Assert.Contains(PlateFlag.Warrant, flagged.Flags);
        }

        [Fact]
        public async Task CheckPlate_DoesNotSaveUnknownPlates()
        {
            await _lookup.CheckPlateAsync("NPC123");
            Assert.Null(await _database.Store.GetVehicleByPlateAsync("NPC123"));
        }
    }
}
