using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Client.App
{
    internal static class AdminPanel
    {
        private static AdminState? _state;

        public static bool IsAvailable => _state is not null;

        public static void SetState(string json) => _state = JsonConvert.DeserializeObject<AdminState>(json);

        public static void Request() => ClientEvents.TriggerServer(EventNames.ServerAdminRequest, _state?.Query ?? string.Empty);

        public static void Search(IDictionary<string, object> data) =>
            ClientEvents.TriggerServer(EventNames.ServerAdminRequest, NuiData.GetString(data, "query"));

        public static void SetRoster(IDictionary<string, object> data)
        {
            if (!NuiData.TryGetString(data, "license", out var license) ||
                !NuiData.TryGetInt(data, "departmentId", out var departmentId))
            {
                return;
            }

            ClientEvents.TriggerServer(EventNames.ServerAdminRosterSet, license, departmentId,
                NuiData.GetBool(data, "granted"), _state?.Query ?? string.Empty);
        }

        public static object? BuildView()
        {
            if (_state is null) return null;

            return new
            {
                State       = _state,
                Departments = Enum.GetValues(typeof(Department))
                    .Cast<Department>()
                    .Where(d => d != Department.None)
                    .Select(d => new { Id = (int)d, Name = d.ToString() })
                    .ToList(),
            };
        }
    }
}
