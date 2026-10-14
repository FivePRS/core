using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Logging;
using FivePRS.Core.Models;
using FivePRS.Server.Database;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const int MaxXpGrant = 1_000_000;

        private void RegisterAdminCommands()
        {
            RegisterAdminCommand("fprs_setrank", "<player id> <rank>",       2, SetRankAsync);
            RegisterAdminCommand("fprs_addxp",   "<player id> <amount>",     2, AddXpAsync);
            RegisterAdminCommand("fprs_setdept", "<player id> <department>", 2, SetDepartmentAsync);
            RegisterAdminCommand("fprs_offduty", "<player id>",              1, ForceOffDutyAsync);
            RegisterAdminCommand("fprs_endcall", "<call id>",                1, EndCallAsync);
            RegisterAdminCommand("fprs_units",   "",                         0, ListUnitsAsync);
        }

        private void RegisterAdminCommand(string name, string usage, int requiredArgs, Func<Player?, string[], Task<CommandResult>> handler)
        {
            API.RegisterCommand(name, new Action<int, List<object>, string>(async (source, rawArgs, _) =>
            {
                var caller = source == 0 ? null : Players[source];

                if (caller is not null && !_permissions.IsAdmin(caller.Handle))
                {
                    Reply(caller, CommandResult.Failed("You do not have permission to use this command."));
                    Audit(AuditActions.PermissionDenied, caller, null, name);
                    return;
                }

                var args = rawArgs.Select(a => a?.ToString()?.Trim() ?? string.Empty).Where(a => a.Length > 0).ToArray();
                if (args.Length != requiredArgs)
                {
                    Reply(caller, CommandResult.Failed($"Usage: {name} {usage}".TrimEnd()));
                    return;
                }

                try
                {
                    Reply(caller, await handler(caller, args));
                }
                catch (Exception ex)
                {
                    Log.Error($"{name} failed", ex);
                    Reply(caller, CommandResult.Failed($"{name} failed: {LogEntry.Cause(ex).Message}"));
                }
            }), false);
        }

        private async Task<CommandResult> SetRankAsync(Player? caller, string[] args)
        {
            if (!TryGetTarget(args[0], out var target, out var data)) return TargetNotFound(args[0]);
            var ladder = data.Ladder();
            if (!int.TryParse(args[1], out var rank) || rank < 1 || rank > ladder.Count)
                return CommandResult.Failed($"Rank must be a number from 1 to {ladder.Count} for {data.Department}.");

            data.Rank = rank;
            data.XP   = 0;
            await _db.SavePlayerAsync(data);

            var name = data.RankName();
            SendPlayerData(target, data);
            _dispatch.UpdateRank(ServerId(target), rank);
            Notify(target, $"~y~An administrator made you {name}.");
            Audit(AuditActions.AdminSetRank, caller, data.License, $"rank {rank} ({name})");

            return CommandResult.Done($"{data.Name} is now {name} (rank {rank}).");
        }

        private async Task<CommandResult> AddXpAsync(Player? caller, string[] args)
        {
            if (!TryGetTarget(args[0], out var target, out var data)) return TargetNotFound(args[0]);
            if (!int.TryParse(args[1], out var amount) || amount < 1 || amount > MaxXpGrant)
                return CommandResult.Failed($"Amount must be a number from 1 to {MaxXpGrant:N0}.");

            await GrantXpAsync(target, data, amount);
            Notify(target, $"~y~An administrator granted you {amount} XP.");
            Audit(AuditActions.AdminAddXp, caller, data.License, $"+{amount}");

            return CommandResult.Done($"Granted {amount} XP to {data.Name} ({data.RankName()}, {data.XP} XP).");
        }

        private async Task<CommandResult> SetDepartmentAsync(Player? caller, string[] args)
        {
            if (!TryGetTarget(args[0], out var target, out var data)) return TargetNotFound(args[0]);
            var department = Enum.GetValues(typeof(Department)).Cast<Department>()
                .FirstOrDefault(d => d != Department.None && string.Equals(d.ToString(), args[1], StringComparison.OrdinalIgnoreCase));
            if (department == Department.None)
            {
                var names = Enum.GetValues(typeof(Department)).Cast<Department>().Where(d => d != Department.None);
                return CommandResult.Failed($"Unknown department '{args[1]}'. Use {string.Join(", ", names)}.");
            }

            await ChangeDepartmentAsync(target, data, department);
            Notify(target, $"~y~Your department was set to {department} by an administrator.");
            Audit(AuditActions.AdminSetDept, caller, data.License, department.ToString());

            return CommandResult.Done($"{data.Name} is now in {department}.");
        }

        private async Task<CommandResult> ForceOffDutyAsync(Player? caller, string[] args)
        {
            if (!TryGetTarget(args[0], out var target, out var data)) return TargetNotFound(args[0]);
            if (!data.IsOnDuty) return CommandResult.Failed($"{data.Name} is not on duty.");

            await SetDutyAsync(target, data, false);
            Notify(target, "~y~You were placed off duty by an administrator.");
            Audit(AuditActions.AdminOffDuty, caller, data.License, data.Department.ToString());

            return CommandResult.Done($"{data.Name} is now off duty.");
        }

        private Task<CommandResult> EndCallAsync(Player? caller, string[] args)
        {
            var callId = args[0].TrimStart('#');
            var emergencyCaller = _dispatch.GetEmergencyCaller(callId);
            var primary = _dispatch.ForceClose(callId);
            if (primary is null) return Task.FromResult(CommandResult.Failed($"Call #{callId} is not active."));

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

            return Task.FromResult(CommandResult.Done($"Call #{callId} closed."));
        }

        private Task<CommandResult> ListUnitsAsync(Player? caller, string[] args)
        {
            var units = _dispatch.CreateSnapshot().Units;
            if (units.Count == 0) return Task.FromResult(CommandResult.Done("No units on duty."));

            var lines = units.Select(u =>
                $"{u.Callsign} {u.Name} [{u.Status}]{(u.CallId is null ? string.Empty : $" call #{u.CallId}")}");

            return Task.FromResult(CommandResult.Done(string.Join("\n", lines)));
        }

        private bool TryGetTarget(string arg, out Player target, out PlayerData data)
        {
            target = null!;
            data   = null!;

            if (!int.TryParse(arg, out var id) || string.IsNullOrEmpty(API.GetPlayerName(id.ToString()))) return false;

            target = Players[id];
            return TryGetCached(target, out _, out data);
        }

        private static CommandResult TargetNotFound(string arg) => CommandResult.Failed($"No player with server id '{arg}' is loaded.");

        private void Reply(Player? caller, string message)
        {
            if (caller is null)
                Log.Info(message);
            else
                Notify(caller, message.Replace("\n", "~n~"));
        }

        private void Reply(Player? caller, CommandResult result)
        {
            if (caller is null)
            {
                if (result.Succeeded)
                    Log.Success(result.Message);
                else
                    Log.Warn(result.Message);
                return;
            }

            Notify(caller, (result.Succeeded ? "~g~" : "~r~") + result.Message.Replace("\n", "~n~~w~"));
        }
    }
}
