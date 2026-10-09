using System;
using System.Collections.Generic;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Client.App
{
    internal static class RecordsPanel
    {
        private static List<CharacterSummary>? _results;
        private static string? _query;
        private static LookupRecord? _record;

        public static bool IsAvailable => DispatchPanel.IsAvailable && DutyPanel.Department == Department.Police;

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
        }

        public static void CloseRecord() => _record = null;

        public static void SearchName(IDictionary<string, object> data) =>
            ClientEvents.TriggerServer(EventNames.ServerLookupName, NuiData.GetString(data, "term"));

        public static void SearchPlate(IDictionary<string, object> data) =>
            ClientEvents.TriggerServer(EventNames.ServerLookupPlate, NuiData.GetString(data, "plate"));

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
                Results        = _results,
                Query          = _query,
                Record         = _record,
                MaxFine        = ConfigManager.Settings.MaxFine,
                MaxDescription = CivilianRules.MaxDescriptionLength,
            };
        }
    }
}
