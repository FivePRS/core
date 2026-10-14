using System;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Models;
using FivePRS.Core.Police;
using FivePRS.Server.Civilian;
using Xunit;

namespace FivePRS.Tests
{
    [Collection(TestDatabase.Collection)]
    public sealed class ReportServiceTests : IAsyncLifetime
    {
        private static readonly OfficerInfo Medic = new() { License = "license:medic", Name = "Medic Pixel", Callsign = "MEDIC-1", Department = Department.EMS };
        private static readonly OfficerInfo Partner = new() { License = "license:partner", Name = "Medic Two", Callsign = "MEDIC-2", Department = Department.EMS };
        private static readonly OfficerInfo Outsider = new() { License = "license:other", Name = "Medic Three", Callsign = "MEDIC-3", Department = Department.EMS };
        private static readonly OfficerInfo Firefighter = new() { License = "license:fire", Name = "Fire Pixel", Callsign = "ENGINE-1", Department = Department.Fire };
        private static readonly OfficerInfo Officer = new() { License = "license:cop", Name = "Officer Pixel", Callsign = "1-ADAM-12", Department = Department.Police };

        private static readonly CrewMember[] Crew = { new(Medic.License, Medic.Callsign), new(Partner.License, Partner.Callsign) };

        private TestDatabase _database = null!;
        private LookupService _lookup = null!;
        private ReportService _reports = null!;

        public async Task InitializeAsync()
        {
            _database = await TestDatabase.CreateAsync();
            _lookup   = new LookupService(_database.Store, _database.Photos, () => new ResourceSettings(), () => new LicensesConfig());
            _reports  = new ReportService(_database.Reports, _database.Store, new StreetService(_database.Store, _lookup));
        }

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        private static ReportDraft PatientDraft(int seed = 7) => new()
        {
            Kind          = ReportKind.Patient,
            CallId        = "1001",
            Title         = "Medical Emergency",
            Location      = "Vespucci Blvd, Little Seoul",
            Narrative     = "Patient treated and transported.",
            PatientSeed   = seed,
            PatientFemale = true,
        };

        [Fact]
        public async Task Draft_LinksPatientAndCrew()
        {
            var (report, error) = await _reports.DraftAsync(Medic, PatientDraft(), Crew);

            Assert.Null(error);
            Assert.Equal(ReportStatus.Draft, report!.Status);
            Assert.Equal(StreetProfiles.Person(7, true).FullName, report.PersonName);
            Assert.Equal("MEDIC-1, MEDIC-2", report.Crew);
            Assert.Single(await _reports.DraftsForAsync(Partner.License));
            Assert.Empty(await _reports.DraftsForAsync(Outsider.License));
        }

        [Fact]
        public async Task Draft_RefusesWrongDepartmentOrNoCrew()
        {
            Assert.NotNull((await _reports.DraftAsync(Officer, PatientDraft(), Crew)).Error);
            Assert.NotNull((await _reports.DraftAsync(Medic, new ReportDraft { Kind = ReportKind.Incident, CallId = "1002" }, Crew)).Error);
            Assert.NotNull((await _reports.DraftAsync(Medic, PatientDraft(), new CrewMember[0])).Error);
            Assert.Null((await _reports.DraftAsync(Firefighter, new ReportDraft { Kind = ReportKind.Incident, CallId = "1003", Plate = "ab 12" }, new[] { new CrewMember(Firefighter.License, Firefighter.Callsign) })).Error);
        }

        [Fact]
        public async Task File_OnlyCrewAndAddsMedicalEntry()
        {
            var (report, _) = await _reports.DraftAsync(Medic, PatientDraft(), Crew);

            Assert.NotNull(await _reports.SaveAsync(Outsider, report!.Id, "Edited"));
            Assert.NotNull(await _reports.SaveAsync(Partner, report.Id, "   "));
            Assert.Null(await _reports.SaveAsync(Partner, report.Id, "Edited by my partner."));

            var (filed, error) = await _reports.FileAsync(Medic, report.Id, "Final narrative.");
            Assert.Null(error);
            Assert.Equal(ReportStatus.Filed, filed!.Status);
            Assert.Equal("Medic Pixel (MEDIC-1)", filed.FiledBy);
            Assert.Empty(await _reports.DraftsForAsync(Medic.License));
            Assert.NotNull((await _reports.FileAsync(Partner, report.Id, "Again.")).Error);

            var record = (await _lookup.GetRecordAsync(filed.CharacterId!.Value)).Result!.Record!;
            var entry = record.Records.Single(r => r.Type == RecordType.Medical && r.OfficerCallsign == "MEDIC-1");
            Assert.Contains("Final narrative.", entry.Description);
        }

        [Fact]
        public async Task FileStale_FilesOldDraftsOnly()
        {
            var (report, _) = await _reports.DraftAsync(Medic, PatientDraft(), Crew);

            Assert.Empty(await _reports.FileStaleAsync(DateTime.UtcNow));
            var filed = await _reports.FileStaleAsync(DateTime.UtcNow.AddMinutes(ReportRules.AutoFileMinutes + 1));

            Assert.Equal(report!.Id, Assert.Single(filed).Id);
            Assert.Empty(await _reports.DraftsForAsync(Medic.License));
        }

        [Fact]
        public async Task Search_FindsFiledByTermAndDate()
        {
            var (report, _) = await _reports.DraftAsync(Medic, PatientDraft(), Crew);
            await _reports.FileAsync(Medic, report!.Id, "Done.");
            await _reports.DraftAsync(Medic, PatientDraft(8), Crew);

            var (byTerm, _) = await _reports.SearchAsync(Firefighter, "little seoul", null);
            Assert.Equal(report.Id, Assert.Single(byTerm!.Results!).Id);

            var (byDate, _) = await _reports.SearchAsync(Medic, "", DateTime.UtcNow.ToString("yyyy-MM-dd"));
            Assert.Single(byDate!.Results!);

            var (none, _) = await _reports.SearchAsync(Medic, "100%_", null);
            Assert.Empty(none!.Results!);

            Assert.NotNull((await _reports.SearchAsync(Officer, "", null)).Error);
            Assert.NotNull((await _reports.SearchAsync(Medic, "", "13/10/2026")).Error);
        }
    }
}
