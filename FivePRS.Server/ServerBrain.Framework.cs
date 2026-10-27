using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Events;
using FivePRS.Core.Models;
using FivePRS.Server.Framework;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private const string TerminalKey = "F5";

        private FrameworkBridge? _framework;
        private CharacterLinker? _linker;
        private readonly ConcurrentDictionary<int, FrameworkCharacter> _frameworkCharacters = new();
        private readonly ConcurrentDictionary<int, FrameworkJobMatch> _frameworkJobs = new();

        private bool UsesFramework => _framework is not null;

        private void RegisterFrameworkEvents()
        {
            EventHandlers[EventNames.LocalFrameworkResponse] += new Action<string, string, string>(FrameworkBridge.OnResponse);
            EventHandlers[EventNames.LocalFrameworkChanged]  += new Action<int, string>(OnFrameworkChanged);
        }

        private async Task DetectFrameworkAsync()
        {
            var preferred = ConfigManager.Framework.Preferred;
            if (preferred != FrameworkConfig.Auto && preferred != FrameworkConfig.Standalone && !FrameworkConfig.Supported.Contains(preferred))
            {
                Log.Warn($"framework.json names an unknown framework \"{preferred}\". Use auto, standalone, {string.Join(", ", FrameworkConfig.Supported)}.");
                return;
            }

            try
            {
                _framework = await FrameworkBridge.DetectAsync(preferred);
            }
            catch (Exception ex)
            {
                Log.Error("Could not check for a framework, running standalone", ex);
                return;
            }

            if (_framework is null)
            {
                if (preferred != FrameworkConfig.Auto && preferred != FrameworkConfig.Standalone)
                    Log.Warn($"framework.json asks for {FrameworkBridge.DisplayName(preferred)}, but it isn't running. Running standalone.");
                return;
            }

            var name = FrameworkBridge.DisplayName(_framework.Info.Name);
            _linker = new CharacterLinker(_db.Civilians);
            if (_civilians is not null) _civilians.ManagedBy = name;
            if (_lookup is not null)
            {
                _lookup.ExternalPlate = LinkFrameworkPlateAsync;
                _lookup.CollectsFines = ConfigManager.Framework.Fines.Collect;
            }

            Log.Success($"Using {name} ({_framework.Info.Resource}) for jobs, duty, characters and vehicles.");
            foreach (var player in Players.ToList())
                await SyncFrameworkAsync(player);
        }

        private async void OnFrameworkChanged(int source, string kind)
        {
            if (_framework is null || source <= 0 || !TryGetPlayer(source, out var player)) return;

            if (kind == "unloaded")
            {
                await ClearFrameworkCharacterAsync(player, "~o~You logged out of your character.~w~ Your shift has ended.");
                return;
            }

            await SyncFrameworkAsync(player);
        }

        private async Task SyncFrameworkAsync(Player player)
        {
            if (_framework is null || !TryGetCached(player, out var license, out var data)) return;
            var source = ServerId(player);

            FrameworkCharacter? character;
            try
            {
                character = await _framework.CharacterAsync(source, ConfigManager.Framework.Jobs.Keys);
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not read {player.Name}'s character from {FrameworkBridge.DisplayName(_framework.Info.Name)}", ex);
                return;
            }

            if (character is null || string.IsNullOrEmpty(character.Id))
            {
                await ClearFrameworkCharacterAsync(player, null);
                return;
            }

            _frameworkCharacters.TryGetValue(source, out var previous);
            _frameworkCharacters[source] = character;

            if (character.FullName.Length > 0) data.Name = character.FullName;
            await LinkFrameworkCharacterAsync(player, license, character);

            var match = ConfigManager.Framework.Resolve(character.Job, character.Grade, department => ConfigManager.RanksFor(department).Count);
            if (match is null)
            {
                _frameworkJobs.TryRemove(source, out _);
                if (data.IsOnDuty) await EndFrameworkShiftAsync(player, data, "~o~Your job changed.~w~ Your FivePRS shift has ended.");
            }
            else
            {
                _frameworkJobs[source] = match;
                if (data.IsOnDuty && data.Department != match.Department)
                    await EndFrameworkShiftAsync(player, data, "~o~Your job changed.~w~ Your FivePRS shift has ended.");

                data.Department = match.Department;
                data.Agency     = ConfigManager.Territories.ResolveAgency(match.Department, match.Agency ?? data.Agency)?.Id ?? string.Empty;
                if (data.Rank != match.Rank)
                {
                    data.Rank = match.Rank;
                    if (data.IsOnDuty) _dispatch.UpdateRank(source, data.Rank);
                }

                if (character.TracksDuty && data.IsOnDuty && character.OnDuty == false)
                    await EndFrameworkShiftAsync(player, data, "~o~You clocked out.~w~ Your FivePRS shift has ended.");
                else if (character.TracksDuty && !data.IsOnDuty && character.OnDuty == true && previous?.OnDuty != true)
                    Notify(player, $"~b~FivePRS~w~ | You're clocked in. Open the terminal ({TerminalKey}) and start your shift from the Duty app.");
            }

            await _db.SavePlayerAsync(data);
            SendPlayerData(player, data);
            SendEntryOptions(player);
        }

        private async Task ClearFrameworkCharacterAsync(Player player, string? message)
        {
            var source = ServerId(player);
            _frameworkCharacters.TryRemove(source, out _);
            _frameworkJobs.TryRemove(source, out _);

            if (TryGetCached(player, out _, out var data) && data.IsOnDuty)
                await EndFrameworkShiftAsync(player, data, message);
            SendEntryOptions(player);
        }

        private async Task EndFrameworkShiftAsync(Player player, PlayerData data, string? message)
        {
            await SetDutyAsync(player, data, false);
            if (message is not null) Notify(player, message);
        }

        private async Task LinkFrameworkCharacterAsync(Player player, string license, FrameworkCharacter character)
        {
            if (_framework is null || _linker is null) return;

            try
            {
                var characterId = await _linker.LinkAsync(new ExternalIdentity
                {
                    ExternalId   = _framework.ExternalId(character.Id),
                    OwnerLicense = license,
                    FirstName    = character.FirstName,
                    LastName     = character.LastName,
                    DateOfBirth  = character.DateOfBirth,
                    Gender       = character.Gender,
                });
                await _db.Civilians.TouchCharacterAsync(characterId);

                var vehicles = await _framework.VehiclesAsync(character.Id);
                await _linker.SyncVehiclesAsync(characterId, vehicles.Select(v => new ExternalVehicle { Plate = v.Plate, Model = v.Model }));
                await CollectFinesAsync(characterId);

                if (_civilians is not null)
                {
                    var state = await _civilians.GetStateAsync(license);
                    TriggerClientEvent(player, EventNames.ClientCivilianState, JsonConvert.SerializeObject(state));
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not link {player.Name}'s character", ex);
            }
        }

        private async Task CollectFinesAsync(int characterId)
        {
            if (_framework is null || !ConfigManager.Framework.Fines.Collect) return;

            try
            {
                var character = await _db.Civilians.GetCharacterAsync(characterId);
                if (character?.ExternalId is null) return;

                var source = _frameworkCharacters.FirstOrDefault(entry => _framework.ExternalId(entry.Value.Id) == character.ExternalId).Key;
                if (source <= 0 || !TryGetPlayer(source, out var player)) return;

                var paid = 0;
                var unpaid = 0;
                foreach (var record in await _db.Civilians.GetUnpaidFinesAsync(characterId))
                {
                    if (unpaid == 0 && await _framework.RemoveMoneyAsync(source, ConfigManager.Framework.Fines.Account, record.Fine, $"Citation: {record.Description}"))
                    {
                        await _db.Civilians.SetFineStatusAsync(record.Id, FineStatus.Paid);
                        paid += record.Fine;
                    }
                    else
                    {
                        unpaid += record.Fine;
                    }
                }

                if (paid > 0) Notify(player, $"~y~Fines~w~ | ${paid:N0} was taken from your {ConfigManager.Framework.Fines.Account} for citations.");
                if (unpaid > 0) Notify(player, $"~o~Fines~w~ | You couldn't pay ${unpaid:N0} in fines. It will be collected later.");
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not collect fines for character {characterId}", ex);
            }
        }

        private async Task<bool> LinkFrameworkPlateAsync(string plate)
        {
            if (_framework is null || _linker is null) return false;

            try
            {
                var vehicle = await _framework.VehicleAsync(plate);
                if (vehicle?.Owner is null || string.IsNullOrEmpty(vehicle.Owner.Id)) return false;

                var characterId = await _linker.LinkAsync(new ExternalIdentity
                {
                    ExternalId   = _framework.ExternalId(vehicle.Owner.Id),
                    OwnerLicense = vehicle.Owner.License,
                    FirstName    = vehicle.Owner.FirstName,
                    LastName     = vehicle.Owner.LastName,
                    DateOfBirth  = vehicle.Owner.DateOfBirth,
                    Gender       = vehicle.Owner.Gender,
                });
                return await _linker.LinkVehicleAsync(characterId, new ExternalVehicle { Plate = vehicle.Plate, Model = vehicle.Model });
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not look up plate {plate} in {FrameworkBridge.DisplayName(_framework.Info.Name)}", ex);
                return false;
            }
        }

        private bool CanJoin(Player player, Department department)
        {
            if (_framework is null) return _permissions.CanJoinDepartment(player.Handle, department);
            if (department == Department.None) return false;
            if (_permissions.IsAdmin(player.Handle)) return true;
            return _frameworkJobs.TryGetValue(ServerId(player), out var match) && match.Department == department;
        }

        private string? FrameworkAgency(Player player, Department department) =>
            _frameworkJobs.TryGetValue(ServerId(player), out var match) && match.Department == department ? match.Agency : null;

        private void SyncFrameworkDuty(Player player, bool onDuty)
        {
            if (_framework is null) return;
            if (_frameworkCharacters.TryGetValue(ServerId(player), out var character))
            {
                if (!character.TracksDuty || character.OnDuty == onDuty) return;
                character.OnDuty = onDuty;
            }

            _ = PushFrameworkDutyAsync(_framework, ServerId(player), onDuty);
        }

        private static async Task PushFrameworkDutyAsync(FrameworkBridge framework, int source, bool onDuty)
        {
            try
            {
                await framework.SetDutyAsync(source, onDuty);
            }
            catch (Exception ex)
            {
                Log.Warn($"Could not update duty in {FrameworkBridge.DisplayName(framework.Info.Name)}", ex);
            }
        }

        private void ForgetFrameworkCharacter(Player player)
        {
            _frameworkCharacters.TryRemove(ServerId(player), out _);
            _frameworkJobs.TryRemove(ServerId(player), out _);
        }
    }
}
