using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using FivePRS.Server.Permissions;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const int MaxLicenseLength = 60;
        private const int MaxQueryLength = 64;
        private const int AdminAuditLimit = 100;

        private RosterService? _roster;

        private void RegisterRosterEvents()
        {
            EventHandlers[EventNames.ServerAdminRequest]   += new Action<Player, string>(OnAdminRequest);
            EventHandlers[EventNames.ServerAdminRosterSet] += new Action<Player, string, int, bool, string>(OnAdminRosterSet);
            EventHandlers[EventNames.ServerAdminAction]    += new Action<Player, string, string, string, string>(OnAdminAction);
        }

        private async void OnAdminAction([FromSource] Player player, string action, string target, string value, string query)
        {
            if (!_permissions.IsAdmin(player.Handle) || !Allow(player, "admin", l => l.Admin)) return;

            target = (target ?? string.Empty).Trim();
            value  = (value ?? string.Empty).Trim();

            try
            {
                var result = action switch
                {
                    "setrank"  => await SetRankAsync(player, new[] { target, value }),
                    "addxp"    => await AddXpAsync(player, new[] { target, value }),
                    "setdept"  => await SetDepartmentAsync(player, new[] { target, value }),
                    "offduty"  => await ForceOffDutyAsync(player, new[] { target }),
                    "revive"   => await AdminReviveAsync(player, new[] { target }),
                    "endcall"  => await EndCallAsync(player, new[] { target }),
                    "teleport" => TeleportTo(player, target),
                    _          => CommandResult.Failed("Unknown admin action."),
                };

                Reply(player, result);
            }
            catch (Exception ex)
            {
                Log.Error($"Admin action {action} failed for {player.Name}", ex);
                Reply(player, CommandResult.Failed("That action failed. Check the server console."));
            }

            await SendAdminStateAsync(player, TrimQuery(query));
        }

        private CommandResult TeleportTo(Player admin, string target)
        {
            if (!int.TryParse(target, out var id) || !TryGetPlayer(id, out var player)) return TargetNotFound(target);

            var position = API.GetEntityCoords(API.GetPlayerPed(player.Handle));
            if (position == Vector3.Zero) return CommandResult.Failed("Teleporting needs OneSync to find the player.");

            TriggerClientEvent(admin, EventNames.ClientAdminTeleport, position.X, position.Y, position.Z);
            Audit(AuditActions.AdminTeleport, admin, GetLicense(player), $"to {player.Name}");
            return CommandResult.Done($"Teleported to {player.Name}.");
        }

        private bool IsOnRoster(string playerHandle, Department department)
        {
            if (_roster is null || !int.TryParse(playerHandle, out var id)) return false;
            return TryGetPlayer(id, out var player) && _roster.Contains(GetLicense(player), department);
        }

        private async void OnAdminRequest([FromSource] Player player, string query)
        {
            if (!_permissions.IsAdmin(player.Handle) || !Allow(player, "admin", l => l.Admin)) return;
            await SendAdminStateAsync(player, TrimQuery(query));
        }

        private async void OnAdminRosterSet([FromSource] Player player, string license, int departmentId, bool granted, string query)
        {
            if (!_permissions.IsAdmin(player.Handle) || _roster is null || !Allow(player, "admin", l => l.Admin)) return;
            if (!IsLicense(license)) return;
            query = TrimQuery(query);
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
                Log.Error($"Roster change failed for {player.Name}", ex);
                Notify(player, "~r~Roster change failed. Check the server console.");
            }
        }

        private static bool IsLicense(string? license) =>
            license is not null && license.Length <= MaxLicenseLength && license.StartsWith("license:", StringComparison.Ordinal);

        private static string TrimQuery(string? query)
        {
            var trimmed = (query ?? string.Empty).Trim();
            return trimmed.Length > MaxQueryLength ? trimmed.Substring(0, MaxQueryLength) : trimmed;
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
                        Callsign    = cached?.Callsign ?? string.Empty,
                        Agency      = cached is null ? string.Empty : ConfigManager.Territories.FindAgency(cached.Agency)?.Name ?? string.Empty,
                        OnDuty      = cached?.IsOnDuty ?? false,
                        Down        = _downed.Contains(ServerId(p.Player)),
                    };
                })
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var onlineLicenses = online.Select(p => p.License).ToList();
            var results = (await _roster.SearchAsync(query)).Where(p => !onlineLicenses.Contains(p.License)).ToList();
            foreach (var player in online.Concat(results).Where(p => p.Department != Department.None))
                player.RankName = ConfigManager.RanksFor(player.Department).NameFor(player.Rank);

            var snapshot = _dispatch.CreateSnapshot();
            var calls = snapshot.Calls.Select(call => new AdminCall
            {
                Id         = call.Id,
                Name       = call.Name,
                Code       = (int)call.Priority,
                Department = call.Department,
                Territory  = ConfigManager.Territories.FindTerritory(call.Territory)?.Name,
                Units      = call.Units
                    .Select(id => snapshot.Units.FirstOrDefault(u => u.ServerId == id)?.Callsign)
                    .Where(callsign => callsign is not null)
                    .Select(callsign => callsign!)
                    .ToList(),
                Emergency  = call.IsEmergency,
            }).ToList();

            var audit = _db.IsReady
                ? (await _db.Players.GetAuditAsync(AdminAuditLimit)).Select(entry => new AdminAudit
                {
                    CreatedAt = entry.CreatedAt,
                    Action    = entry.Action,
                    ActorName = entry.ActorName,
                    Details   = entry.Details,
                }).ToList()
                : new List<AdminAudit>();

            var state = new AdminState
            {
                RestrictDepartments = _permissions.RestrictsDepartments,
                Query               = query,
                Online              = online,
                Results             = results,
                Calls               = calls,
                Audit               = audit,
                Server              = DiagnosticFacts(),
                Warnings            = StartupWarnings(),
                Addons              = Addons(),
            };

            TriggerClientEvent(admin, EventNames.ClientAdminState, JsonConvert.SerializeObject(state));
        }
    }
}
