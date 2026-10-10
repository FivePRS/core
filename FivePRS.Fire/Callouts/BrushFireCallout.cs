using System;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Client;
using FivePRS.Client.Callouts;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.Fire.Callouts
{
    [CalloutInfo(
        name:            "Brush Fire",
        department:      Department.Fire,
        priority:        CalloutPriority.High,
        weight:          20,
        cooldownSeconds: 300,
        xpReward:        120)]
    public sealed class BrushFireCallout : CalloutBase
    {
        private const string Countryside = "blaine_county";
        private const float IgniteDistance = 150f;
        private const float SceneRadius = 45f;
        private const int FireCount = 6;
        private const int TimeoutMinutes = 25;

        private readonly Random _rng = new();
        private readonly Vector3 _location;

        private FireScene? _scene;

        public BrushFireCallout()
        {
            _location = Positions.Around(Game.PlayerPed.Position, 300f + _rng.Next(250), _rng);
            Data.Description = "Smoke reported from dry brush. The fire is spreading. Contain it before it reaches property.";
        }

        public override Vector3 GetDispatchLocation() => _location;

        public override bool CanBeDispatched() =>
            ConfigManager.Territories.Resolve(Game.PlayerPed.Position.X, Game.PlayerPed.Position.Y)?.Id == Countryside;

        public override async Task OnCalloutAccepted(CancellationToken ct)
        {
            var blip       = TrackBlip(World.CreateBlip(_location));
            blip.Sprite    = BlipSprite.Standard;
            blip.Color     = BlipColor.Red;
            blip.Name      = "Brush fire";
            blip.ShowRoute = true;

            while (Vector3.Distance(Game.PlayerPed.Position, _location) > IgniteDistance)
                await Timing.WaitAsync(1_000, ct);

            if (!FireScene.TryGround(_location.X, _location.Y, Game.PlayerPed.Position.Z, out var center))
            {
                ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | False alarm. The smoke was from a controlled burn.");
                CalloutCompleted();
                return;
            }

            _scene = new FireScene(center, SceneRadius);
            for (var i = 0; i < FireCount; i++)
            {
                var spot = Positions.Around(center, 2f + (float)_rng.NextDouble() * 12f, _rng);
                if (FireScene.TryGround(spot.X, spot.Y, center.Z, out var ground))
                    _scene.Ignite(ground);
            }

            if (!_scene.Ignited)
            {
                CalloutCompleted();
                return;
            }

            blip.Position = center;
            ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | The brush is ~r~burning and spreading~w~. Knock down every fire.");

            var deadline = DateTime.UtcNow.AddMinutes(TimeoutMinutes);
            while (!ct.IsCancellationRequested)
            {
                await Timing.WaitAsync(1_000, ct);

                if (_scene.IsOut())
                {
                    ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | ~g~Fire contained.~w~ Good work.");
                    CalloutCompleted();
                    return;
                }

                if (DateTime.UtcNow > deadline)
                {
                    ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | ~r~The fire spread out of control.");
                    CalloutFailed();
                    return;
                }

                if (Vector3.Distance(Game.PlayerPed.Position, _scene.Center) < 90f)
                    ClientBrain.ShowHelp($"~r~Fires~w~: {_scene.ActiveFires}", 1_100);
            }
        }

        protected override void OnCleanup() => _scene?.Extinguish();

        public override void OnCalloutDeclined() => ClientBrain.ShowNotification("Brush fire declined.", "~r~Dispatch");

        public override void OnCalloutFailed() => ClientBrain.ShowNotification("Brush fire call ended.", "~r~Dispatch");
    }
}
