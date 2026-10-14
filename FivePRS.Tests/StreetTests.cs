using System;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Police;
using FivePRS.Server.Civilian;
using Newtonsoft.Json;
using Xunit;

namespace FivePRS.Tests
{
    public sealed class StreetProfilesTests
    {
        [Fact]
        public void Person_SameSeed_IsIdentical()
        {
            var a = StreetProfiles.Person(1234, female: true);
            var b = StreetProfiles.Person(1234, female: true);

            Assert.Equal(JsonConvert.SerializeObject(a), JsonConvert.SerializeObject(b));
            Assert.Equal("Female", a.Gender);
        }

        [Fact]
        public void Person_ProducesValidCharacterFields()
        {
            for (var seed = 0; seed < 500; seed++)
            {
                var person = StreetProfiles.Person(seed, seed % 2 == 0);

                Assert.True(CivilianRules.TryNormalizeName(person.FirstName, out _));
                Assert.True(CivilianRules.TryNormalizeName(person.LastName, out _));
                Assert.True(CivilianRules.TryParseDateOfBirth(person.DateOfBirth, new DateTime(2026, 10, 10), out _));
                Assert.False(person.WillFlee && person.Warrant is null);
            }
        }

        [Fact]
        public void Person_OutcomesHaveSensibleRates()
        {
            var people = Enumerable.Range(0, 4000).Select(seed => StreetProfiles.Person(seed, false)).ToList();

            Assert.InRange(people.Count(p => p.DriverLicense == StreetLicense.Valid) / 40.0, 75, 89);
            Assert.InRange(people.Count(p => p.Warrant is not null) / 40.0, 4, 10);
            Assert.InRange(people.Count(p => p.Contraband is not null) / 40.0, 11, 19);
        }

        [Fact]
        public void Vehicle_SamePlate_IsIdenticalIgnoringCaseAndSpaces()
        {
            var a = StreetProfiles.Vehicle("ab12cd ");
            var b = StreetProfiles.Vehicle(" AB12CD");

            Assert.Equal(JsonConvert.SerializeObject(a), JsonConvert.SerializeObject(b));
            Assert.Equal("AB12CD", a.Plate);
        }

        [Fact]
        public void Vehicle_StolenIsNeverOwnedByDriver()
        {
            for (var i = 0; i < 2000; i++)
            {
                var vehicle = StreetProfiles.Vehicle($"P{i}");
                Assert.False(vehicle.Stolen && vehicle.DriverIsOwner);
                Assert.False(vehicle.Stolen && !vehicle.Registered);
            }
        }

        [Fact]
        public void Citations_ConfigReplacesDefaults()
        {
            var config = JsonConvert.DeserializeObject<CitationsConfig>("{\"offences\":[{\"label\":\"Jaywalking\",\"fine\":50}]}")!;

            Assert.Equal("Jaywalking", Assert.Single(config.Offences).Label);
        }
    }

    [Collection(TestDatabase.Collection)]
    public sealed class StreetServiceTests : IAsyncLifetime
    {
        private TestDatabase _database = null!;
        private StreetService _street = null!;

        public async Task InitializeAsync()
        {
            _database = await TestDatabase.CreateAsync();
            var lookup = new LookupService(_database.Store, _database.Photos, () => new ResourceSettings(), () => new LicensesConfig());
            _street = new StreetService(_database.Store, lookup);
        }

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Identify_CreatesPersonOnceWithLicenseAndWarrant()
        {
            var seed   = Enumerable.Range(0, 1000).First(s => StreetProfiles.Person(s, false) is { Warrant: not null, DriverLicense: StreetLicense.Suspended });
            var person = StreetProfiles.Person(seed, false);

            var (first, error) = await _street.IdentifyAsync(seed, false);
            var (second, _)    = await _street.IdentifyAsync(seed, false);

            Assert.Null(error);
            Assert.Equal(first!.Record!.Character.Id, second!.Record!.Character.Id);
            Assert.Equal(person.FirstName, first.Record.Character.FirstName);
            Assert.True(first.Record.Character.HasActiveWarrant);
            Assert.Equal(LicenseStatus.Suspended, first.Record.Licenses.Single(l => l.Type == "driver").Status);
            Assert.Single(second.Record.Records);
        }

        [Fact]
        public async Task Plate_Registered_CreatesVehicleForOwner()
        {
            var plate = Enumerable.Range(0, 1000).Select(i => $"FP{i}").First(p => StreetProfiles.Vehicle(p) is { Registered: true, DriverIsOwner: true, Stolen: false });

            var (result, error) = await _street.PlateAsync(plate, "Sultan", 42, false);

            Assert.Null(error);
            Assert.Equal(StreetProfiles.Person(42, false).LastName, result!.Record!.Character.LastName);
            var vehicle = Assert.Single(result.Record.Vehicles);
            Assert.Equal(plate, vehicle.Plate);
            Assert.Equal("Sultan", vehicle.Model);
        }

        [Fact]
        public async Task Plate_Unregistered_ReturnsNoRecord()
        {
            var plate = Enumerable.Range(0, 1000).Select(i => $"NR{i}").First(p => !StreetProfiles.Vehicle(p).Registered);

            var (result, error) = await _street.PlateAsync(plate, "Sultan", 1, false);

            Assert.Null(result);
            Assert.Contains(plate, error);
        }

        [Fact]
        public async Task Plate_PlayerRegistration_TakesPriority()
        {
            var ownerId = await _database.Store.CreateCharacterAsync(new CharacterInfo
            {
                OwnerLicense = "license:player", FirstName = "Jane", LastName = "Player", DateOfBirth = "1990-01-01", Gender = "Female",
            });
            await _database.Store.AddVehicleAsync(new VehicleInfo { CharacterId = ownerId, Plate = "JANE1", Model = "Comet" });

            var (result, _) = await _street.PlateAsync("jane1", "Sultan", 7, true);

            Assert.Equal(ownerId, result!.Record!.Character.Id);
        }

        [Fact]
        public async Task Plate_UnreadablePlate_ReturnsError()
        {
            var (result, error) = await _street.PlateAsync("!!!", "Sultan", 1, false);

            Assert.Null(result);
            Assert.NotNull(error);
        }
    }
}
