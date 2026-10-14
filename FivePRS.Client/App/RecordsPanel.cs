using System.Collections.Generic;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Terminal.Apps;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Client.App
{
    internal static class RecordsPanel
    {
        private const float VehicleRange = 30f;
        private const float FootVehicleRange = 10f;
        private const float VehicleCone = 0.8f;
        private const float PersonRange = 3f;
        private const float PersonCone = 0.5f;

        private static List<CharacterSummary>? _results;
        private static string? _query;
        private static LookupRecord? _record;
        private static ReportsView? _reports;

        public static int DraftCount => _reports?.Drafts.Count ?? 0;

        public static void SetReports(string json)
        {
            var view = JsonConvert.DeserializeObject<ReportsView>(json);
            if (view is null) return;

            if (view.Results is null && _reports is not null)
            {
                view.Results = _reports.Results;
                view.Query   = _reports.Query;
                view.Date    = _reports.Date;
            }

            _reports = view;
        }

        public static void RequestReports() => ClientEvents.TriggerServer(EventNames.ServerReportsRequest);

        public static void SaveReport(IDictionary<string, object> data)
        {
            if (NuiData.TryGetInt(data, "reportId", out var id))
                ClientEvents.TriggerServer(EventNames.ServerReportSave, id, NuiData.GetString(data, "narrative"));
        }

        public static void FileReport(IDictionary<string, object> data)
        {
            if (NuiData.TryGetInt(data, "reportId", out var id))
                ClientEvents.TriggerServer(EventNames.ServerReportFile, id, NuiData.GetString(data, "narrative"));
        }

        public static void SearchReports(IDictionary<string, object> data) =>
            ClientEvents.TriggerServer(EventNames.ServerReportSearch, NuiData.GetString(data, "term"), NuiData.GetString(data, "date"));

        public static bool IsAvailable => DispatchPanel.IsAvailable && RecordAccess.CanView(DutyPanel.Department);

        private static bool Police => DutyPanel.Department == Department.Police;

        public static void SetResult(string json)
        {
            var result = JsonConvert.DeserializeObject<LookupResult>(json);
            if (result is null) return;

            if (result.Results is not null)
            {
                _results = result.Results;
                _query   = result.Query;
                _record  = null;
            }

            if (result.Record is not null)
                _record = result.Record;
        }

        public static void Clear()
        {
            _results = null;
            _query   = null;
            _record  = null;
            _reports = null;
        }

        public static void CloseRecord() => _record = null;

        public static void SearchName(IDictionary<string, object> data) =>
            ClientEvents.TriggerServer(EventNames.ServerLookupName, NuiData.GetString(data, "term"));

        public static void SearchPlate(IDictionary<string, object> data)
        {
            var plate   = NuiData.GetString(data, "plate");
            var vehicle = StreetIdentity.FindByPlate(plate);
            if (vehicle is not null && IsStreetVehicle(vehicle))
                RunVehicle(vehicle);
            else
                ClientEvents.TriggerServer(EventNames.ServerLookupPlate, plate);
        }

        public static void RunVehicleAhead()
        {
            if (!Police) return;

            var inVehicle = Game.PlayerPed.IsInVehicle();
            var vehicle   = StreetIdentity.VehicleAhead(inVehicle ? VehicleRange : FootVehicleRange, VehicleCone, true);
            if (vehicle is null)
            {
                RecordsApp.ShowError("There's no vehicle ahead of you.");
                return;
            }

            if (IsStreetVehicle(vehicle))
                RunVehicle(vehicle);
            else
                ClientEvents.TriggerServer(EventNames.ServerLookupPlate, StreetIdentity.PlateOf(vehicle));
        }

        public static void IdentifyPerson()
        {
            var ped = StreetIdentity.PersonAhead(PersonRange, PersonCone, true);
            if (ped is null)
            {
                RecordsApp.ShowError("Face the person you want to identify and stand close to them.");
                return;
            }

            ClientEvents.TriggerServer(EventNames.ServerRunPerson, StreetIdentity.SeedFor(ped), StreetIdentity.IsFemale(ped));
        }

        public static void RunStopPlate() => StreetIdentity.CurrentStop?.Invoke()?.RunPlate?.Invoke();

        private static void RunVehicle(Vehicle vehicle)
        {
            var driver    = vehicle.Driver;
            var hasDriver = driver is not null && driver.Exists() && !driver.IsPlayer && !driver.IsDead;
            ClientEvents.TriggerServer(EventNames.ServerRunPlate,
                StreetIdentity.PlateOf(vehicle),
                StreetIdentity.VehicleName(vehicle),
                hasDriver ? StreetIdentity.SeedFor(driver!) : 0,
                hasDriver && StreetIdentity.IsFemale(driver!),
                hasDriver);
        }

        private static bool IsStreetVehicle(Vehicle vehicle)
        {
            var driver = vehicle.Driver;
            if (driver is not null && driver.Exists() && driver.IsPlayer) return false;

            var last = API.GetLastPedInVehicleSeat(vehicle.Handle, -1);
            return last == 0 || !API.IsPedAPlayer(last);
        }

        public static void Open(IDictionary<string, object> data)
        {
            if (NuiData.TryGetInt(data, "characterId", out var id))
                ClientEvents.TriggerServer(EventNames.ServerLookupCharacter, id);
        }

        public static void Issue(IDictionary<string, object> data)
        {
            if (!NuiData.TryGetInt(data, "characterId", out var id) || !NuiData.TryGetInt(data, "type", out var type)) return;
            NuiData.TryGetInt(data, "fine", out var fine);
            ClientEvents.TriggerServer(EventNames.ServerRecordIssue, id, type, NuiData.GetString(data, "description"), fine);
        }

        public static void Resolve(IDictionary<string, object> data)
        {
            if (!NuiData.TryGetInt(data, "recordId", out var id)) return;
            var served = NuiData.GetBool(data, "served");
            ClientEvents.TriggerServer(EventNames.ServerRecordResolve, id, served);
        }

        public static void SetLicense(IDictionary<string, object> data)
        {
            if (!NuiData.TryGetInt(data, "characterId", out var id) || !NuiData.TryGetInt(data, "status", out var status)) return;
            ClientEvents.TriggerServer(EventNames.ServerLicenseSetStatus, id, NuiData.GetString(data, "type"), status);
        }

        public static void FlagVehicle(IDictionary<string, object> data)
        {
            if (!NuiData.TryGetInt(data, "vehicleId", out var id)) return;
            var stolen = NuiData.GetBool(data, "stolen");
            ClientEvents.TriggerServer(EventNames.ServerVehicleFlag, id, stolen);
        }

        public static object? BuildView()
        {
            if (!IsAvailable) return null;

            return new
            {
                Medical        = !Police,
                Reports        = Police ? null : _reports,
                MaxNarrative   = ReportRules.MaxNarrativeLength,
                AutoFileMinutes = ReportRules.AutoFileMinutes,
                Stop           = Police ? StreetIdentity.CurrentStop?.Invoke() : null,
                Results        = _results,
                Query          = _query,
                Record         = _record,
                MaxFine        = ConfigManager.Settings.MaxFine,
                MaxDescription = CivilianRules.MaxDescriptionLength,
                Offences       = ConfigManager.Citations.Offences,
                StateInfo      = ConfigManager.Licenses.State,
            };
        }
    }
}
