using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Civilian;
using FivePRS.Core.Events;
using FivePRS.Server.Civilian;
using FivePRS.Server.Database;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const int ReportSweepMs = 60_000;

        private ReportService? _reports;

        private void RegisterReportEvents()
        {
            EventHandlers[EventNames.ServerReportDraft]    += new Action<Player, string>(OnReportDraft);
            EventHandlers[EventNames.ServerReportsRequest] += new Action<Player>(OnReportsRequest);
            EventHandlers[EventNames.ServerReportSave]     += new Action<Player, int, string>(OnReportSave);
            EventHandlers[EventNames.ServerReportFile]     += new Action<Player, int, string>(OnReportFile);
            EventHandlers[EventNames.ServerReportSearch]   += new Action<Player, string, string>(OnReportSearch);
            Tick += ReportSweepTickAsync;
        }

        private async void OnReportDraft([FromSource] Player player, string json)
        {
            if (_reports is null || !Allow(player, "records", l => l.Records)) return;
            if (!TryGetResponder(player, out var author)) return;

            ReportDraft? draft;
            try
            {
                draft = JsonConvert.DeserializeObject<ReportDraft>(json);
            }
            catch (JsonException)
            {
                return;
            }

            if (draft is null || !IsCallId(draft.CallId)) return;

            var crew = new List<CrewMember>();
            foreach (var unitId in _dispatch.CrewOf(draft.CallId, ServerId(player)))
            {
                if (!TryGetPlayer(unitId, out var member) || GetLicense(member) is not { } license) continue;
                crew.Add(new CrewMember(license, _dispatch.GetUnit(unitId)?.Callsign ?? string.Empty));
            }

            try
            {
                var (report, error) = await _reports.DraftAsync(author, draft, crew);
                if (report is null)
                {
                    if (error is not null) Log.Debug($"Report draft from {player.Name} refused: {error}");
                    return;
                }

                foreach (var license in crew.Select(member => member.License))
                    await PushReportsAsync(license, $"~b~Reports~w~ | {report.Title} draft ready. Review and file it in ~y~Records › Reports~w~.");
            }
            catch (Exception ex)
            {
                Log.Error($"Could not draft a report for {player.Name}", ex);
            }
        }

        private void OnReportsRequest([FromSource] Player player) =>
            RunReports(player, async (reports, reader) =>
                ReportService.CanRead(reader.Department)
                    ? (new ReportsView { Drafts = await reports.DraftsForAsync(reader.License) }, null)
                    : (null, "Reports are for Fire and EMS."));

        private void OnReportSave([FromSource] Player player, int reportId, string narrative) =>
            RunReports(player, async (reports, editor) =>
            {
                var error = await reports.SaveAsync(editor, reportId, narrative);
                if (error is not null) return (null, error);

                await PushReportsToCrewAsync(reportId, editor.License, null);
                return (new ReportsView { Drafts = await reports.DraftsForAsync(editor.License) }, null);
            });

        private void OnReportFile([FromSource] Player player, int reportId, string narrative) =>
            RunReports(player, async (reports, filer) =>
            {
                var (report, error) = await reports.FileAsync(filer, reportId, narrative);
                if (report is null) return (null, error);

                Audit(AuditActions.ReportFiled, player, null, $"{report.Kind.ToString().ToLowerInvariant()} report {report.Id} call {report.CallId}");
                await PushReportsToCrewAsync(reportId, filer.License, $"~b~Reports~w~ | {filer.Callsign} filed the {report.Title} report.");
                return (new ReportsView { Drafts = await reports.DraftsForAsync(filer.License) }, null);
            });

        private void OnReportSearch([FromSource] Player player, string term, string date) =>
            RunReports(player, (reports, reader) => reports.SearchAsync(reader, term, date));

        private async void RunReports(Player player, Func<ReportService, OfficerInfo, Task<(ReportsView? View, string? Error)>> action)
        {
            if (!Allow(player, "records", l => l.Records))
            {
                TriggerClientEvent(player, EventNames.ClientLookupError, "Slow down. Try again in a few seconds.");
                return;
            }

            if (_reports is null || !TryGetResponder(player, out var officer))
            {
                TriggerClientEvent(player, EventNames.ClientLookupError, "Reports are only available on duty.");
                return;
            }

            try
            {
                var (view, error) = await action(_reports, officer);
                if (error is not null)
                    TriggerClientEvent(player, EventNames.ClientLookupError, error);
                else if (view is not null)
                    TriggerClientEvent(player, EventNames.ClientReports, JsonConvert.SerializeObject(view));
            }
            catch (Exception ex)
            {
                Log.Error($"Reports action failed for {player.Name}", ex);
                TriggerClientEvent(player, EventNames.ClientLookupError, "Something went wrong. Please try again.");
            }
        }

        private async Task PushReportsToCrewAsync(int reportId, string? exceptLicense, string? message)
        {
            if (_reports is null) return;
            foreach (var license in await _reports.CrewOfAsync(reportId))
            {
                if (license == exceptLicense) continue;
                await PushReportsAsync(license, message);
            }
        }

        private async Task PushReportsAsync(string license, string? message)
        {
            if (_reports is null) return;

            var player = Players.FirstOrDefault(p => GetLicense(p) == license);
            if (player is null) return;

            if (message is not null) Notify(player, message);
            var view = new ReportsView { Drafts = await _reports.DraftsForAsync(license) };
            TriggerClientEvent(player, EventNames.ClientReports, JsonConvert.SerializeObject(view));
        }

        private async Task ReportSweepTickAsync()
        {
            await Delay(ReportSweepMs);
            if (_reports is null) return;

            try
            {
                var filed = await _reports.FileStaleAsync(DateTime.UtcNow);
                if (filed.Count == 0) return;

                Log.Info($"Filed {filed.Count} report draft{(filed.Count == 1 ? "" : "s")} left unfiled for {ReportRules.AutoFileMinutes} minutes.");
                foreach (var report in filed)
                    await PushReportsToCrewAsync(report.Id, null, null);
            }
            catch (Exception ex)
            {
                Log.Error("Could not file old report drafts", ex);
            }
        }
    }
}
