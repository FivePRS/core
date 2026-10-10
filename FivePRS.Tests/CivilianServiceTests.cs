using System;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Server.Civilian;
using FivePRS.Server.Database;
using Xunit;

namespace FivePRS.Tests
{
    [Collection(TestDatabase.Collection)]
    public sealed class CivilianServiceTests : IAsyncLifetime
    {
        private const string Owner = "license:owner";
        private const string Other = "license:other";

        private readonly ResourceSettings _settings = new() { MaxCharacters = 2, MaxVehiclesPerCharacter = 2 };
        private TestDatabase _database = null!;
        private CivilianStore _store = null!;
        private CivilianService _service = null!;

        public async Task InitializeAsync()
        {
            _database = await TestDatabase.CreateAsync();
            _store = _database.Store;
            _service = new CivilianService(_store, () => _settings, () => new LicensesConfig(), () => new DateTime(2026, 10, 9));
        }

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        private Task<string?> CreateAsync(string owner = Owner, string first = "john", string dob = "1990-05-01") =>
            _service.CreateCharacterAsync(owner, first, "Smith", dob, "male");

        [Fact]
        public async Task CreateCharacter_Valid_BecomesActive()
        {
            Assert.Null(await CreateAsync());

            var state = await _service.GetStateAsync(Owner);
            var character = Assert.Single(state.Characters);
            Assert.Equal("John", character.FirstName);
            Assert.Equal("Male", character.Gender);
            Assert.Equal(character.Id, state.ActiveCharacterId);
        }

        [Theory]
        [InlineData("J0hn", "1990-05-01")]
        [InlineData("John", "1990-13-01")]
        [InlineData("John", "2015-01-01")]
        public async Task CreateCharacter_InvalidInput_IsRejected(string first, string dob)
        {
            Assert.NotNull(await CreateAsync(first: first, dob: dob));
            Assert.Empty((await _service.GetStateAsync(Owner)).Characters);
        }

        [Fact]
        public async Task CreateCharacter_OverLimit_IsRejected()
        {
            Assert.Null(await CreateAsync(first: "One"));
            Assert.Null(await CreateAsync(first: "Two"));
            Assert.NotNull(await CreateAsync(first: "Three"));
        }

        [Fact]
        public async Task SelectCharacter_SwitchesActive_OnlyForOwner()
        {
            await CreateAsync(first: "One");
            await CreateAsync(first: "Two");
            var first = (await _service.GetStateAsync(Owner)).Characters.Single(c => c.FirstName == "One");

            Assert.NotNull(await _service.SelectCharacterAsync(Other, first.Id));
            Assert.Null(await _service.SelectCharacterAsync(Owner, first.Id));
            Assert.Equal(first.Id, (await _service.GetStateAsync(Owner)).ActiveCharacterId);
        }

        [Fact]
        public async Task DeleteCharacter_RemovesLicensesAndVehicles()
        {
            await CreateAsync();
            await _service.ApplyForLicenseAsync(Owner, "driver");
            await _service.RegisterVehicleAsync(Owner, "abc 123", "Sultan");
            var id = (await _service.GetStateAsync(Owner)).ActiveCharacterId!.Value;

            Assert.NotNull(await _service.DeleteCharacterAsync(Other, id));
            Assert.Null(await _service.DeleteCharacterAsync(Owner, id));

            Assert.Empty(await _store.GetLicensesAsync(id));
            Assert.Empty(await _store.GetVehiclesAsync(id));
            Assert.Null(await _store.GetVehicleByPlateAsync("ABC 123"));
        }

        [Fact]
        public async Task ApplyForLicense_FollowsSelfServiceAndStatusRules()
        {
            await CreateAsync();

            Assert.Null(await _service.ApplyForLicenseAsync(Owner, "driver"));
            Assert.NotNull(await _service.ApplyForLicenseAsync(Owner, "driver"));
            Assert.NotNull(await _service.ApplyForLicenseAsync(Owner, "weapon"));
            Assert.NotNull(await _service.ApplyForLicenseAsync(Owner, "spaceship"));

            var id = (await _service.GetStateAsync(Owner)).ActiveCharacterId!.Value;
            await _store.SetLicenseAsync(id, "boat", LicenseStatus.Suspended);
            Assert.NotNull(await _service.ApplyForLicenseAsync(Owner, "boat"));

            var licenses = (await _service.GetStateAsync(Owner)).Licenses;
            Assert.Equal(LicenseStatus.Valid, licenses.Single(l => l.Type == "driver").Status);
            Assert.Equal(LicenseStatus.Suspended, licenses.Single(l => l.Type == "boat").Status);
            Assert.Null(licenses.Single(l => l.Type == "weapon").Status);
        }

        [Fact]
        public async Task RegisterVehicle_EnforcesPlateUniquenessAndLimit()
        {
            await CreateAsync();
            await CreateAsync(owner: Other);

            Assert.Null(await _service.RegisterVehicleAsync(Owner, " abc 123 ", "Sultan"));
            Assert.NotNull(await _service.RegisterVehicleAsync(Owner, "ABC 123", "Sultan"));
            Assert.NotNull(await _service.RegisterVehicleAsync(Other, "abc 123", "Sultan"));
            Assert.NotNull(await _service.RegisterVehicleAsync(Owner, "TOO-LONG1", "Sultan"));
            Assert.Null(await _service.RegisterVehicleAsync(Owner, "XYZ 9", "Banshee"));
            Assert.NotNull(await _service.RegisterVehicleAsync(Owner, "LMN 4", "Comet"));

            var vehicles = (await _service.GetStateAsync(Owner)).Vehicles;
            Assert.Equal(new[] { "ABC 123", "XYZ 9" }, vehicles.Select(v => v.Plate));
        }

        [Fact]
        public async Task VehicleActions_RequireOwnership()
        {
            await CreateAsync();
            await _service.RegisterVehicleAsync(Owner, "ABC 123", "Sultan");
            var vehicle = (await _service.GetStateAsync(Owner)).Vehicles.Single();

            Assert.NotNull(await _service.SetVehicleStolenAsync(Other, vehicle.Id, true));
            Assert.Null(await _service.SetVehicleStolenAsync(Owner, vehicle.Id, true));
            Assert.Equal(VehicleStatus.Stolen, (await _store.GetVehicleAsync(vehicle.Id))!.Status);

            Assert.NotNull(await _service.RemoveVehicleAsync(Other, vehicle.Id));
            Assert.Null(await _service.RemoveVehicleAsync(Owner, vehicle.Id));
            Assert.Null(await _store.GetVehicleAsync(vehicle.Id));
        }
    }
}
