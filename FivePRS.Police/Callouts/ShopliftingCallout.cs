using System;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Client;
using FivePRS.Client.Callouts;
using FivePRS.Core.Models;

namespace FivePRS.Police.Callouts
{
    [CalloutInfo(
        name:            "Shoplifting",
        department:      Department.Police,
        priority:        CalloutPriority.Low,
        weight:          20,
        cooldownSeconds: 300,
        xpReward:        75)]
    public sealed class ShopliftingCallout : CalloutBase
    {
        private static readonly Vector3[] StoreLocations =
        {
            new( 25.7f,  -1344.4f, 29.5f),
            new(-47.1f,  -1757.5f, 29.4f),
            new(1163.2f,  -323.4f, 69.2f),
            new(-706.3f,  -913.3f, 19.2f),
            new( 545.0f,  2656.9f, 42.0f),
            new(-3040.3f, 584.2f,  7.9f),
        };

        private static readonly string[] SuspectModels =
        {
            "a_m_y_hipster_01",
            "a_f_y_hipster_01",
            "a_m_m_business_01",
            "a_f_m_business_02",
            "a_m_y_skater_01",
            "a_f_y_scdressy_01",
        };

        private readonly Random  _rng = new();
        private readonly Vector3 _storePosition;
        private readonly string  _suspectModelName;

        private Ped? _suspect;

        public ShopliftingCallout()
        {
            _storePosition    = StoreLocations[_rng.Next(StoreLocations.Length)];
            _suspectModelName = SuspectModels[_rng.Next(SuspectModels.Length)];

            Data.Description = "Report of a shoplifter fleeing on foot from a convenience store. Suspect is considered non-violent.";
        }

        public override Vector3 GetDispatchLocation() => _storePosition;

        public override bool CanBeDispatched()
        {
            var hour = World.CurrentDayTime.Hours;
            return hour is >= 6 and <= 23;
        }

        public override async Task OnCalloutAccepted(CancellationToken ct)
        {
            var blip           = TrackBlip(World.CreateBlip(_storePosition));
            blip.Sprite        = BlipSprite.Store;
            blip.Color         = BlipColor.Red;
            blip.Name          = "Shoplifting — Suspect Last Seen";
            blip.IsShortRange  = false;
            blip.ShowRoute     = true;

            var model = new Model(_suspectModelName);
            if (!await model.Request(7_000))
            {
                Debug.WriteLine($"[ShopliftingCallout] Model '{_suspectModelName}' failed to load.");
                CalloutFailed();
                return;
            }

            var offset  = new Vector3(_rng.Next(-5, 5) + 0.5f, _rng.Next(-5, 5) + 0.5f, 0f);
            _suspect    = TrackEntity(await World.CreatePed(model, _storePosition + offset));
            model.MarkAsNoLongerNeeded();

            if (_suspect is null || !_suspect.Exists())
            {
                Debug.WriteLine("[ShopliftingCallout] Ped creation failed.");
                CalloutFailed();
                return;
            }

            ClientBrain.ShowNotification($"~b~{Data.Name}~w~ | Suspect spotted, ~r~pursue on foot~w~.");

            await FootPursuitAsync(_suspect, catchDistM: 3.0f, escapeDistM: 300.0f, ct);
        }

        public override void OnCalloutDeclined()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Shoplifting callout declined.");

        public override void OnCalloutFailed()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Shoplifting callout cancelled.");
    }
}
