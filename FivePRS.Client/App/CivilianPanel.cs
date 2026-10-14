using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client.Terminal;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
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

    internal sealed class PendingMugshot
    {
        public int    CharacterId { get; set; }
        public string Data        { get; set; } = string.Empty;
    }

    internal static class CivilianPanel
    {
        private const float ShowCardRangeM = 3f;
        private const int MaxPlayerSlots = 256;

        private static readonly string[] AppIds = { "characters", "licenses", "vehicles" };

        private static CivilianState? _state;
        private static PendingMugshot? _mugshot;

        public static event Action? AppearanceRequested;

        public static event Action<int>? MugshotRequested;

        public static void RequestAppearance() => AppearanceRequested?.Invoke();

        public static void RequestMugshot(IDictionary<string, object> data)
        {
            if (NuiData.TryGetInt(data, "characterId", out var id)) MugshotRequested?.Invoke(id);
        }

        public static void SetMugshot(int characterId, string data) =>
            _mugshot = new PendingMugshot { CharacterId = characterId, Data = data };

        public static void DiscardMugshot() => _mugshot = null;

        public static void SetPhotoUrl(IDictionary<string, object> data)
        {
            if (NuiData.TryGetInt(data, "characterId", out var id))
                ClientEvents.TriggerServer(EventNames.ServerCharacterPhoto, id, NuiData.GetString(data, "url").Trim());
        }

        public static void RemovePhoto(IDictionary<string, object> data)
        {
            if (NuiData.TryGetInt(data, "characterId", out var id))
                ClientEvents.TriggerServer(EventNames.ServerCharacterPhoto, id, string.Empty);
        }

        public static void ShowLicense(IDictionary<string, object> data)
        {
            var me       = Game.PlayerPed.Position;
            var self     = API.PlayerId();
            var nearest  = -1;
            var distance = ShowCardRangeM;

            for (var player = 0; player < MaxPlayerSlots; player++)
            {
                if (player == self || !API.NetworkIsPlayerActive(player)) continue;

                var ped = API.GetPlayerPed(player);
                if (ped == 0) continue;

                var gap = Vector3.Distance(me, API.GetEntityCoords(ped, false));
                if (gap > distance) continue;

                nearest  = player;
                distance = gap;
            }

            if (nearest < 0)
            {
                ShowError("Nobody is close enough. Stand next to the person you want to show it to.");
                return;
            }

            ClientEvents.TriggerServer(EventNames.ServerShowLicense, API.GetPlayerServerId(nearest), NuiData.GetString(data, "type"));
        }

        public static void ShowError(string message)
        {
            foreach (var id in AppIds) TerminalApps.SendToApp(id, "error", message);
        }

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

        public static void RegisterVehicle()
        {
            var vehicle = CurrentVehicle();
            if (vehicle is null)
            {
                ShowError("Get in the driver's seat of the vehicle you want to register.");
                return;
            }

            ClientEvents.TriggerServer(EventNames.ServerVehicleRegister, vehicle.Model);
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
                Icons          = ConfigManager.Settings.Branding.Departments,
                StateInfo      = ConfigManager.Licenses.State,
                Mugshot        = _mugshot,
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
