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
    public sealed class LookupServiceTests : IAsyncLifetime
    {
        private const string Owner = "license:civ";

        private static readonly OfficerInfo Officer = new() { License = "license:cop", Name = "Officer Pixel", Callsign = "1-ADAM-12" };

        private readonly ResourceSettings _settings = new() { MaxCharacters = 3, MaxVehiclesPerCharacter = 3, MaxFine = 500 };
        private TestDatabase _database = null!;
        private CivilianStore _store = null!;
        private CivilianService _civilians = null!;
        private LookupService _lookup = null!;
        private int _characterId;

        public async Task InitializeAsync()
        {
            _database  = await TestDatabase.CreateAsync();
            _store     = _database.Store;
            _civilians = new CivilianService(_store, () => _settings, () => new LicensesConfig(), () => new DateTime(2026, 10, 9));
            _lookup    = new LookupService(_store, () => _settings, () => new LicensesConfig());

            await _civilians.CreateCharacterAsync(Owner, "Trevor", "Philips", "1967-01-01", "Male");
            await _civilians.RegisterVehicleAsync(Owner, "TP 1", "Bodhi");
            _characterId = (await _civilians.GetStateAsync(Owner)).ActiveCharacterId!.Value;
        }

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        [Theory]
        [InlineData("trev")]
        [InlineData("PHILIPS")]
        [InlineData("Trevor Phil")]
        public async Task SearchByName_FindsCharacter(string term)
        {
            var (result, error) = await _lookup.SearchByNameAsync(term);

            Assert.Null(error);
            var match = Assert.Single(result!.Results!);
            Assert.Equal(_characterId, match.Id);
            Assert.False(match.HasActiveWarrant);
        }

        [Theory]
        [InlineData("t")]
        [InlineData("tr%")]
        public async Task SearchByName_InvalidTerm_IsRejected(string term)
        {
            var (result, error) = await _lookup.SearchByNameAsync(term);

            Assert.Null(result);
            Assert.NotNull(error);
        }

        [Fact]
        public async Task SearchByPlate_ReturnsOwnerRecord()
        {
            var (result, error) = await _lookup.SearchByPlateAsync("tp 1");

            Assert.Null(error);
            Assert.Equal("TP 1", result!.Record!.MatchedPlate);
            Assert.Equal("Trevor", result.Record.Character.FirstName);
            Assert.Single(result.Record.Vehicles);

            Assert.NotNull((await _lookup.SearchByPlateAsync("NOPE 1")).Error);
        }

        [Fact]
        public async Task IssueRecord_ValidatesAndStoresOfficerDetails()
        {
            Assert.NotNull(await _lookup.IssueRecordAsync(Officer, _characterId, 99, "Speeding", 100));
            Assert.NotNull(await _lookup.IssueRecordAsync(Officer, _characterId, (int)RecordType.Citation, "  ", 100));
            Assert.NotNull(await _lookup.IssueRecordAsync(Officer, _characterId, (int)RecordType.Citation, "Speeding", 501));
            Assert.NotNull(await _lookup.IssueRecordAsync(Officer, 9999, (int)RecordType.Warning, "Loitering", 0));

            Assert.Null(await _lookup.IssueRecordAsync(Officer, _characterId, (int)RecordType.Citation, "Speeding 80 in a 50", 250));
            Assert.Null(await _lookup.IssueRecordAsync(Officer, _characterId, (int)RecordType.Arrest, "Assault", 300));

            var records = (await _lookup.GetRecordAsync(_characterId)).Result!.Record!.Records;
            Assert.Equal(2, records.Count);
            var citation = records.Single(r => r.Type == RecordType.Citation);
            Assert.Equal(250, citation.Fine);
            Assert.Equal("1-ADAM-12", citation.OfficerCallsign);
            Assert.Equal(0, records.Single(r => r.Type == RecordType.Arrest).Fine);
        }

        [Fact]
        public async Task Warrant_IsActiveUntilServed_AndBlocksCharacterDeletion()
        {
            Assert.Null(await _lookup.IssueRecordAsync(Officer, _characterId, (int)RecordType.Warrant, "Failure to appear", 0));

            Assert.True(Assert.Single((await _lookup.SearchByNameAsync("Trevor")).Result!.Results!).HasActiveWarrant);
            Assert.NotNull(await _civilians.DeleteCharacterAsync(Owner, _characterId));

            var warrant = (await _lookup.GetRecordAsync(_characterId)).Result!.Record!.Records.Single();
            var (characterId, error) = await _lookup.ResolveWarrantAsync(Officer, warrant.Id, served: true);
            Assert.Null(error);
            Assert.Equal(_characterId, characterId);
            Assert.NotNull((await _lookup.ResolveWarrantAsync(Officer, warrant.Id, served: false)).Error);

            var resolved = (await _lookup.GetRecordAsync(_characterId)).Result!.Record!.Records.Single();
            Assert.False(resolved.Active);
            Assert.Equal("Served by 1-ADAM-12", resolved.Resolution);
            Assert.False(Assert.Single((await _lookup.SearchByNameAsync("Trevor")).Result!.Results!).HasActiveWarrant);
            Assert.Null(await _civilians.DeleteCharacterAsync(Owner, _characterId));
        }

        [Fact]
        public async Task OfficerLicenseAndVehicleActions_UpdateRecord()
        {
            Assert.Null(await _lookup.SetLicenseStatusAsync(_characterId, "weapon", (int)LicenseStatus.Valid));
            Assert.Null(await _lookup.SetLicenseStatusAsync(_characterId, "weapon", (int)LicenseStatus.Revoked));
            Assert.NotNull(await _lookup.SetLicenseStatusAsync(_characterId, "spaceship", (int)LicenseStatus.Valid));
            Assert.NotNull(await _lookup.SetLicenseStatusAsync(_characterId, "weapon", 7));

            var vehicle = (await _lookup.GetRecordAsync(_characterId)).Result!.Record!.Vehicles.Single();
            Assert.Equal(_characterId, (await _lookup.SetVehicleStolenAsync(vehicle.Id, true)).CharacterId);

            var record = (await _lookup.GetRecordAsync(_characterId)).Result!.Record!;
            Assert.Equal(LicenseStatus.Revoked, record.Licenses.Single(l => l.Type == "weapon").Status);
            Assert.Equal(VehicleStatus.Stolen, record.Vehicles.Single().Status);

            var civilianView = await _civilians.GetStateAsync(Owner);
            Assert.Equal(LicenseStatus.Revoked, civilianView.Licenses.Single(l => l.Type == "weapon").Status);
        }
    }
}
