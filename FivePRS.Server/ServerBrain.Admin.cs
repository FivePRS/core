using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Database;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private void RegisterAdminCommands()
        {
            RegisterAdminCommand("fprs_setrank", "<player id> <rank>",       2, SetRankAsync);
            RegisterAdminCommand("fprs_addxp",   "<player id> <amount>",     2, AddXpAsync);
            RegisterAdminCommand("fprs_setdept", "<player id> <department>", 2, SetDepartmentAsync);
            RegisterAdminCommand("fprs_offduty", "<player id>",              1, ForceOffDutyAsync);
            RegisterAdminCommand("fprs_endcall", "<call id>",                1, EndCallAsync);
            RegisterAdminCommand("fprs_units",   "",                         0, ListUnitsAsync);
        }

        private void RegisterAdminCommand(string name, string usage, int requiredArgs, Func<Player?, string[], Task<string>> handler)
        {
            API.RegisterCommand(name, new Action<int, List<object>, string>(async (source, rawArgs, _) =>
            {
                var caller = source == 0 ? null : Players[source];

                if (caller is not null && !_permissions.IsAdmin(caller.Handle))
                {
                    Reply(caller, "You do not have permission to use this command.");
                    Audit(AuditActions.PermissionDenied, caller, null, name);
                    return;
                }

                var args = rawArgs.Select(a => a?.ToString() ?? string.Empty).ToArray();
                if (args.Length < requiredArgs)
                {
                    Reply(caller, $"Usage: {name} {usage}".TrimEnd());
                    return;
                }

                try
                {
                    Reply(caller, await handler(caller, args));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[FivePRS] {name} failed: {ex}");
                    Reply(caller, $"{name} failed: {ex.Message}");
                }
            }), false);
        }

        private async Task<string> SetRankAsync(Player? caller, string[] args)
        {
            if (!TryGetTarget(args[0], out var target, out var data)) return TargetNotFound(args[0]);
            if (!int.TryParse(args[1], out var rank) || rank < 1) return "Rank must be a positive number.";

            data.Rank = rank;
            data.XP   = 0;
            await _db.SavePlayerAsync(data);

            SendPlayerData(target, data);
            _dispatch.UpdateRank(ServerId(target), rank);
            Notify(target, $"~y~Your rank was set to {rank} by an administrator.");
            Audit(AuditActions.AdminSetRank, caller, data.License, $"rank {rank}");

            return $"{data.Name} is now rank {rank}.";
        }

        private async Task<string> AddXpAsync(Player? caller, string[] args)
        {
            if (!TryGetTarget(args[0], out var target, out var data)) return TargetNotFound(args[0]);
            if (!int.TryParse(args[1], out var amount) || amount < 1) return "Amount must be a positive number.";

            await GrantXpAsync(target, data, amount);
            Notify(target, $"~y~An administrator granted you {amount} XP.");
            Audit(AuditActions.AdminAddXp, caller, data.License, $"+{amount}");

            return $"Granted {amount} XP to {data.Name} (rank {data.Rank}, {data.XP} XP).";
        }

        private async Task<string> SetDepartmentAsync(Player? caller, string[] args)
        {
            if (!TryGetTarget(args[0], out var target, out var data)) return TargetNotFound(args[0]);
            if (!Enum.TryParse<Department>(args[1], true, out var department) ||
                !Enum.IsDefined(typeof(Department), department) ||
                department == Department.None)
            {
                return $"Unknown department '{args[1]}'.";
            }

            await ChangeDepartmentAsync(target, data, department);
            Notify(target, $"~y~Your department was set to {department} by an administrator.");
            Audit(AuditActions.AdminSetDept, caller, data.License, department.ToString());

            return $"{data.Name} is now in {department}.";
        }

        private async Task<string> ForceOffDutyAsync(Player? caller, string[] args)
        {
            if (!TryGetTarget(args[0], out var target, out var data)) return TargetNotFound(args[0]);
            if (!data.IsOnDuty) return $"{data.Name} is not on duty.";

            await SetDutyAsync(target, data, false);
            Notify(target, "~y~You were placed off duty by an administrator.");
            Audit(AuditActions.AdminOffDuty, caller, data.License, data.Department.ToString());

            return $"{data.Name} is now off duty.";
        }

        private Task<string> EndCallAsync(Player? caller, string[] args)
        {
            var callId = args[0].TrimStart('#');
            var emergencyCaller = _dispatch.GetEmergencyCaller(callId);
            var primary = _dispatch.ForceClose(callId);
            if (primary is null) return Task.FromResult($"Call #{callId} is not active.");

            string? targetLicense = null;
            if (primary.Value > 0)
            {
                var owner = Players[primary.Value];
                TriggerClientEvent(owner, EventNames.ClientEndCallout);
                Notify(owner, $"~o~Call #{callId} was closed by dispatch.");
                targetLicense = GetLicense(owner);
            }

            if (emergencyCaller is not null)
                SendEmergencyStatus(emergencyCaller.Value, $"Your 911 call #{callId} was closed by dispatch.");

            Audit(AuditActions.AdminEndCall, caller, targetLicense, $"call #{callId}");

            return Task.FromResult($"Call #{callId} closed.");
        }

        private Task<string> ListUnitsAsync(Player? caller, string[] args)
        {
            var units = _dispatch.CreateSnapshot().Units;
            if (units.Count == 0) return Task.FromResult("No units on duty.");

            var lines = units.Select(u =>
                $"{u.Callsign} {u.Name} [{u.Status}]{(u.CallId is null ? string.Empty : $" call #{u.CallId}")}");

            return Task.FromResult(string.Join("\n", lines));
        }

        private bool TryGetTarget(string arg, out Player target, out PlayerData data)
        {
            target = null!;
            data   = null!;

            if (!int.TryParse(arg, out var id) || string.IsNullOrEmpty(API.GetPlayerName(id.ToString()))) return false;

            target = Players[id];
            return TryGetCached(target, out _, out data);
        }

        private static string TargetNotFound(string arg) => $"No loaded player with server id '{arg}'.";

        private void Reply(Player? caller, string message)
        {
            if (caller is null)
                Debug.WriteLine($"[FivePRS] {message}");
            else
                Notify(caller, message.Replace("\n", "~n~"));
        }
    }
}
