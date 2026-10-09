using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Client.Agency;
using FivePRS.Client.Dispatch;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;

namespace FivePRS.Client.App
{
    internal static class DutyPanel
    {
        private static PlayerData? _profile;
        private static List<Department>? _allowed;

        public static bool IsLoaded => _profile is not null && _allowed is not null;

        public static bool HasDepartment => _profile is not null && _profile.Department != Department.None;

        public static void SetProfile(PlayerData profile) => _profile = profile;

        public static void SetAllowed(IEnumerable<int> departmentIds) =>
            _allowed = departmentIds
                .Where(id => Enum.IsDefined(typeof(Department), id))
                .Select(id => (Department)id)
                .ToList();

        public static void SetOnDuty(bool isOnDuty)
        {
            if (_profile is not null)
                _profile.IsOnDuty = isOnDuty;
        }

        public static string? Enter(IDictionary<string, object> data)
        {
            if (!data.TryGetValue("departmentId", out var rawDepartment) ||
                !int.TryParse(rawDepartment?.ToString(), out var departmentId))
            {
                return "Choose a department first.";
            }

            var agencyId    = data.TryGetValue("agencyId", out var rawAgency) ? rawAgency?.ToString() ?? string.Empty : string.Empty;
            var rawCallsign = data.TryGetValue("callsign", out var callsignValue) ? callsignValue?.ToString() : null;

            if (!Callsign.TryNormalize(rawCallsign, out var callsign))
                return $"Callsigns can be up to {Callsign.MaxLength} letters, numbers or hyphens.";

            ClientEvents.TriggerServer(EventNames.ServerEnterService, departmentId, agencyId, callsign);
            return null;
        }

        public static void GoOffDuty()
        {
            if (_profile?.IsOnDuty == true)
                ClientEvents.TriggerServer(EventNames.ServerToggleDuty);
        }

        public static object BuildView()
        {
            var profile = _profile ?? new PlayerData();
            var map     = ConfigManager.Territories;
            var unit    = DispatchClient.LocalUnit;

            var departments = (_allowed ?? new List<Department>())
                .Where(d => d != Department.None && BaseAgency.IsDepartmentLoaded(d))
                .Select(d => new
                {
                    Id       = (int)d,
                    Name     = d.ToString(),
                    Agencies = map.AgenciesFor(d).Select(a => new { a.Id, a.Name, a.CallsignPrefix }).ToList(),
                })
                .ToList();

            return new
            {
                profile.Name,
                profile.Rank,
                profile.XP,
                XpToNext          = profile.XPToNextRank,
                profile.IsOnDuty,
                DepartmentId      = (int)profile.Department,
                AgencyId          = profile.Agency,
                profile.Callsign,
                CallsignMaxLength = Callsign.MaxLength,
                Departments       = departments,
                Unit              = unit is null ? null : new
                {
                    unit.Callsign,
                    Agency = map.FindAgency(unit.Agency)?.Name ?? unit.Department.ToString(),
                },
            };
        }
    }
}
