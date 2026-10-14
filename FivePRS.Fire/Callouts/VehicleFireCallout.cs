using System;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Callouts;
using FivePRS.Core.Models;

namespace FivePRS.Fire.Callouts
{
    [CalloutInfo(
        name:            "Vehicle Fire",
        department:      Department.Fire,
        priority:        CalloutPriority.Medium,
        weight:          30,
        cooldownSeconds: 180,
        xpReward:        80)]
    public sealed class VehicleFireCallout : CalloutBase
    {
        private const float IgniteDistance = 150f;
        private const float SceneRadius = 20f;
        private const int TimeoutMinutes = 20;

        private static readonly string[] Models =
        {
            "asea", "emperor", "fugitive", "ingot", "primo", "stanier", "tailgater", "regina", "manana", "picador",
        };

        private readonly Random _rng = new();
        private readonly Vector3 _location;
        private readonly string _model;

        private FireScene? _scene;
        private Vehicle? _vehicle;
        private int _peak;

        public VehicleFireCallout()
        {
            _model    = Models[_rng.Next(Models.Length)];
            _location = World.GetNextPositionOnStreet(Positions.Around(Game.PlayerPed.Position, 250f + _rng.Next(200), _rng));
            Data.Description = "A vehicle is on fire at the roadside. No occupants reported. Put the fire out before it spreads.";
        }

        public override Vector3 GetDispatchLocation() => _location;

        public override async Task OnCalloutAccepted(CancellationToken ct)
        {
            var blip       = TrackBlip(World.CreateBlip(_location));
            blip.Sprite    = BlipSprite.Standard;
            blip.Color     = BlipColor.Red;
            blip.Name      = "Vehicle fire";
            blip.ShowRoute = true;

            while (Vector3.Distance(Game.PlayerPed.Position, _location) > IgniteDistance)
                await Timing.WaitAsync(1_000, ct);

            var model = new Model(_model);
            if (!await model.Request(7_000))
            {
                CalloutFailed();
                return;
            }

            _vehicle = TrackEntity(await World.CreateVehicle(model, _location, _rng.Next(360)));
            model.MarkAsNoLongerNeeded();
            if (_vehicle is null || !_vehicle.Exists())
            {
                CalloutFailed();
                return;
            }

            _vehicle.IsEngineRunning = false;
            API.SetVehicleEngineHealth(_vehicle.Handle, 50f);
            API.SetVehicleDoorsLocked(_vehicle.Handle, 2);

            _scene = new FireScene(_vehicle.Position, SceneRadius);
            API.StartEntityFire(_vehicle.Handle);
            _scene.Ignite(_vehicle.Position + _vehicle.ForwardVector * 2f);
            _scene.Ignite(_vehicle.Position - _vehicle.ForwardVector * 2.5f);

            ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | The vehicle is ~r~burning~w~. Put it out with your extinguisher or the engine's water cannon.");

            var deadline = DateTime.UtcNow.AddMinutes(TimeoutMinutes);
            while (!ct.IsCancellationRequested)
            {
                await Timing.WaitAsync(1_000, ct);
                _peak = Math.Max(_peak, _scene.ActiveFires);

                if (_scene.IsOut(_vehicle))
                {
                    ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | ~g~Fire out.~w~ Scene secured.");
                    var plate = _vehicle.Exists() ? StreetIdentity.PlateOf(_vehicle) : null;
                    var name  = _vehicle.Exists() ? StreetIdentity.VehicleName(_vehicle) : "vehicle";
                    FireReport.Draft(Data, _scene.Center,
                        $"Dispatched: {Data.Description}" + NL + NL +
                        $"Found a {name} at the roadside fully involved, with {FireReport.Fires(Math.Max(_peak, 1))} burning. No occupants." + NL + NL +
                        "Fire knocked down and extinguished. Vehicle left secured at the scene.",
                        plate);
                    CalloutCompleted();
                    return;
                }

                if (DateTime.UtcNow > deadline)
                {
                    ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | ~r~The fire burned out of control.");
                    CalloutFailed();
                    return;
                }

                if (Vector3.Distance(Game.PlayerPed.Position, _scene.Center) < 60f)
                    ClientBrain.ShowHelp($"~r~Fires~w~: {_scene.ActiveFires}", 1_100);
            }
        }

        private static readonly string NL = Environment.NewLine;

        protected override void OnCleanup() => _scene?.Extinguish();

        public override void OnCalloutDeclined() => ClientBrain.ShowNotification("Vehicle fire declined.", "~r~Dispatch");

        public override void OnCalloutFailed() => ClientBrain.ShowNotification("Vehicle fire call ended.", "~r~Dispatch");
    }
}
