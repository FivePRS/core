using System;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using FivePRS.Server.Permissions;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private RosterService? _roster;

        private void RegisterRosterEvents()
        {
            EventHandlers[EventNames.ServerAdminRequest]   += new Action<Player, string>(OnAdminRequest);
            EventHandlers[EventNames.ServerAdminRosterSet] += new Action<Player, string, int, bool, string>(OnAdminRosterSet);
        }

        private bool IsOnRoster(string playerHandle, Department department)
        {
            if (_roster is null || !int.TryParse(playerHandle, out var id)) return false;
            return TryGetPlayer(id, out var player) && _roster.Contains(GetLicense(player), department);
        }

        private async void OnAdminRequest([FromSource] Player player, string query)
        {
            if (!_permissions.IsAdmin(player.Handle)) return;
            await SendAdminStateAsync(player, query);
        }

        private async void OnAdminRosterSet([FromSource] Player player, string license, int departmentId, bool granted, string query)
        {
            if (!_permissions.IsAdmin(player.Handle) || _roster is null) return;
            if (!Enum.IsDefined(typeof(Department), departmentId) || departmentId == (int)Department.None) return;

            var department = (Department)departmentId;

            try
            {
                if (await _roster.SetAsync(license, department, granted, player.Name))
                {
                    Audit(granted ? AuditActions.RosterGranted : AuditActions.RosterRevoked, player, license, department.ToString());
                    await ApplyRosterChangeAsync(license, department, granted);
                }

                await SendAdminStateAsync(player, query);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Roster change failed for {player.Name}: {ex}");
                Notify(player, "~r~Roster change failed. Check the server console.");
            }
        }

        private async Task ApplyRosterChangeAsync(string license, Department department, bool granted)
        {
            var target = Players.FirstOrDefault(p => GetLicense(p) == license);
            if (target is null) return;

            SendEntryOptions(target);

            if (granted)
            {
                Notify(target, $"~g~Roster~w~ | You can now join {department}.");
                return;
            }

            Notify(target, $"~o~Roster~w~ | You were removed from {department}.");

            if (TryGetCached(target, out _, out var data) && data.IsOnDuty && data.Department == department &&
                !_permissions.CanJoinDepartment(target.Handle, department))
            {
                await SetDutyAsync(target, data, false);
            }
        }

        private async Task SendAdminStateAsync(Player admin, string? query)
        {
            if (_roster is null) return;

            var online = Players
                .Select(p => (Player: p, License: GetLicense(p)))
                .Where(p => p.License is not null)
                .Select(p =>
                {
                    var cached = TryGetCached(p.Player, out _, out var data) ? data : null;
                    return new RosterPlayer
                    {
                        License     = p.License!,
                        Name        = cached?.Name ?? p.Player.Name,
                        Rank        = cached?.Rank ?? 1,
                        Department  = cached?.Department ?? Department.None,
                        Online      = true,
                        ServerId    = ServerId(p.Player),
                        Departments = _roster.DepartmentsFor(p.License!),
                    };
                })
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var onlineLicenses = online.Select(p => p.License).ToList();
            var results = (await _roster.SearchAsync(query)).Where(p => !onlineLicenses.Contains(p.License)).ToList();

            var state = new AdminState
            {
                RestrictDepartments = _permissions.RestrictsDepartments,
                Query               = query,
                Online              = online,
                Results             = results,
            };

            TriggerClientEvent(admin, EventNames.ClientAdminState, JsonConvert.SerializeObject(state));
        }
    }
}
