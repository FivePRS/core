using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Client.Agency;
using FivePRS.Client.App;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Client.Terminal.Apps
{
    public class EmergencyApp : TerminalApp
    {
        private const string AppId = "emergency";

        private static EmergencyCallStatus? _status;

        public override string Id => AppId;

        public override string Label => "911";

        public override string Color => "#b91c1c";

        public override string Icon =>
            "M5 4h4l2 5-2.5 1.5a11 11 0 0 0 5 5L15 13l5 2v4a2 2 0 0 1-2 2A16 16 0 0 1 3 6a2 2 0 0 1 2-2z";

        public override int Order => 60;

        public override string? Page => "nui/apps/emergency/index.html";

        public override string? Badge => _status is null ? null : "•";

        internal static void SetStatus(string json) => _status = JsonConvert.DeserializeObject<EmergencyCallStatus>(json);

        internal static void RequestStatus() => ClientEvents.TriggerServer(EventNames.ServerEmergencyStatus);

        internal static void ShowError(string message) => TerminalApps.SendToApp(AppId, "error", message);

        public override void OnOpened() => RequestStatus();

        public override object BuildState() => new
        {
            Departments = Enum.GetValues(typeof(Department))
                .Cast<Department>()
                .Where(d => d != Department.None && BaseAgency.IsDepartmentLoaded(d))
                .Select(d => new { Id = (int)d, Name = d.ToString() })
                .ToList(),
            Status    = _status,
            MaxLength = CivilianRules.MaxEmergencyLength,
            Icons     = ConfigManager.Settings.Branding.Departments,
            Location  = HomePanel.CurrentLocation(),
        };

        public override void OnAction(string action, IDictionary<string, object> data)
        {
            switch (action)
            {
                case "call" when NuiData.TryGetInt(data, "departmentId", out var departmentId):
                    ClientEvents.TriggerServer(EventNames.ServerEmergencyCall, departmentId,
                        NuiData.GetString(data, "description"), NuiData.GetBool(data, "anonymous"));
                    break;

                case "cancel" when _status is not null:
                    ClientEvents.TriggerServer(EventNames.ServerEmergencyCancel, _status.CallId);
                    break;
            }
        }
    }
}
