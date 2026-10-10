using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Civilian;
using FivePRS.Core.Events;
using Newtonsoft.Json;

namespace FivePRS.Client.App
{
    internal sealed class DrivenVehicle
    {
        public string Plate { get; }
        public string Model { get; }

        public DrivenVehicle(string plate, string model)
        {
            Plate = plate;
            Model = model;
        }
    }

    internal static class CivilianPanel
    {
        private static CivilianState? _state;

        public static void SetState(string json) => _state = JsonConvert.DeserializeObject<CivilianState>(json);

        public static int? CharacterCount => _state?.Characters.Count;

        public static CharacterInfo? ActiveCharacter =>
            _state?.Characters.FirstOrDefault(c => c.Id == _state.ActiveCharacterId);

        public static void RequestState() => ClientEvents.TriggerServer(EventNames.ServerCivilianRequest);

        public static void CreateCharacter(IDictionary<string, object> data) =>
            ClientEvents.TriggerServer(EventNames.ServerCharacterCreate,
                NuiData.GetString(data, "firstName"), NuiData.GetString(data, "lastName"),
                NuiData.GetString(data, "dateOfBirth"), NuiData.GetString(data, "gender"));

        public static void SelectCharacter(IDictionary<string, object> data)
        {
            if (NuiData.TryGetInt(data, "characterId", out var id))
                ClientEvents.TriggerServer(EventNames.ServerCharacterSelect, id);
        }

        public static void DeleteCharacter(IDictionary<string, object> data)
        {
            if (NuiData.TryGetInt(data, "characterId", out var id))
                ClientEvents.TriggerServer(EventNames.ServerCharacterDelete, id);
        }

        public static void ApplyLicense(IDictionary<string, object> data) =>
            ClientEvents.TriggerServer(EventNames.ServerLicenseApply, NuiData.GetString(data, "type"));

        public static string? RegisterVehicle()
        {
            var vehicle = CurrentVehicle();
            if (vehicle is null) return "Get in the driver's seat of the vehicle you want to register.";

            ClientEvents.TriggerServer(EventNames.ServerVehicleRegister, vehicle.Model);
            return null;
        }

        public static void RemoveVehicle(IDictionary<string, object> data)
        {
            if (NuiData.TryGetInt(data, "vehicleId", out var id))
                ClientEvents.TriggerServer(EventNames.ServerVehicleRemove, id);
        }

        public static void SetVehicleStolen(IDictionary<string, object> data)
        {
            if (!NuiData.TryGetInt(data, "vehicleId", out var id)) return;
            var stolen = NuiData.GetBool(data, "stolen");
            ClientEvents.TriggerServer(EventNames.ServerVehicleSetStolen, id, stolen);
        }

        public static object BuildView()
        {
            var vehicle = CurrentVehicle();
            return new
            {
                State          = _state,
                OnDuty         = ClientBrain.LocalPlayerData.IsOnDuty,
                Genders        = CivilianRules.Genders,
                MaxNameLength  = CivilianRules.MaxNameLength,
                CurrentVehicle = vehicle,
            };
        }

        private static DrivenVehicle? CurrentVehicle()
        {
            var ped = Game.PlayerPed;
            var vehicle = API.GetVehiclePedIsIn(ped.Handle, false);
            if (vehicle == 0 || API.GetPedInVehicleSeat(vehicle, -1) != ped.Handle) return null;

            var displayName = API.GetDisplayNameFromVehicleModel((uint)API.GetEntityModel(vehicle));
            var label = API.GetLabelText(displayName);
            var model = string.IsNullOrEmpty(label) || label == "NULL" ? displayName : label;

            return new DrivenVehicle(API.GetVehicleNumberPlateText(vehicle).Trim(), model);
        }
    }
}
