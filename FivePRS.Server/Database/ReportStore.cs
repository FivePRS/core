using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Models;

namespace FivePRS.Server.Database
{
    public sealed class ReportStore : SqlStore
    {
        private const string Columns =
            "r.id, r.kind, r.department, r.call_id, r.status, r.title, r.location, r.narrative, r.character_id, r.plate, r.crew, " +
            "r.author_name, r.author_callsign, r.created_at, r.filed_at, r.filed_by, c.first_name, c.last_name";

        private const string From = "FROM fiveprs_reports r LEFT JOIN fiveprs_characters c ON c.id = r.character_id";

        public ReportStore(IDatabaseProvider db) : base(db)
        {
        }

        public async Task<int> AddAsync(ReportInfo report, string authorLicense, IEnumerable<string> crewLicenses)
        {
            var id = await InsertAsync(
                "INSERT INTO fiveprs_reports (kind, department, call_id, status, title, location, narrative, character_id, plate, crew, crew_licenses, " +
                "author_license, author_name, author_callsign, created_at) VALUES (@kind, @department, @call, @status, @title, @location, @narrative, " +
                "@character, @plate, @crew, @crewLicenses, @license, @name, @callsign, @now)",
                ("@kind", (int)report.Kind), ("@department", (int)report.Department), ("@call", report.CallId),
                ("@status", (int)ReportStatus.Draft), ("@title", report.Title), ("@location", report.Location),
                ("@narrative", report.Narrative), ("@character", report.CharacterId), ("@plate", report.Plate),
                ("@crew", report.Crew), ("@crewLicenses", Pack(crewLicenses)), ("@license", authorLicense),
                ("@name", report.AuthorName), ("@callsign", report.AuthorCallsign), ("@now", DateTime.UtcNow));
            return Convert.ToInt32(id);
        }

        public async Task<ReportInfo?> GetAsync(int id)
        {
            var rows = await QueryAsync($"SELECT {Columns} {From} WHERE r.id = @id", Read, ("@id", id));
            return rows.Count > 0 ? rows[0] : null;
        }

        public Task<List<ReportInfo>> GetDraftsForAsync(string license) =>
            QueryAsync(
                $"SELECT {Columns} {From} WHERE r.status = @draft AND r.crew_licenses LIKE @license ESCAPE '!' ORDER BY r.created_at DESC, r.id DESC",
                Read, ("@draft", (int)ReportStatus.Draft), ("@license", $"%|{Escape(license)}|%"));

        public async Task<bool> IsCrewAsync(int id, string license)
        {
            var rows = await QueryAsync(
                "SELECT id FROM fiveprs_reports WHERE id = @id AND crew_licenses LIKE @license ESCAPE '!'",
                row => row.GetInt32("id"), ("@id", id), ("@license", $"%|{Escape(license)}|%"));
            return rows.Count > 0;
        }

        public async Task<List<string>> CrewLicensesAsync(int id)
        {
            var rows = await QueryAsync("SELECT crew_licenses FROM fiveprs_reports WHERE id = @id",
                row => row.GetString("crew_licenses"), ("@id", id));
            return rows.SelectMany(Unpack).ToList();
        }

        public Task SaveNarrativeAsync(int id, string narrative) =>
            ExecuteAsync("UPDATE fiveprs_reports SET narrative = @narrative WHERE id = @id AND status = @draft",
                ("@narrative", narrative), ("@id", id), ("@draft", (int)ReportStatus.Draft));

        public Task FileAsync(int id, string narrative, string filedBy) =>
            ExecuteAsync("UPDATE fiveprs_reports SET narrative = @narrative, status = @filed, filed_at = @now, filed_by = @by WHERE id = @id AND status = @draft",
                ("@narrative", narrative), ("@filed", (int)ReportStatus.Filed), ("@now", DateTime.UtcNow),
                ("@by", filedBy), ("@id", id), ("@draft", (int)ReportStatus.Draft));

        public Task<List<ReportInfo>> GetStaleDraftsAsync(DateTime olderThan) =>
            QueryAsync($"SELECT {Columns} {From} WHERE r.status = @draft AND r.created_at < @before ORDER BY r.id",
                Read, ("@draft", (int)ReportStatus.Draft), ("@before", olderThan));

        public Task<List<ReportInfo>> SearchAsync(string term, DateTime? day, IReadOnlyCollection<ReportKind> kinds, int limit)
        {
            var conditions = new List<string> { "r.status = @filed" };
            var parameters = new List<(string, object?)> { ("@filed", (int)ReportStatus.Filed) };

            if (term.Length > 0)
            {
                conditions.Add("(r.location LIKE @term ESCAPE '!' OR r.title LIKE @term ESCAPE '!' OR r.plate LIKE @term ESCAPE '!' OR r.call_id LIKE @term ESCAPE '!')");
                parameters.Add(("@term", $"%{Escape(term)}%"));
            }

            if (day is not null)
            {
                conditions.Add("r.created_at >= @from AND r.created_at < @to");
                parameters.Add(("@from", DateTime.SpecifyKind(day.Value.Date, DateTimeKind.Utc)));
                parameters.Add(("@to", DateTime.SpecifyKind(day.Value.Date.AddDays(1), DateTimeKind.Utc)));
            }

            var kindList = kinds.Select((kind, index) => ($"@kind{index}", (object?)(int)kind)).ToList();
            conditions.Add($"r.kind IN ({string.Join(", ", kindList.Select(k => k.Item1))})");
            parameters.AddRange(kindList);

            return QueryAsync(
                $"SELECT {Columns} {From} WHERE {string.Join(" AND ", conditions)} ORDER BY r.created_at DESC, r.id DESC LIMIT {limit}",
                Read, parameters.ToArray());
        }

        private static string Pack(IEnumerable<string> licenses) =>
            "|" + string.Join("|", licenses.Distinct()) + "|";

        private static IEnumerable<string> Unpack(string packed) =>
            packed.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);

        private static string Escape(string text) =>
            text.Replace("!", "!!").Replace("%", "!%").Replace("_", "!_");

        private static ReportInfo Read(DbRow row) => new()
        {
            Id             = row.GetInt32("id"),
            Kind           = (ReportKind)row.GetInt32("kind"),
            Department     = (Department)row.GetInt32("department"),
            CallId         = row.GetString("call_id"),
            Status         = (ReportStatus)row.GetInt32("status"),
            Title          = row.GetString("title"),
            Location       = row.GetString("location"),
            Narrative      = row.GetString("narrative"),
            CharacterId    = row.IsNull("character_id") ? null : row.GetInt32("character_id"),
            PersonName     = row.IsNull("first_name") ? null : $"{row.GetString("first_name")} {row.GetString("last_name")}",
            Plate          = row.IsNull("plate") ? null : row.GetString("plate"),
            Crew           = row.GetString("crew"),
            AuthorName     = row.GetString("author_name"),
            AuthorCallsign = row.GetString("author_callsign"),
            CreatedAt      = row.GetDateTime("created_at"),
            FiledAt        = row.IsNull("filed_at") ? null : row.GetDateTime("filed_at"),
            FiledBy        = row.IsNull("filed_by") ? null : row.GetString("filed_by"),
        };
    }
}
