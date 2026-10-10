using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;
using FivePRS.Server.Civilian;
using FivePRS.Server.Database;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public partial class ServerBrain
    {
        private CivilianService? _civilians;

        private void RegisterCivilianEvents()
        {
            EventHandlers[EventNames.ServerCivilianRequest]  += new Action<Player>(OnCivilianRequest);
            EventHandlers[EventNames.ServerCharacterCreate]  += new Action<Player, string, string, string, string>(OnCharacterCreate);
            EventHandlers[EventNames.ServerCharacterSelect]  += new Action<Player, int>(OnCharacterSelect);
            EventHandlers[EventNames.ServerCharacterDelete]  += new Action<Player, int>(OnCharacterDelete);
            EventHandlers[EventNames.ServerLicenseApply]     += new Action<Player, string>(OnLicenseApply);
            EventHandlers[EventNames.ServerVehicleRegister]  += new Action<Player, string>(OnVehicleRegister);
            EventHandlers[EventNames.ServerVehicleRemove]    += new Action<Player, int>(OnVehicleRemove);
            EventHandlers[EventNames.ServerVehicleSetStolen] += new Action<Player, int, bool>(OnVehicleSetStolen);
        }

        private void OnCivilianRequest([FromSource] Player player) =>
            RunCivilian(player, (_, __) => Task.FromResult<string?>(null));

        private void OnCharacterCreate([FromSource] Player player, string firstName, string lastName, string dateOfBirth, string gender) =>
            RunCivilian(player, (service, license) => service.CreateCharacterAsync(license, firstName, lastName, dateOfBirth, gender),
                AuditActions.CharacterCreated, $"{firstName} {lastName}");

        private void OnCharacterSelect([FromSource] Player player, int characterId) =>
            RunCivilian(player, (service, license) => service.SelectCharacterAsync(license, characterId));

        private void OnCharacterDelete([FromSource] Player player, int characterId) =>
            RunCivilian(player, async (service, license) =>
                {
                    var error = await service.DeleteCharacterAsync(license, characterId);
                    if (error is null && _appearances is not null) await _appearances.DeleteAsync(characterId);
                    return error;
                },
                AuditActions.CharacterDeleted, $"character {characterId}");

        private void OnLicenseApply([FromSource] Player player, string type) =>
            RunCivilian(player, (service, license) => service.ApplyForLicenseAsync(license, type),
                AuditActions.LicenseIssued, type);

        private void OnVehicleRegister([FromSource] Player player, string model)
        {
            var ped = API.GetPlayerPed(player.Handle);
            var vehicle = ped == 0 ? 0 : API.GetVehiclePedIsIn(ped, false);

            if (vehicle == 0 || API.GetPedInVehicleSeat(vehicle, -1) != ped)
            {
                TriggerClientEvent(player, EventNames.ClientCivilianError, "Get in the driver's seat of the vehicle you want to register.");
                return;
            }

            var plate = API.GetVehicleNumberPlateText(vehicle);
            RunCivilian(player, (service, license) => service.RegisterVehicleAsync(license, plate, model),
                AuditActions.VehicleRegistered, plate?.Trim() ?? string.Empty);
        }

        private void OnVehicleRemove([FromSource] Player player, int vehicleId) =>
            RunCivilian(player, (service, license) => service.RemoveVehicleAsync(license, vehicleId),
                AuditActions.VehicleRemoved, $"vehicle {vehicleId}");

        private void OnVehicleSetStolen([FromSource] Player player, int vehicleId, bool stolen) =>
            RunCivilian(player, (service, license) => service.SetVehicleStolenAsync(license, vehicleId, stolen));

        private async void RunCivilian(Player player, Func<CivilianService, string, Task<string?>> action,
            string? auditAction = null, string? auditDetails = null)
        {
            var license = GetLicense(player);
            if (license is null) return;

            if (!Allow(player, "civilian", l => l.Civilian))
            {
                TriggerClientEvent(player, EventNames.ClientCivilianError, "Slow down. Try again in a few seconds.");
                return;
            }

            if (_civilians is null)
            {
                TriggerClientEvent(player, EventNames.ClientCivilianError, "Civilian records are not available yet.");
                return;
            }

            try
            {
                var error = await action(_civilians, license);
                if (error is not null)
                    TriggerClientEvent(player, EventNames.ClientCivilianError, error);
                else if (auditAction is not null)
                    Audit(auditAction, player, license, auditDetails ?? string.Empty);

                var state = await _civilians.GetStateAsync(license);
                TriggerClientEvent(player, EventNames.ClientCivilianState, JsonConvert.SerializeObject(state));
                await SendAppearanceAsync(player, license);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS] Civilian action failed for {player.Name}: {ex}");
                TriggerClientEvent(player, EventNames.ClientCivilianError, "Something went wrong. Please try again.");
            }
        }
    }
}
