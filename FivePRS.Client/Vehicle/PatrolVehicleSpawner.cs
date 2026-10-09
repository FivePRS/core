using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Client.VehicleSpawner
{
    public sealed class PatrolVehicleConfig
    {
        public IReadOnlyList<string> ModelPool { get; set; } = new[] { "police" };

        public int PrimaryColor   { get; set; } = -1;

        public int SecondaryColor { get; set; } = -1;

        public int[]? NeonColor { get; set; }

        public int DirtLevel  { get; set; } = -1;

        public int Livery     { get; set; } = -1;

        public IReadOnlyList<int> ForcedExtras  { get; set; } = new int[0];

        public IReadOnlyList<int> DisabledExtras { get; set; } = new int[0];

        public string PlateText { get; set; } = string.Empty;
    }

    public sealed class SpawnPoint
    {
        public Vector3 Position { get; }
        public float Heading { get; }

        public SpawnPoint(Vector3 position, float heading)
        {
            Position = position;
            Heading = heading;
        }
    }

    public sealed class PatrolVehicleSpawner
    {
        private static readonly Random Rng = new();

        private CitizenFX.Core.Vehicle? _vehicle;
        private Blip?                   _vehicleBlip;

        public bool HasVehicle => _vehicle is not null && _vehicle.Exists();

        public CitizenFX.Core.Vehicle? Current => HasVehicle ? _vehicle : null;

        public async Task<CitizenFX.Core.Vehicle?> SpawnAsync(PatrolVehicleConfig config, SpawnPoint spawn, bool warpIn)
        {
            Despawn();

            if (config.ModelPool.Count == 0)
            {
                Debug.WriteLine("[PatrolVehicleSpawner] No vehicle models configured.");
                return null;
            }

            var modelName = config.ModelPool[Rng.Next(config.ModelPool.Count)];
            var model     = new Model(modelName);

            if (!await model.Request(10_000))
            {
                Debug.WriteLine($"[PatrolVehicleSpawner] Model '{modelName}' failed to load.");
                model.MarkAsNoLongerNeeded();
                return null;
            }

            _vehicle = await CitizenFX.Core.World.CreateVehicle(model, spawn.Position, spawn.Heading);
            model.MarkAsNoLongerNeeded();

            if (_vehicle is null || !_vehicle.Exists())
            {
                Debug.WriteLine("[PatrolVehicleSpawner] Vehicle creation failed.");
                return null;
            }

            ConfigureVehicle(_vehicle, config);
            if (warpIn) WarpPlayerIn(_vehicle);
            AddVehicleBlip(_vehicle, modelName);

            Debug.WriteLine($"[PatrolVehicleSpawner] Spawned '{modelName}' at {spawn.Position}.");
            return _vehicle;
        }

        public void Despawn()
        {
            _vehicleBlip?.Delete();
            _vehicleBlip = null;

            if (_vehicle is not null && _vehicle.Exists())
            {
                _vehicle.Delete();
                Debug.WriteLine("[PatrolVehicleSpawner] Patrol vehicle despawned.");
            }

            _vehicle = null;
        }

        public static SpawnPoint RoadSpawnPoint()
        {
            var streetPos = CitizenFX.Core.World.GetNextPositionOnStreet(Game.PlayerPed.Position);
            return new SpawnPoint(streetPos, API.GetEntityHeading(Game.PlayerPed.Handle));
        }

        private static void ConfigureVehicle(CitizenFX.Core.Vehicle vehicle, PatrolVehicleConfig config)
        {
            var h = vehicle.Handle;

            vehicle.Repair();
            vehicle.FuelLevel = 100f;

            if (config.PrimaryColor >= 0 && config.SecondaryColor >= 0)
                API.SetVehicleColours(h, config.PrimaryColor, config.SecondaryColor);

            if (config.DirtLevel >= 0)
                API.SetVehicleDirtLevel(h, config.DirtLevel);

            if (config.Livery >= 0)
                API.SetVehicleLivery(h, config.Livery);

            if (!string.IsNullOrWhiteSpace(config.PlateText))
                API.SetVehicleNumberPlateText(h, config.PlateText);

            foreach (var id in config.ForcedExtras)
                API.SetVehicleExtra(h, id, false);
            foreach (var id in config.DisabledExtras)
                API.SetVehicleExtra(h, id, true);

            if (config.NeonColor is { Length: 3 })
            {
                API.SetVehicleNeonLightEnabled(h, 0, true);
                API.SetVehicleNeonLightEnabled(h, 1, true);
                API.SetVehicleNeonLightEnabled(h, 2, true);
                API.SetVehicleNeonLightEnabled(h, 3, true);
                API.SetVehicleNeonLightsColour(h, config.NeonColor[0], config.NeonColor[1], config.NeonColor[2]);
            }
        }

        private static void WarpPlayerIn(CitizenFX.Core.Vehicle vehicle)
        {
            API.TaskWarpPedIntoVehicle(Game.PlayerPed.Handle, vehicle.Handle, -1);
        }

        private void AddVehicleBlip(CitizenFX.Core.Vehicle vehicle, string modelName)
        {
            _vehicleBlip          = vehicle.AttachBlip();
            _vehicleBlip.Sprite   = BlipSprite.PersonalVehicleCar;
            _vehicleBlip.Color    = BlipColor.Blue;
            _vehicleBlip.Name     = $"Patrol Vehicle ({modelName})";
            _vehicleBlip.Scale    = 0.75f;
        }
    }
}
