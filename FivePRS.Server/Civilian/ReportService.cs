using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Models;
using FivePRS.Server.Database;

namespace FivePRS.Server.Civilian
{
    public sealed class CrewMember
    {
        public CrewMember(string license, string callsign)
        {
            License  = license;
            Callsign = callsign;
        }

        public string License { get; }
        public string Callsign { get; }
    }

    public sealed class ReportService
    {
        public const string AutoFiled = "Filed automatically";

        private const int SearchLimit = 25;
        private const string DefaultNarrative = "No narrative was written for this report.";

        private static readonly ReportKind[] AllKinds = { ReportKind.Patient, ReportKind.Incident };

        private readonly ReportStore _reports;
        private readonly CivilianStore _store;
        private readonly StreetService _street;

        public ReportService(ReportStore reports, CivilianStore store, StreetService street)
        {
            _reports = reports;
            _store   = store;
            _street  = street;
        }

        public static bool CanWrite(Department department, ReportKind kind) => kind switch
        {
            ReportKind.Patient  => department == Department.EMS || department == Department.Fire,
            ReportKind.Incident => department == Department.Fire,
            _                   => false,
        };

        public static bool CanRead(Department department) => department == Department.EMS || department == Department.Fire;

        public async Task<(ReportInfo? Report, string? Error)> DraftAsync(OfficerInfo author, ReportDraft draft, IReadOnlyList<CrewMember> crew)
        {
            if (!Enum.IsDefined(typeof(ReportKind), draft.Kind) || !CanWrite(author.Department, draft.Kind)) return (null, "Your department doesn't write that kind of report.");
            if (crew.Count == 0) return (null, "You weren't on that call.");

            int? characterId = null;
            if (draft.Kind == ReportKind.Patient && draft.PatientSeed is { } seed)
            {
                var (identity, _) = await _street.IdentifyAsync(seed, draft.PatientFemale);
                characterId = identity?.Record?.Character.Id;
            }

            var report = new ReportInfo
            {
                Kind           = draft.Kind,
                Department     = author.Department,
                CallId         = draft.CallId,
                Title          = ReportRules.NormalizeLine(draft.Title, ReportRules.MaxTitleLength, draft.Kind == ReportKind.Patient ? "Patient care" : "Incident"),
                Location       = ReportRules.NormalizeLine(draft.Location, ReportRules.MaxLocationLength, "Unknown location"),
                Narrative      = ReportRules.TryNormalizeNarrative(draft.Narrative, out var narrative) ? narrative : DefaultNarrative,
                CharacterId    = characterId,
                Plate          = CivilianRules.TryNormalizePlate(draft.Plate, out var plate) ? plate : null,
                Crew           = string.Join(", ", crew.Select(member => member.Callsign).Where(callsign => callsign.Length > 0)),
                AuthorName     = author.Name,
                AuthorCallsign = author.Callsign,
            };

            report.Id = await _reports.AddAsync(report, author.License, crew.Select(member => member.License));
            return (await _reports.GetAsync(report.Id), null);
        }

        public Task<List<ReportInfo>> DraftsForAsync(string license) => _reports.GetDraftsForAsync(license);

        public Task<List<string>> CrewOfAsync(int reportId) => _reports.CrewLicensesAsync(reportId);

        public async Task<string?> SaveAsync(OfficerInfo editor, int reportId, string? narrative)
        {
            var (report, error) = await EditableAsync(editor, reportId);
            if (report is null) return error;
            if (!ReportRules.TryNormalizeNarrative(narrative, out var text)) return NarrativeError;

            await _reports.SaveNarrativeAsync(reportId, text);
            return null;
        }

        public async Task<(ReportInfo? Report, string? Error)> FileAsync(OfficerInfo filer, int reportId, string? narrative)
        {
            var (report, error) = await EditableAsync(filer, reportId);
            if (report is null) return (null, error);
            if (!ReportRules.TryNormalizeNarrative(narrative, out var text)) return (null, NarrativeError);

            report.Narrative = text;
            await FileAsync(report, filer.License, filer.Name, filer.Callsign, $"{filer.Name} ({filer.Callsign})");
            return (await _reports.GetAsync(reportId), null);
        }

        public async Task<List<ReportInfo>> FileStaleAsync(DateTime now)
        {
            var stale = await _reports.GetStaleDraftsAsync(now.AddMinutes(-ReportRules.AutoFileMinutes));
            foreach (var report in stale)
            {
                var license = (await _reports.CrewLicensesAsync(report.Id)).FirstOrDefault() ?? string.Empty;
                await FileAsync(report, license, report.AuthorName, report.AuthorCallsign, AutoFiled);
            }

            return stale;
        }

        public async Task<(ReportsView? View, string? Error)> SearchAsync(OfficerInfo reader, string? term, string? date)
        {
            if (!CanRead(reader.Department)) return (null, "Reports are for Fire and EMS.");
            if (!ReportRules.TryParseDate(date, out var day)) return (null, "Dates look like 2026-10-13.");

            var query = ReportRules.NormalizeSearch(term);
            var results = await _reports.SearchAsync(query, day, AllKinds, SearchLimit);
            return (new ReportsView
            {
                Drafts  = await _reports.GetDraftsForAsync(reader.License),
                Results = results,
                Query   = query,
                Date    = day?.ToString("yyyy-MM-dd"),
            }, null);
        }

        private static string NarrativeError => $"Write a narrative of up to {ReportRules.MaxNarrativeLength} characters.";

        private async Task<(ReportInfo? Report, string? Error)> EditableAsync(OfficerInfo editor, int reportId)
        {
            var report = await _reports.GetAsync(reportId);
            if (report is null || !await _reports.IsCrewAsync(reportId, editor.License)) return (null, "Report not found.");
            if (report.Status != ReportStatus.Draft) return (null, "That report has already been filed.");
            return (report, null);
        }

        private async Task FileAsync(ReportInfo report, string license, string name, string callsign, string filedBy)
        {
            await _reports.FileAsync(report.Id, report.Narrative, filedBy);

            if (report.Kind != ReportKind.Patient || report.CharacterId is not { } characterId) return;
            if (await _store.GetCharacterAsync(characterId) is null) return;

            await _store.AddRecordAsync(new RecordInfo
            {
                CharacterId     = characterId,
                Type            = RecordType.Medical,
                Description     = ReportRules.MedicalSummary(report),
                OfficerName     = name,
                OfficerCallsign = callsign,
            }, license);
        }
    }
}
