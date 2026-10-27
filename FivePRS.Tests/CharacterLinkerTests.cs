using System;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Server.Civilian;
using FivePRS.Server.Framework;
using Xunit;

namespace FivePRS.Tests
{
    [Collection(TestDatabase.Collection)]
    public sealed class CharacterLinkerTests : IAsyncLifetime
    {
        private const string License = "license:player";

        private TestDatabase _database = null!;
        private CharacterLinker _linker = null!;
        private CivilianService _civilians = null!;
        private LookupService _lookup = null!;

        public async Task InitializeAsync()
        {
            _database  = await TestDatabase.CreateAsync();
            _linker    = new CharacterLinker(_database.Store);
            _civilians = new CivilianService(_database.Store, _database.Photos, () => new ResourceSettings(), () => new LicensesConfig(), () => new DateTime(2026, 10, 26));
            _lookup    = new LookupService(_database.Store, _database.Photos, () => new ResourceSettings(), () => new LicensesConfig());
        }

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        private static ExternalIdentity Identity(string? license = License, string first = "Jane") => new()
        {
            ExternalId = "qb:ABC123", OwnerLicense = license, FirstName = first, LastName = "Doe", DateOfBirth = "1990-05-04", Gender = "female",
        };

        [Fact]
        public async Task Link_CreatesOnceAndUpdatesDetails()
        {
            var first = await _linker.LinkAsync(Identity());
            var again = await _linker.LinkAsync(Identity(first: "Janet"));

            Assert.Equal(first, again);
            var character = await _database.Store.GetCharacterAsync(first);
            Assert.Equal("Janet", character!.FirstName);
            Assert.Equal("Female", character.Gender);
            Assert.Equal("qb:ABC123", character.ExternalId);
            Assert.Equal(License, character.OwnerLicense);
        }

        [Fact]
        public async Task Link_KeepsTheKnownOwnerWhenTheFrameworkDoesNotSay()
        {
            var placeholder = await _linker.LinkAsync(Identity(license: null));
            Assert.StartsWith(CharacterLinker.PlaceholderOwner, (await _database.Store.GetCharacterAsync(placeholder))!.OwnerLicense);

            await _linker.LinkAsync(Identity());
            await _linker.LinkAsync(Identity(license: null));
            Assert.Equal(License, (await _database.Store.GetCharacterAsync(placeholder))!.OwnerLicense);
        }

        [Fact]
        public async Task Link_FillsInMissingDetails()
        {
            var id = await _linker.LinkAsync(new ExternalIdentity { ExternalId = "esx:char1:abc", DateOfBirth = "not a date", Gender = "?" });
            var character = await _database.Store.GetCharacterAsync(id);

            Assert.Equal(CharacterLinker.UnknownName, character!.FirstName);
            Assert.Equal(string.Empty, character.DateOfBirth);
            Assert.Equal("Male", character.Gender);
        }

        [Fact]
        public async Task SyncVehicles_AddsMovesAndRemoves()
        {
            var jane = await _linker.LinkAsync(Identity());
            var other = await _linker.LinkAsync(new ExternalIdentity { ExternalId = "qb:OTHER", OwnerLicense = "license:other", FirstName = "Sam", LastName = "Roe" });
            await _linker.LinkVehicleAsync(other, new ExternalVehicle { Plate = "sold1", Model = "sultan" });

            await _linker.SyncVehiclesAsync(jane, new[]
            {
                new ExternalVehicle { Plate = "abc 123", Model = "adder" },
                new ExternalVehicle { Plate = "SOLD1", Model = "sultan" },
                new ExternalVehicle { Plate = "!!", Model = "bad" },
            });

            var vehicles = await _database.Store.GetVehiclesAsync(jane);
            Assert.Equal(new[] { "ABC 123", "SOLD1" }, vehicles.Select(v => v.Plate).OrderBy(p => p));
            Assert.Empty(await _database.Store.GetVehiclesAsync(other));

            await _linker.SyncVehiclesAsync(jane, new[] { new ExternalVehicle { Plate = "ABC 123" } });
            var remaining = Assert.Single(await _database.Store.GetVehiclesAsync(jane));
            Assert.Equal(CharacterLinker.UnknownModel, remaining.Model);
        }

        [Fact]
        public async Task ManagedMode_ShowsOnlyTheLinkedCharacterAndLocksEdits()
        {
            await _civilians.CreateCharacterAsync(License, "Old", "Standalone", "1980-01-01", "Male");
            _civilians.ManagedBy = "QBCore";

            var empty = await _civilians.GetStateAsync(License);
            Assert.Empty(empty.Characters);
            Assert.Null(empty.ActiveCharacterId);
            Assert.Equal("QBCore", empty.ManagedBy);

            var linked = await _linker.LinkAsync(Identity());
            await _database.Store.TouchCharacterAsync(linked);
            var state = await _civilians.GetStateAsync(License);
            Assert.Equal(linked, Assert.Single(state.Characters).Id);
            Assert.Equal(linked, state.ActiveCharacterId);

            Assert.NotNull(await _civilians.CreateCharacterAsync(License, "New", "Person", "1990-01-01", "Male"));
            Assert.NotNull(await _civilians.DeleteCharacterAsync(License, linked));
            Assert.NotNull(await _civilians.RegisterVehicleAsync(License, "NEW1", "Adder"));
        }

        [Fact]
        public async Task PlateLookup_AsksTheFrameworkForUnknownPlates()
        {
            var asked = 0;
            _lookup.ExternalPlate = async plate =>
            {
                asked++;
                if (plate != "FW 1") return false;
                var owner = await _linker.LinkAsync(Identity());
                return await _linker.LinkVehicleAsync(owner, new ExternalVehicle { Plate = plate, Model = "adder" });
            };

            var (found, error) = await _lookup.SearchByPlateAsync("fw 1");
            Assert.Null(error);
            Assert.Equal("Jane", found!.Record!.Character.FirstName);

            var (missing, missingError) = await _lookup.SearchByPlateAsync("NOPE");
            Assert.Null(missing);
            Assert.Contains("NOPE", missingError);

            await _lookup.SearchByPlateAsync("FW 1");
            Assert.Equal(2, asked);
        }

        [Fact]
        public async Task Citations_OnLinkedCharactersAreUnpaidUntilCollected()
        {
            var officer = new OfficerInfo { License = "license:cop", Name = "Officer", Callsign = "1-ADAM-12" };
            var linked = await _linker.LinkAsync(Identity());
            await _civilians.CreateCharacterAsync("license:solo", "Solo", "Player", "1990-01-01", "Male");
            var standalone = (await _database.Store.GetCharactersAsync("license:solo")).Single().Id;

            _lookup.CollectsFines = true;
            Assert.Null(await _lookup.IssueRecordAsync(officer, linked, (int)RecordType.Citation, "Speeding", 250));
            Assert.Null(await _lookup.IssueRecordAsync(officer, linked, (int)RecordType.Warning, "Loitering", 0));
            Assert.Null(await _lookup.IssueRecordAsync(officer, standalone, (int)RecordType.Citation, "Speeding", 250));

            var unpaid = Assert.Single(await _database.Store.GetUnpaidFinesAsync(linked));
            Assert.Equal(250, unpaid.Fine);
            Assert.Empty(await _database.Store.GetUnpaidFinesAsync(standalone));

            await _database.Store.SetFineStatusAsync(unpaid.Id, FineStatus.Paid);
            Assert.Empty(await _database.Store.GetUnpaidFinesAsync(linked));
            Assert.Equal(FineStatus.Paid, (await _database.Store.GetRecordsAsync(linked)).Single(r => r.Type == RecordType.Citation).FineStatus);

            _lookup.CollectsFines = false;
            Assert.Null(await _lookup.IssueRecordAsync(officer, linked, (int)RecordType.Citation, "Parking", 50));
            Assert.Empty(await _database.Store.GetUnpaidFinesAsync(linked));
        }
    }
}
