using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Logging;
using FivePRS.Core.Models;
using FivePRS.Server.Civilian;
using FivePRS.Server.Database;
using FivePRS.Server.Framework;
using FivePRS.Server.Http;
using FivePRS.Server.Logging;
using FivePRS.Server.Dispatch;
using FivePRS.Server.Permissions;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain : BaseScript
    {
        private static readonly Logger Log = Logger.For("Server");

        private const int SnapshotIntervalMs = 5_000;
        private const int MaxCalloutCatalogJson = 200_000;

        private readonly ConcurrentDictionary<string, PlayerData> _cache = new();
        private readonly DatabaseManager _db = new();
        private readonly DispatchService _dispatch;
        private readonly PermissionService _permissions;

        private long _lastSnapshotAt;
        private DatabaseType _databaseType;

        public ServerBrain()
        {
            Resource.Capture();
            ServerLogging.Start(Resource.Path);
            foreach (var name in RetiredFiles.Remove(Path.Combine(Resource.Path, "server"), warning => Log.Warn(warning)))
                Log.Info($"Removed server/{name}, left over from an older version.");
            API.SetConvarServerInfo("fiveprs_version", InstalledVersion().ToString());
            MainThread.Enable();
            var resource = Resource.Name;
            ConfigLayout.Update(Resource.Path);
            ConfigManager.LoadAll(path => API.LoadResourceFile(resource, path));

            _dispatch    = new DispatchService(
                () => DateTime.UtcNow,
                () => ConfigManager.Settings,
                () => ConfigManager.Territories,
                new Random());
            _permissions = new PermissionService(
                (playerId, ace) => API.IsPlayerAceAllowed(playerId, ace),
                () => API.GetConvar("fiveprs_restrict_departments", "false").Equals("true", StringComparison.OrdinalIgnoreCase),
                IsOnRoster);

            EventHandlers["playerConnecting"] += new Action<Player, string, dynamic, dynamic>(OnPlayerConnecting);
            EventHandlers["playerDropped"]    += new Action<Player, string>(OnPlayerDropped);

            EventHandlers[EventNames.ServerPlayerConnected]  += new Action<Player>(OnPlayerReady);
            EventHandlers[EventNames.ServerToggleDuty]       += new Action<Player>(OnToggleDuty);
            EventHandlers[EventNames.ServerEnterService]     += new Action<Player, int, string, string>(OnEnterService);
            EventHandlers[EventNames.ServerRegisterCallouts] += new Action<Player, string>(OnRegisterCallouts);
            EventHandlers[EventNames.ServerCalloutResponse]  += new Action<Player, string, int, float, float, float>(OnCalloutResponse);
            EventHandlers[EventNames.ServerCalloutEnded]     += new Action<Player, string, int>(OnCalloutEnded);
            EventHandlers[EventNames.ServerSetUnitStatus]    += new Action<Player, int>(OnSetUnitStatus);
            EventHandlers[EventNames.ServerAttachToCall]     += new Action<Player, string>(OnAttachToCall);
            EventHandlers[EventNames.ServerSetAiCallouts]    += new Action<Player, bool>(OnSetAiCallouts);
            EventHandlers[EventNames.LocalHttpResponse]      += new Action<string, int, string, string>(HttpBridge.OnResponse);
            EventHandlers[EventNames.LocalDbResponse]        += new Action<string, string, string>(OxMySqlBridge.OnResponse);

            Tick += DrainMainThreadAsync;
            Tick += DispatchTickAsync;
            RegisterAdminCommands();
            RegisterCivilianEvents();
            RegisterLookupEvents();
            RegisterEmergencyEvents();
            RegisterBackupEvents();
            RegisterRosterEvents();
            RegisterAppearanceEvents();
            RegisterTerminalEvents();
            RegisterPositionEvents();
            RegisterUpdates();
            RegisterDiagnostics();
            RegisterStreetEvents();
            RegisterReportEvents();
            RegisterFrameworkEvents();
            RegisterDeathEvents();

            _ = ServerIcon.ApplyDefaultAsync(ConfigManager.Settings.Branding.ServerIcon);
            _ = InitDbAsync();
        }

        private static Task DrainMainThreadAsync()
        {
            MainThread.Tick();
            return Task.FromResult(0);
        }

        private async Task InitDbAsync()
        {
            var connString = API.GetConvar("fiveprs_db_connection", "");
            try
            {
                var dbTypeRaw  = API.GetConvar("fiveprs_db_type", "sqlite").ToLowerInvariant();

                var dbType = dbTypeRaw == "mysql" ? DatabaseType.MySQL : DatabaseType.SQLite;
                _databaseType = dbType;
                await _db.InitializeAsync(dbType, string.IsNullOrEmpty(connString) ? null : connString);
                _civilians = new CivilianService(_db.Civilians, _db.Photos, () => ConfigManager.Settings, () => ConfigManager.Licenses, () => DateTime.UtcNow);
                _lookup    = new LookupService(_db.Civilians, _db.Photos, () => ConfigManager.Settings, () => ConfigManager.Licenses);
                _street    = new StreetService(_db.Civilians, _lookup);
                _reports   = new ReportService(_db.Reports, _db.Civilians, _street);
                _appearances = new AppearanceService(_db.Civilians, _db.Appearances, () => ConfigManager.Settings.Creator);
                _roster    = new RosterService(_db.Roster);
                await _roster.LoadAsync();

                LogStartup();
                await DetectFrameworkAsync();
            }
            catch (Exception ex) when (ex is NotSupportedException || ex is DatabaseSetupException)
            {
                Log.Error($"FivePRS could not start: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                Log.Error($"FivePRS could not start: {ex.Message} The server needs write access to the fiveprs folder.");
            }
            catch (DatabaseQueryException ex)
            {
                Log.Error($"The database didn't start: {ex.Message}");
            }
            catch (Exception ex)
            {
                Log.Error("The database didn't start", ex);
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
                Log.Error($"Could not load {playerName}", ex);
                deferrals.done("Error loading your profile. Please try again.");
            }
        }

        private void OnPlayerDropped([FromSource] Player player, string reason)
        {
            _rateLimiter.Forget(player.Handle);
            ClearDowned(ServerId(player));
            _dispatch.SetOffDuty(ServerId(player));
            ForgetFrameworkCharacter(player);

            var license = GetLicense(player);
            if (license is null) return;

            if (_cache.TryRemove(license, out var data) && data.IsOnDuty)
            {
                data.IsOnDuty = false;
                EndShift(data);
                _ = _db.SavePlayerAsync(data);
            }

            _ = ForgetPositionAsync(license);
        }

        private async void OnPlayerReady([FromSource] Player player)
        {
            if (!Allow(player, "requests", l => l.Requests)) return;
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
                SendDownedPlayers(player);
                await SyncFrameworkAsync(player);
                await SendAppearanceAsync(player, license);
                await SendPreferencesAsync(player, license);

                if (_permissions.IsAdmin(player.Handle)) NotifyUpdate(player);
            }
            catch (Exception ex)
            {
                Log.Error($"Could not send {player.Name}'s profile", ex);
            }
        }

        private async void OnEnterService([FromSource] Player player, int departmentId, string agencyId, string callsign)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;
            if (!TryGetCached(player, out var license, out var data) || data.IsOnDuty) return;
            if (!Enum.IsDefined(typeof(Department), departmentId) || departmentId == (int)Department.None) return;

            var department = (Department)departmentId;
            if (!CanJoin(player, department))
            {
                TriggerClientEvent(player, EventNames.ClientEntryRejected, UsesFramework
                    ? $"Your character's job doesn't let you join {department}."
                    : $"You are not authorised to join {department}.");
                Audit(AuditActions.PermissionDenied, player, license, $"department {department}");
                return;
            }

            if (!Callsign.TryNormalize(callsign, out var normalizedCallsign))
            {
                TriggerClientEvent(player, EventNames.ClientEntryRejected,
                    $"Callsigns can be up to {Callsign.MaxLength} letters, numbers or hyphens.");
                return;
            }

            if (normalizedCallsign.Length > 0 && _dispatch.IsCallsignTaken(normalizedCallsign, ServerId(player)))
            {
                TriggerClientEvent(player, EventNames.ClientEntryRejected, $"Callsign {normalizedCallsign} is already in use.");
                return;
            }

            try
            {
                var agency = ConfigManager.Territories.ResolveAgency(department, FrameworkAgency(player, department) ?? agencyId)?.Id ?? string.Empty;
                data.Callsign = normalizedCallsign;

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
                Log.Error($"Could not put {player.Name} into service", ex);
                TriggerClientEvent(player, EventNames.ClientEntryRejected, "Something went wrong. Please try again.");
            }
        }

        private void SendEntryOptions(Player player)
        {
            var allowed = new List<int>();
            foreach (Department department in Enum.GetValues(typeof(Department)))
            {
                if (CanJoin(player, department))
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
                        ["rankName"] = data.Department == Department.None ? string.Empty : data.RankName(),
                        ["agency"] = agency,
                    },
                });
            }
            catch (Exception ex)
            {
                Log.Warn("Loading screen handover failed", ex);
            }
        }

        private async void OnToggleDuty([FromSource] Player player)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;
            if (!TryGetCached(player, out var license, out var data)) return;
            if (!data.IsOnDuty && data.Department == Department.None) return;

            var goingOnDuty = !data.IsOnDuty;
            if (goingOnDuty && !CanJoin(player, data.Department))
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
                Log.Error($"Could not change duty for {player.Name}", ex);
            }
        }

        private void OnRegisterCallouts([FromSource] Player player, string json)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;
            if (!_dispatch.IsOnDuty(ServerId(player)) || json is null || json.Length > MaxCalloutCatalogJson) return;

            try
            {
                var definitions = JsonConvert.DeserializeObject<List<CalloutDefinition>>(json);
                if (definitions is null) return;

                if (_dispatch.RegisterCallouts(definitions, ReadMaxXp()) == 0) return;
                Log.Debug($"Dispatch catalog has {_dispatch.CatalogCount} callouts.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Invalid callout catalog from {player.Name}", ex);
            }
        }

        private void OnCalloutResponse([FromSource] Player player, string callId, int response, float x, float y, float z)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;
            if (!IsCallId(callId) || !IsFinite(x) || !IsFinite(y) || !IsFinite(z)) return;
            if (!Enum.IsDefined(typeof(OfferResponse), response)) return;
            _dispatch.Respond(ServerId(player), callId, (OfferResponse)response, x, y, z);
        }

        private async void OnCalloutEnded([FromSource] Player player, string callId, int result)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;
            if (!IsCallId(callId) || !Enum.IsDefined(typeof(CalloutResult), result)) return;

            foreach (var award in _dispatch.End(ServerId(player), callId, (CalloutResult)result))
                await AwardXpAsync(award.UnitId, award.Amount, callId);
        }

        private void OnSetAiCallouts([FromSource] Player player, bool enabled)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;
            if (_dispatch.SetAiCallouts(ServerId(player), enabled))
                Notify(player, enabled
                    ? "Dispatch | ~g~AI callouts on.~w~ Dispatch will offer you calls again."
                    : "Dispatch | ~o~AI callouts off.~w~ Player 911 calls still reach you.");
        }

        private void OnSetUnitStatus([FromSource] Player player, int status)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;
            if (!Enum.IsDefined(typeof(UnitStatus), status)) return;

            var id = ServerId(player);
            if (!_dispatch.UnitIds.Contains(id))
                Notify(player, "~r~You must be on duty to set your status.");
            else if (!_dispatch.SetStatus(id, (UnitStatus)status))
                Notify(player, "~r~Status unchanged.~w~ End your active callout or answer your call offer first.");
        }

        private void OnAttachToCall([FromSource] Player player, string callId)
        {
            if (!Allow(player, "dispatch", l => l.Dispatch)) return;
            var id = ServerId(player);
            if (!_dispatch.UnitIds.Contains(id))
            {
                Notify(player, "~r~You must be on duty to respond to calls.");
                return;
            }

            if (!IsCallId(callId) || !_dispatch.Attach(id, callId))
            {
                Notify(player, $"~r~Unable to attach to call #{callId}.~w~ Check the call is active and you aren't already on a call.");
                return;
            }

            Notify(player, $"~g~Attached to call ~y~#{callId}~w~. Waypoint set.");

            var caller = _dispatch.GetEmergencyCaller(callId);
            if (caller is not null)
                SendEmergencyStatus(caller.Value, "~b~911~w~ | Units are responding to your call.");
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

            data.CallsCompleted++;
            if (!XpActive)
            {
                await _db.SavePlayerAsync(data);
                SendPlayerData(player, data);
                Notify(player, $"~g~CALL #{callId} CLOSED");
                return;
            }

            var awardedXP = (int)Math.Min(Math.Max(baseAmount * ReadXpMultiplier(), 0), ReadMaxXp());

            try
            {
                await GrantXpAsync(player, data, awardedXP);
                Notify(player, $"~g~CALL #{callId} CLOSED~w~ | ~y~+{awardedXP} XP");
                Audit(AuditActions.XpAwarded, player, data.License, $"+{awardedXP} call #{callId}");
            }
            catch (Exception ex)
            {
                Log.Error($"Could not award XP for call #{callId}", ex);
            }
        }

        private async Task SetDutyAsync(Player player, PlayerData data, bool onDuty)
        {
            data.IsOnDuty = onDuty;

            if (onDuty)
            {
                _dispatch.SetOnDuty(ServerId(player), data.Name, data.Department, data.Rank, data.Agency, data.Callsign);
                data.ShiftStartedAt = DateTime.UtcNow;
            }
            else
            {
                _dispatch.SetOffDuty(ServerId(player));
                EndShift(data);
            }

            TriggerClientEvent(player, EventNames.ClientDutyStatusChanged, onDuty, (int)data.Department);
            SendPlayerData(player, data);
            SyncFrameworkDuty(player, onDuty);
            await _db.SavePlayerAsync(data);
            if (onDuty && ReportService.CanRead(data.Department)) await PushReportsAsync(data.License, null);

            Log.Info($"{data.Name} is {(onDuty ? "on" : "off")} duty.");
        }

        private void EndShift(PlayerData data)
        {
            if (data.ShiftStartedAt is not { } started) return;

            data.ShiftStartedAt = null;
            var seconds = (long)data.DutySeconds + (long)Math.Max(0, (DateTime.UtcNow - started).TotalSeconds);
            data.DutySeconds = (int)Math.Min(int.MaxValue, seconds);
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
            var rank = data.Rank;
            var rankedUp = data.Ladder().AddXp(data, amount);
            if (UsesFramework)
            {
                data.Rank = rank;
                rankedUp = false;
            }
            await _db.SavePlayerAsync(data);
            SendPlayerData(player, data);

            if (!rankedUp) return;

            _dispatch.UpdateRank(ServerId(player), data.Rank);
            TriggerClientEvent(player, EventNames.ClientRankedUp, data.Rank);
            Audit(AuditActions.RankUp, player, data.License, $"rank {data.Rank} ({data.RankName()})");
            Log.Success($"{data.Name} was promoted to {data.RankName()}.");
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
                Log.Error($"Could not write audit entry '{entry.Action}'", ex);
            }
        }

        private async Task<PlayerData> LoadProfileAsync(string license, string playerName)
        {
            var data = await _db.GetPlayerAsync(license);
            if (data is null)
            {
                data = new PlayerData { License = license };
                Log.Info($"New player: {playerName}.");
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

        private bool XpActive => ConfigManager.Settings.XpEnabled && !UsesFramework;

        private void SendPlayerData(Player player, PlayerData data)
        {
            data.UsesXp = XpActive;
            data.Framework = _framework is null ? null : FrameworkBridge.DisplayName(_framework.Info.Name);
            TriggerClientEvent(player, EventNames.ClientReceivePlayerData, JsonConvert.SerializeObject(data));
        }

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
