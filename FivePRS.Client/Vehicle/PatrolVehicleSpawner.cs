using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Client.VehicleSpawner
{
    /// <summary>Describes a vehicle model pool + visual configuration for a department.</summary>
    public sealed class PatrolVehicleConfig
    {
        public IReadOnlyList<string> ModelPool { get; init; } = new[] { "police" };

        public int PrimaryColor   { get; init; } = 0;

        public int SecondaryColor { get; init; } = 0;

        public (int R, int G, int B) NeonColor  { get; init; } = (-1, -1, -1);

        public int DirtLevel  { get; init; } = 0;

        public int Livery     { get; init; } = -1;

        public IReadOnlyList<int> ForcedExtras  { get; init; } = Array.Empty<int>();

        public IReadOnlyList<int> DisabledExtras { get; init; } = Array.Empty<int>();

        public string PlateText { get; init; } = string.Empty;
    }
    /// Spawns, configures, and tracks a single patrol vehicle for an officer's duty session.
    /// One instance per agency; call <see cref="SpawnAsync"/> on duty and <see cref="Despawn"/> off duty.
    ///
    /// Spawn strategy (in order of preference):
    ///   1. Nearest police station spawn point within 600 m of the player.
    ///   2. Nearest road node to the player's current position (fallback).
    /// </summary>
    public sealed class PatrolVehicleSpawner
    {
        private static readonly (Vector3 Pos, float Heading)[] StationSpawns =
        {
            (new Vector3( 457.1f,  -1016.8f,  28.0f),  90f),
            (new Vector3( 441.8f,  -986.0f,   30.7f),   0f),
            (new Vector3(-1108.0f, -845.0f,   19.3f), 120f),
            (new Vector3( 372.5f, -1608.9f,   29.3f), 260f),
            (new Vector3(1853.5f,  3686.8f,   34.3f),  30f),
            (new Vector3(-448.8f,  6012.2f,   31.5f), 240f),
        };

        private const float StationSearchRadius = 600f;

        private CitizenFX.Core.Vehicle? _vehicle;
        private Blip?                   _vehicleBlip;

        public bool HasVehicle => _vehicle is not null && _vehicle.Exists();

        public async Task<CitizenFX.Core.Vehicle?> SpawnAsync(PatrolVehicleConfig config)
        {
            var modelName = config.ModelPool[new Random().Next(config.ModelPool.Count)];
            var model     = new Model(modelName);

            if (!await model.Request(10_000))
            {
                Debug.WriteLine($"[PatrolVehicleSpawner] Model '{modelName}' failed to load.");
                model.MarkAsNoLongerNeeded();
                return null;
            }

            var (spawnPos, heading) = FindSpawnPoint();

            _vehicle = await CitizenFX.Core.World.CreateVehicle(model, spawnPos, heading);
            model.MarkAsNoLongerNeeded();

            if (_vehicle is null || !_vehicle.Exists())
            {
                Debug.WriteLine("[PatrolVehicleSpawner] Vehicle creation failed.");
                return null;
            }

            ConfigureVehicle(_vehicle, config);
            WarpPlayerIn(_vehicle);
            AddVehicleBlip(_vehicle, modelName);

            Debug.WriteLine($"[PatrolVehicleSpawner] Spawned '{modelName}' at {spawnPos}.");
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

        private static (Vector3 Pos, float Heading) FindSpawnPoint()
        {
            var playerPos = Game.PlayerPed.Position;

            var nearestStation = (Pos: Vector3.Zero, Heading: 0f, Dist: float.MaxValue);

            foreach (var (pos, heading) in StationSpawns)
            {
                var dist = Vector3.Distance(playerPos, pos);
                if (dist < StationSearchRadius && dist < nearestStation.Dist)
                    nearestStation = (pos, heading, dist);
            }

            if (nearestStation.Pos != Vector3.Zero)
                return (nearestStation.Pos, nearestStation.Heading);

            var streetPos    = CitizenFX.Core.World.GetNextPositionOnStreet(playerPos);
            var fallbackHdg  = API.GetEntityHeading(Game.PlayerPed.Handle);
            return (streetPos, fallbackHdg);
        }

        private static void ConfigureVehicle(CitizenFX.Core.Vehicle vehicle, PatrolVehicleConfig config)
        {
            var h = vehicle.Handle;

            API.SetVehicleColours(h, config.PrimaryColor, config.SecondaryColor);

            API.SetVehicleDirtLevel(h, config.DirtLevel);

            if (config.Livery >= 0)
                API.SetVehicleLivery(h, config.Livery);

            if (!string.IsNullOrWhiteSpace(config.PlateText))
                API.SetVehicleNumberPlateText(h, config.PlateText);

            foreach (var id in config.ForcedExtras)
                API.SetVehicleExtra(h, id, false);
            foreach (var id in config.DisabledExtras)
                API.SetVehicleExtra(h, id, true);

            if (config.NeonColor.R >= 0)
            {
                API.SetVehicleNeonLightEnabled(h, 0, true);
                API.SetVehicleNeonLightEnabled(h, 1, true);
                API.SetVehicleNeonLightEnabled(h, 2, true);
                API.SetVehicleNeonLightEnabled(h, 3, true);
                API.SetVehicleNeonLightsColour(h, config.NeonColor.R, config.NeonColor.G, config.NeonColor.B);
            }

            vehicle.Repair();
            vehicle.FuelLevel = 100f;
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
