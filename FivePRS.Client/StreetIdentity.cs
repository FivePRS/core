using System;
using System.Collections.Generic;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using Newtonsoft.Json;

namespace FivePRS.Client
{
    public sealed class StreetStop
    {
        public string Kind { get; set; } = "person";
        public string Plate { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string? Name { get; set; }
        public int? CharacterId { get; set; }
        public int? OwnerId { get; set; }
        public bool PlateChecked { get; set; }
        public bool Checking { get; set; }

        [JsonIgnore]
        public Action? RunPlate { get; set; }
    }

    public static class StreetIdentity
    {
        private const int MaxSeeds = 512;

        private static readonly Random Rng = new();
        private static readonly Dictionary<int, int> Seeds = new();

        public static Func<StreetStop?>? CurrentStop { get; set; }

        public static int SeedFor(Ped ped)
        {
            if (Seeds.TryGetValue(ped.Handle, out var seed)) return seed;

            if (Seeds.Count >= MaxSeeds) Prune();
            Seeds[ped.Handle] = seed = Rng.Next();
            return seed;
        }

        public static bool IsFemale(Ped ped) => !API.IsPedMale(ped.Handle);

        public static string VehicleName(Vehicle vehicle)
        {
            var display = API.GetDisplayNameFromVehicleModel((uint)vehicle.Model.Hash);
            var label   = API.GetLabelText(display);
            return string.IsNullOrEmpty(label) || label == "NULL" ? display : label;
        }

        public static string PlateOf(Vehicle vehicle) => API.GetVehicleNumberPlateText(vehicle.Handle).Trim();

        public static Vehicle? VehicleAhead(float range, float cone, bool includeMission)
        {
            var player = Game.PlayerPed;
            var own    = player.CurrentVehicle;
            var origin = own is not null && own.Exists() ? own.Position : player.Position;
            var facing = own is not null && own.Exists() ? own.ForwardVector : player.ForwardVector;

            Vehicle? best = null;
            var bestDistance = range;

            foreach (var vehicle in World.GetAllVehicles())
            {
                if (!vehicle.Exists() || (own is not null && vehicle.Handle == own.Handle)) continue;
                if (!includeMission && API.IsEntityAMissionEntity(vehicle.Handle)) continue;

                var offset   = vehicle.Position - origin;
                var distance = offset.Length();
                if (distance >= bestDistance || distance < 1f) continue;

                offset.Normalize();
                if (Vector3.Dot(offset, facing) < cone) continue;

                best = vehicle;
                bestDistance = distance;
            }

            return best;
        }

        public static Ped? PersonAhead(float range, float cone, bool includeMission)
        {
            var player = Game.PlayerPed;

            Ped? best = null;
            var bestDistance = range;

            foreach (var ped in World.GetAllPeds())
            {
                if (ped.Handle == player.Handle || !ped.Exists() || ped.IsPlayer) continue;
                if (!API.IsPedHuman(ped.Handle) || (!includeMission && API.IsEntityAMissionEntity(ped.Handle))) continue;

                var offset   = ped.Position - player.Position;
                var distance = offset.Length();
                if (distance >= bestDistance) continue;

                offset.Normalize();
                if (Vector3.Dot(offset, player.ForwardVector) < cone) continue;

                best = ped;
                bestDistance = distance;
            }

            return best;
        }

        public static Vehicle? FindByPlate(string plate)
        {
            var wanted = plate.Trim().Replace(" ", string.Empty).ToUpperInvariant();
            if (wanted.Length == 0) return null;

            foreach (var vehicle in World.GetAllVehicles())
            {
                if (!vehicle.Exists()) continue;
                if (PlateOf(vehicle).Replace(" ", string.Empty).ToUpperInvariant() == wanted) return vehicle;
            }

            return null;
        }

        private static void Prune()
        {
            var stale = new List<int>();
            foreach (var handle in Seeds.Keys)
                if (!API.DoesEntityExist(handle)) stale.Add(handle);

            foreach (var handle in stale) Seeds.Remove(handle);
        }
    }
}
