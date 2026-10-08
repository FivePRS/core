using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using FivePRS.Server.Dispatch;
using FivePRS.Server.Permissions;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain : BaseScript
    {
        private const int SnapshotIntervalMs = 5_000;

        private readonly ConcurrentDictionary<string, PlayerData> _cache = new();
        private readonly DatabaseManager _db = new();
        private readonly DispatchService _dispatch;
        private readonly PermissionService _permissions;

        private long _lastSnapshotAt;

        public ServerBrain()
        {
            ConfigManager.Log = message => Debug.WriteLine(message);
            var resource = API.GetCurrentResourceName();
            ConfigManager.LoadSettings(API.LoadResourceFile(resource, "config/settings.json"));
            ConfigManager.LoadJurisdictions(API.LoadResourceFile(resource, "config/jurisdictions.json"));

            _dispatch    = new DispatchService(
                () => DateTime.UtcNow,
                () => ConfigManager.Settings,
                () => ConfigManager.Territories,
                new Random());
            _permissions = new PermissionService(
                (playerId, ace) => API.IsPlayerAceAllowed(playerId, ace),
                () => API.GetConvar("fiveprs_restrict_departments", "false").Equals("true", StringComparison.OrdinalIgnoreCase));

            EventHandlers["playerConnecting"] += new Action<Player, string, dynamic, dynamic>(OnPlayerConnecting);
            EventHandlers["playerDropped"]    += new Action<Player, string>(OnPlayerDropped);

            EventHandlers[EventNames.ServerPlayerConnected]  += new Action<Player>(OnPlayerReady);
            EventHandlers[EventNames.ServerToggleDuty]       += new Action<Player>(OnToggleDuty);
            EventHandlers[EventNames.ServerSetDepartment]    += new Action<Player, int>(OnSetDepartment);
            EventHandlers[EventNames.ServerSetAgency]        += new Action<Player, string>(OnSetAgency);
            EventHandlers[EventNames.ServerEnterService]     += new Action<Player, int, string>(OnEnterService);
            EventHandlers[EventNames.ServerRegisterCallouts] += new Action<Player, string>(OnRegisterCallouts);
            EventHandlers[EventNames.ServerCalloutResponse]  += new Action<Player, string, int, float, float, float>(OnCalloutResponse);
            EventHandlers[EventNames.ServerCalloutEnded]     += new Action<Player, string, int>(OnCalloutEnded);
            EventHandlers[EventNames.ServerSetUnitStatus]    += new Action<Player, int>(OnSetUnitStatus);
            EventHandlers[EventNames.ServerAttachToCall]     += new Action<Player, string>(OnAttachToCall);

            Tick += DispatchTickAsync;
            RegisterAdminCommands();

            _ = InitDbAsync();
        }

        private async Task InitDbAsync()
        {
            try
            {
                var dbTypeRaw  = API.GetConvar("fiveprs_db_type", "sqlite").ToLowerInvariant();
                var connString = API.GetConvar("fiveprs_db_connection", "");

                var dbType = dbTypeRaw == "mysql" ? DatabaseType.MySQL : DatabaseType.SQLite;
                await _db.InitializeAsync(dbType, string.IsNullOrEmpty(connString) ? null : connString);

                Debug.WriteLine("[FivePRS] ServerBrain online.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] FATAL: DB init failed: {ex}");
            }
        }

        private async void OnPlayerConnecting(
            [FromSource] Player player,
            string playerName,
            dynamic setKickReason,
            dynamic deferrals)
        {
            deferrals.defer();
            await Delay(0);

            if (!_db.IsReady)
            {
                deferrals.done("FivePRS database is not ready. Please try again in a moment.");
                return;
            }

            var license = GetLicense(player);
            if (license is null)
            {
                deferrals.done("A valid FiveM license identifier is required.");
                return;
            }

            deferrals.update($"Loading your FivePRS profile, {playerName}...");

            try
            {
                var data = await LoadProfileAsync(license, playerName);
                HandOverProfile(deferrals, data);
                deferrals.done();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error loading {playerName}: {ex.Message}");
                deferrals.done("Error loading your profile. Please try again.");
            }
        }

        private void OnPlayerDropped([FromSource] Player player, string reason)
        {
            _dispatch.SetOffDuty(ServerId(player));

            var license = GetLicense(player);
            if (license is null) return;

            if (_cache.TryRemove(license, out var data) && data.IsOnDuty)
                _ = _db.UpdateDutyStatusAsync(license, false);
        }

        private async void OnPlayerReady([FromSource] Player player)
        {
            var license = GetLicense(player);
            if (license is null) return;

            try
            {
                if (!_cache.TryGetValue(license, out var data))
                {
                    if (!_db.IsReady) return;
                    data = await LoadProfileAsync(license, player.Name);
                }

                SendPlayerData(player, data);
                SendEntryOptions(player);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error sending profile to {player.Name}: {ex.Message}");
            }
        }

        private async void OnEnterService([FromSource] Player player, int departmentId, string agencyId)
        {
            if (!TryGetCached(player, out var license, out var data) || data.IsOnDuty) return;
            if (!Enum.IsDefined(typeof(Department), departmentId) || departmentId == (int)Department.None) return;

            var department = (Department)departmentId;
            if (!_permissions.CanJoinDepartment(player.Handle, department))
            {
                TriggerClientEvent(player, EventNames.ClientEntryRejected, $"You are not authorised to join {department}.");
                Audit(AuditActions.PermissionDenied, player, license, $"department {department}");
                return;
            }

            try
            {
                var agency = ConfigManager.Territories.ResolveAgency(department, agencyId)?.Id ?? string.Empty;

                if (data.Department != department)
                {
                    data.Department = department;
                    Audit(AuditActions.DepartmentSet, player, license, department.ToString());
                }

                if (data.Agency != agency)
                {
                    data.Agency = agency;
                    Audit(AuditActions.AgencySet, player, license, agency);
                }

                await _db.SavePlayerAsync(data);
                SendPlayerData(player, data);

                await SetDutyAsync(player, data, true);
                Audit(AuditActions.DutyOn, player, license, department.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error entering service for {player.Name}: {ex.Message}");
                TriggerClientEvent(player, EventNames.ClientEntryRejected, "Something went wrong. Please try again.");
            }
        }

        private void SendEntryOptions(Player player)
        {
            var allowed = new List<int>();
            foreach (Department department in Enum.GetValues(typeof(Department)))
            {
                if (_permissions.CanJoinDepartment(player.Handle, department))
                    allowed.Add((int)department);
            }

            TriggerClientEvent(player, EventNames.ClientEntryOptions, JsonConvert.SerializeObject(allowed));
        }

        private static void HandOverProfile(dynamic deferrals, PlayerData data)
        {
            try
            {
                var agency = data.Department == Department.None
                    ? string.Empty
                    : ConfigManager.Territories.FindAgency(data.Agency)?.Name ?? data.Department.ToString();

                deferrals.handover(new Dictionary<string, object>
                {
                    ["fiveprs"] = new Dictionary<string, object>
                    {
                        ["name"]   = data.Name,
                        ["rank"]   = data.Rank,
                        ["agency"] = agency,
                    },
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Loading screen handover failed: {ex.Message}");
            }
        }

        private async void OnToggleDuty([FromSource] Player player)
        {
            if (!TryGetCached(player, out var license, out var data)) return;
            if (!data.IsOnDuty && data.Department == Department.None) return;

            var goingOnDuty = !data.IsOnDuty;
            if (goingOnDuty && !_permissions.CanJoinDepartment(player.Handle, data.Department))
            {
                Notify(player, $"~r~You are not authorised to go on duty with {data.Department}.");
                Audit(AuditActions.PermissionDenied, player, license, $"duty {data.Department}");
                return;
            }

            try
            {
                await SetDutyAsync(player, data, goingOnDuty);
                Audit(goingOnDuty ? AuditActions.DutyOn : AuditActions.DutyOff, player, license, data.Department.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error toggling duty for {player.Name}: {ex.Message}");
            }
        }

        private async void OnSetDepartment([FromSource] Player player, int departmentId)
        {
            if (!Enum.IsDefined(typeof(Department), departmentId) || departmentId == (int)Department.None) return;
            if (!TryGetCached(player, out var license, out var data)) return;

            var department = (Department)departmentId;
            if (!_permissions.CanJoinDepartment(player.Handle, department))
            {
                Notify(player, $"~r~You are not authorised to join {department}.");
                Audit(AuditActions.PermissionDenied, player, license, $"department {department}");
                return;
            }

            try
            {
                await ChangeDepartmentAsync(player, data, department);
                Audit(AuditActions.DepartmentSet, player, license, department.ToString());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error setting department for {player.Name}: {ex.Message}");
            }
        }

        private async void OnSetAgency([FromSource] Player player, string agencyId)
        {
            if (!TryGetCached(player, out var license, out var data)) return;

            var agency = ConfigManager.Territories.FindAgency(agencyId);
            if (agency is null || agency.Department != data.Department)
            {
                Notify(player, $"~r~Unknown agency '{agencyId}' for {data.Department}.");
                return;
            }

            try
            {
                if (data.IsOnDuty)
                    await SetDutyAsync(player, data, false);

                data.Agency = agency.Id;
                await _db.SavePlayerAsync(data);
                SendPlayerData(player, data);

                Notify(player, $"~g~You are now with the {agency.Name}.");
                Audit(AuditActions.AgencySet, player, license, agency.Id);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error setting agency for {player.Name}: {ex.Message}");
            }
        }

        private void OnRegisterCallouts([FromSource] Player player, string json)
        {
            if (!_dispatch.IsOnDuty(ServerId(player))) return;

            try
            {
                var definitions = JsonConvert.DeserializeObject<List<CalloutDefinition>>(json);
                if (definitions is null) return;

                _dispatch.RegisterCallouts(definitions);
                Debug.WriteLine($"[FivePRS] Dispatch catalog has {_dispatch.CatalogCount} callout(s).");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Invalid callout catalog from {player.Name}: {ex.Message}");
            }
        }

        private void OnCalloutResponse([FromSource] Player player, string callId, int response, float x, float y, float z)
        {
            if (!Enum.IsDefined(typeof(OfferResponse), response)) return;
            _dispatch.Respond(ServerId(player), callId, (OfferResponse)response, x, y, z);
        }

        private async void OnCalloutEnded([FromSource] Player player, string callId, int result)
        {
            if (!Enum.IsDefined(typeof(CalloutResult), result)) return;

            foreach (var award in _dispatch.End(ServerId(player), callId, (CalloutResult)result))
                await AwardXpAsync(award.UnitId, award.Amount, callId);
        }

        private void OnSetUnitStatus([FromSource] Player player, int status)
        {
            if (!Enum.IsDefined(typeof(UnitStatus), status)) return;

            if (!_dispatch.SetStatus(ServerId(player), (UnitStatus)status))
                Notify(player, "~r~Status unchanged.~w~ End your active callout first.");
        }

        private void OnAttachToCall([FromSource] Player player, string callId)
        {
            Notify(player, _dispatch.Attach(ServerId(player), callId)
                ? $"~g~Attached to call ~y~#{callId}~w~. Waypoint set."
                : $"~r~Unable to attach to call #{callId}.~w~ Check the ID and your status.");
        }

        private async Task DispatchTickAsync()
        {
            await Delay(1_000);

            foreach (var unitId in _dispatch.UnitIds)
            {
                var ped = API.GetPlayerPed(unitId.ToString());
                if (ped == 0) continue;

                var pos = API.GetEntityCoords(ped);
                _dispatch.UpdatePosition(unitId, pos.X, pos.Y, pos.Z);
            }

            foreach (var offer in _dispatch.Tick())
                TriggerClientEvent(Players[offer.UnitId], EventNames.ClientCalloutOffered, JsonConvert.SerializeObject(offer.Callout));

            if (!_dispatch.IsDirty && API.GetGameTimer() - _lastSnapshotAt < SnapshotIntervalMs) return;

            _lastSnapshotAt = API.GetGameTimer();
            _dispatch.ClearDirty();
            var snapshot = JsonConvert.SerializeObject(_dispatch.CreateSnapshot());
            foreach (var unitId in _dispatch.UnitIds)
                TriggerClientEvent(Players[unitId], EventNames.ClientDispatchSnapshot, snapshot);
        }

        private async Task AwardXpAsync(int unitId, int baseAmount, string callId)
        {
            var player = Players[unitId];
            if (player is null || !TryGetCached(player, out _, out var data)) return;

            var awardedXP = (int)Math.Min(Math.Max(baseAmount * ReadXpMultiplier(), 0), ReadMaxXp());

            try
            {
                await GrantXpAsync(player, data, awardedXP);
                Notify(player, $"~g~CALL #{callId} CLOSED~w~ | ~y~+{awardedXP} XP");
                Audit(AuditActions.XpAwarded, player, data.License, $"+{awardedXP} call #{callId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Error awarding XP for call #{callId}: {ex.Message}");
            }
        }

        private async Task SetDutyAsync(Player player, PlayerData data, bool onDuty)
        {
            data.IsOnDuty = onDuty;

            if (onDuty)
                _dispatch.SetOnDuty(ServerId(player), data.Name, data.Department, data.Rank, data.Agency);
            else
                _dispatch.SetOffDuty(ServerId(player));

            TriggerClientEvent(player, EventNames.ClientDutyStatusChanged, onDuty, (int)data.Department);
            await _db.UpdateDutyStatusAsync(data.License, onDuty);

            Debug.WriteLine($"[FivePRS] {data.Name} is now {(onDuty ? "ON" : "OFF")} duty.");
        }

        private async Task ChangeDepartmentAsync(Player player, PlayerData data, Department department)
        {
            if (data.IsOnDuty)
                await SetDutyAsync(player, data, false);

            data.Department = department;
            data.Agency     = ConfigManager.Territories.DefaultAgency(department)?.Id ?? string.Empty;
            await _db.SavePlayerAsync(data);
            SendPlayerData(player, data);
        }

        private async Task GrantXpAsync(Player player, PlayerData data, int amount)
        {
            var rankedUp = data.AddXP(amount);
            await _db.SavePlayerAsync(data);
            SendPlayerData(player, data);

            if (!rankedUp) return;

            _dispatch.UpdateRank(ServerId(player), data.Rank);
            TriggerClientEvent(player, EventNames.ClientRankedUp, data.Rank);
            Audit(AuditActions.RankUp, player, data.License, $"rank {data.Rank}");
            Debug.WriteLine($"[FivePRS] {data.Name} ranked up to Rank {data.Rank}.");
        }

        private void Audit(string action, Player? actor, string? targetLicense, string details)
        {
            var entry = new AuditEntry
            {
                Action        = action,
                ActorLicense  = actor is null ? null : GetLicense(actor),
                ActorName     = actor?.Name ?? "console",
                TargetLicense = targetLicense,
                Details       = details,
            };

            _ = WriteAuditAsync(entry);
        }

        private async Task WriteAuditAsync(AuditEntry entry)
        {
            if (!_db.IsReady) return;

            try
            {
                await _db.AddAuditAsync(entry);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Failed to write audit entry '{entry.Action}': {ex.Message}");
            }
        }

        private async Task<PlayerData> LoadProfileAsync(string license, string playerName)
        {
            var data = await _db.GetPlayerAsync(license);
            if (data is null)
            {
                data = new PlayerData { License = license };
                Debug.WriteLine($"[FivePRS] New player registered: {playerName} ({license})");
            }

            data.Name     = playerName;
            data.IsOnDuty = false;
            data.Agency   = ConfigManager.Territories.ResolveAgency(data.Department, data.Agency)?.Id ?? string.Empty;
            await _db.SavePlayerAsync(data);

            _cache[license] = data;
            return data;
        }

        private bool TryGetCached(Player player, out string license, out PlayerData data)
        {
            license = GetLicense(player) ?? string.Empty;
            data    = null!;
            return license.Length > 0 && _cache.TryGetValue(license, out data!);
        }

        private static int ServerId(Player player) => int.Parse(player.Handle);

        private void Notify(Player player, string message) =>
            TriggerClientEvent(player, EventNames.ClientNotify, message);

        private void SendPlayerData(Player player, PlayerData data) =>
            TriggerClientEvent(player, EventNames.ClientReceivePlayerData, JsonConvert.SerializeObject(data));

        private static string? GetLicense(Player player)
        {
            var license = player.Identifiers["license"];
            return string.IsNullOrEmpty(license) ? null : license;
        }

        private static int ReadMaxXp() =>
            int.TryParse(API.GetConvar("fiveprs_max_xp", "500"), out var maxXp) && maxXp > 0 ? maxXp : 500;

        private static float ReadXpMultiplier() =>
            float.TryParse(API.GetConvar("fiveprs_xp_multiplier", "1.0"), NumberStyles.Float,
                CultureInfo.InvariantCulture, out var multiplier) && multiplier > 0 ? multiplier : 1.0f;
    }
}
