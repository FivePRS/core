using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Client.Agency;
using FivePRS.Client.Dispatch;
using FivePRS.Client.Stations;
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

        public static Department Department => _profile?.Department ?? Department.None;

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

            var error = SetStart(data, (Department)departmentId, agencyId);
            if (error is not null) return error;

            ClientEvents.TriggerServer(EventNames.ServerEnterService, departmentId, agencyId, callsign);
            return null;
        }

        private static string? SetStart(IDictionary<string, object> data, Department department, string agencyId)
        {
            var rawMode = NuiData.GetString(data, "start");
            if (!Enum.TryParse<DutyStartMode>(rawMode, true, out var mode) || mode == DutyStartMode.Here)
            {
                StationService.SetPendingStart(DutyStartMode.Here, null);
                return null;
            }

            var station = ConfigManager.Stations.Find(NuiData.GetString(data, "stationId"));
            if (station is null || !station.Serves(department, agencyId))
                return "Choose a station first.";

            StationService.SetPendingStart(mode, station.Id);
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
                    Stations = ConfigManager.Stations.For(d, null)
                        .Select(s => new { s.Id, s.Name, s.Agencies, Distance = (int)StationService.DistanceTo(s) })
                        .OrderBy(s => s.Distance)
                        .ToList(),
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
                Department        = profile.Department.ToString(),
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
