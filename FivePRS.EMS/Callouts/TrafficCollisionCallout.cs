using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Callouts;
using FivePRS.Core.Models;

namespace FivePRS.EMS.Callouts
{
    [CalloutInfo(
        name:            "Traffic Collision",
        department:      Department.EMS,
        priority:        CalloutPriority.High,
        weight:          25,
        cooldownSeconds: 240,
        xpReward:        100)]
    public sealed class TrafficCollisionCallout : PatientCallout
    {
        private static readonly string[] VehicleModels = { "asea", "emperor", "fugitive", "ingot", "premier", "stanier", "tailgater" };

        private readonly Vector3 _location;

        public TrafficCollisionCallout()
        {
            _location = World.GetNextPositionOnStreet(Positions.Around(Game.PlayerPed.Position, 250f + Rng.Next(200), Rng));
            Data.Description = "Two-vehicle collision with one person injured. Treat and transport the patient.";
        }

        protected override Vector3 Location => _location;

        protected override string BlipName => "Traffic collision";

        protected override async Task<Ped?> SetupSceneAsync(CancellationToken ct)
        {
            var heading = (float)Rng.Next(360);
            var first   = await SpawnWreckAsync(_location, heading);
            var second  = await SpawnWreckAsync(_location + new Vector3(3.5f, 2.5f, 0f), heading + 70f);
            if (first is null || second is null) return null;

            return await SpawnPatientAsync(first.Position + first.RightVector * 2.5f, heading + 90f);
        }

        private async Task<Vehicle?> SpawnWreckAsync(Vector3 position, float heading)
        {
            var model = new Model(VehicleModels[Rng.Next(VehicleModels.Length)]);
            if (!await model.Request(7_000)) return null;

            var vehicle = TrackEntity(await World.CreateVehicle(model, position, heading));
            model.MarkAsNoLongerNeeded();
            if (vehicle is null || !vehicle.Exists()) return null;

            API.SetVehicleEngineHealth(vehicle.Handle, 150f);
            API.SetVehicleDamage(vehicle.Handle, 0f, 1.5f, 0.3f, 800f, 300f, true);
            API.SmashVehicleWindow(vehicle.Handle, 0);
            API.SmashVehicleWindow(vehicle.Handle, 1);
            API.SetVehicleDoorsLocked(vehicle.Handle, 2);
            vehicle.IsEngineRunning = false;
            return vehicle;
        }

        public override void OnCalloutDeclined() => ClientBrain.ShowNotification("Traffic collision declined.", "~r~Dispatch");

        public override void OnCalloutFailed() => ClientBrain.ShowNotification("Traffic collision call ended.", "~r~Dispatch");
    }
}
