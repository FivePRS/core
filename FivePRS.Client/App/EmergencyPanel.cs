using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Client.Agency;
using FivePRS.Core.Civilian;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Client.App
{
    internal static class EmergencyPanel
    {
        private static EmergencyCallStatus? _status;

        public static void SetStatus(string json) => _status = JsonConvert.DeserializeObject<EmergencyCallStatus>(json);

        public static void RequestStatus() => ClientEvents.TriggerServer(EventNames.ServerEmergencyStatus);

        public static void Call(IDictionary<string, object> data)
        {
            if (!NuiData.TryGetInt(data, "departmentId", out var departmentId)) return;
            ClientEvents.TriggerServer(EventNames.ServerEmergencyCall, departmentId,
                NuiData.GetString(data, "description"), NuiData.GetBool(data, "anonymous"));
        }

        public static void Cancel()
        {
            if (_status is not null)
                ClientEvents.TriggerServer(EventNames.ServerEmergencyCancel, _status.CallId);
        }

        public static void ClearCall(IDictionary<string, object> data)
        {
            if (NuiData.TryGetString(data, "callId", out var callId))
                ClientEvents.TriggerServer(EventNames.ServerCallClear, callId);
        }

        public static object BuildView() => new
        {
            Departments = Enum.GetValues(typeof(Department))
                .Cast<Department>()
                .Where(d => d != Department.None && BaseAgency.IsDepartmentLoaded(d))
                .Select(d => new { Id = (int)d, Name = d.ToString() })
                .ToList(),
            Status    = _status,
            MaxLength = CivilianRules.MaxEmergencyLength,
        };
    }
}
