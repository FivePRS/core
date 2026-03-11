using System;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Arrest;
using FivePRS.Client.Callouts;
using FivePRS.Client.Tasks;
using FivePRS.Core.Models;

namespace FivePRS.Police.Callouts
{
    [CalloutInfo(
        name:            "Domestic Disturbance",
        department:      Department.Police,
        priority:        CalloutPriority.Medium,
        weight:          15,
        cooldownSeconds: 240,
        xpReward:        90)]
    public sealed class DomesticDisturbanceCallout : CalloutBase
    {

        private enum AggressorProfile { Peaceful, Resistive, Armed }

        private const int ResistiveChancePct = 40;
        private const int ArmedChancePct     = 20;

        private static readonly Vector3[] SceneLocations =
        {
            new( 133.0f, -1940.0f, 20.8f),
            new(-273.7f,  -954.0f, 31.2f),
            new( 358.0f,  -124.0f, 68.3f),
            new(-534.5f, -1200.0f, 18.2f),
            new( 823.5f,  -795.0f, 26.2f),
            new( 246.5f,  -593.0f, 43.2f),
            new(-187.0f,   499.0f, 68.3f),
            new( 104.0f,  6626.0f, 31.8f),
        };

        private static readonly string[] AggressorModels =
        {
            "a_m_m_trampbeac_01", "a_m_y_methhead_01", "a_m_m_farmer_01",
            "a_m_y_musclbeac_01", "a_m_m_salton_01",
        };
        private static readonly string[] VictimModels =
        {
            "a_f_m_business_02",  "a_f_y_hipster_01", "a_f_m_salton_01",
            "a_f_y_tourist_01",   "a_f_m_fatbla_01",
        };

        private static readonly uint[] ThreatWeapons =
        {
            (uint)WeaponHash.Knife,
            (uint)WeaponHash.Bat,
            (uint)WeaponHash.Crowbar,
            (uint)WeaponHash.Bottle,
        };

        private readonly AggressorProfile _profile;
        private readonly Vector3          _scenePos;
        private readonly string           _aggressorModel;
        private readonly string           _victimModel;
        private readonly uint             _threatWeapon;

        private Ped? _aggressor;
        private Ped? _victim;

        public DomesticDisturbanceCallout()
        {
            var rng      = new Random();
            _scenePos    = SceneLocations[rng.Next(SceneLocations.Length)];
            _aggressorModel = AggressorModels[rng.Next(AggressorModels.Length)];
            _victimModel    = VictimModels[rng.Next(VictimModels.Length)];
            _threatWeapon   = ThreatWeapons[rng.Next(ThreatWeapons.Length)];

            var roll = rng.Next(100);
            if      (roll < ResistiveChancePct) _profile = AggressorProfile.Resistive;
            else if (roll < ResistiveChancePct + ArmedChancePct) _profile = AggressorProfile.Armed;
            else                                _profile = AggressorProfile.Peaceful;

            Data.Description = _profile switch
            {
                AggressorProfile.Armed     => "Report of a domestic dispute — caller states one party may be armed. Use caution.",
                AggressorProfile.Resistive => "Report of a domestic disturbance. Physical altercation in progress.",
                _                          => "Report of a domestic disturbance. Verbal argument, no weapons reported.",
            };
        }

        public override Vector3 GetDispatchLocation() => _scenePos;

        public override bool CanBeDispatched()
        {
            var h = World.CurrentDayTime.Hours;
            return h is >= 14 or <= 3;
        }

        public override async Task OnCalloutAccepted(CancellationToken ct)
        {

            var aggModel = new Model(_aggressorModel);
            if (!await aggModel.Request(7_000)) { CalloutFailed(); return; }

            _aggressor = TrackEntity(await World.CreatePed(aggModel, _scenePos, 0f));
            aggModel.MarkAsNoLongerNeeded();
            if (_aggressor is null || !_aggressor.Exists()) { CalloutFailed(); return; }

            var vicModel = new Model(_victimModel);
            if (!await vicModel.Request(7_000)) { CalloutFailed(); return; }

            var victimPos = _scenePos + new Vector3(2.0f, 0f, 0f);
            _victim = TrackEntity(await World.CreatePed(vicModel, victimPos, 180f));
            vicModel.MarkAsNoLongerNeeded();
            if (_victim is null || !_victim.Exists()) { CalloutFailed(); return; }

            var blip         = TrackBlip(World.CreateBlip(_scenePos));
            blip.Sprite      = BlipSprite.Safehouse;
            blip.Color       = BlipColor.Orange;
            blip.Name        = "Domestic Disturbance";
            blip.IsShortRange = false;
            blip.ShowRoute   = true;

            _aggressor.BlockPermanentEvents = true;
            _victim.BlockPermanentEvents    = true;

            API.TaskCombatPed(_aggressor.Handle, _victim.Handle, 0, 16);

            API.TaskSmartFleePed(_victim.Handle, _aggressor.Handle, 100f, -1, false, false);

            if (_profile == AggressorProfile.Armed)
            {
                API.GiveWeaponToPed(_aggressor.Handle, _threatWeapon, 1, false, true);
            }

            ClientBrain.ShowNotification(
                $"~b~Domestic Disturbance~w~ | Respond to scene. " +
                (_profile == AggressorProfile.Armed ? "~r~Weapon reported~w~." : ""));

            bool arrived = await WaitForArrivalAsync(ct);
            if (!arrived) return;

            switch (_profile)
            {
                case AggressorProfile.Peaceful:
                    await PeacefulOutcomeAsync(ct);
                    break;
                case AggressorProfile.Resistive:
                    await ResistiveOutcomeAsync(ct);
                    break;
                case AggressorProfile.Armed:
                    await ArmedOutcomeAsync(ct);
                    break;
            }
        }

        private async Task<bool> WaitForArrivalAsync(CancellationToken ct)
        {
            const float ArrivalDistM = 25.0f;
            const int   PollMs       = 500;

            while (!ct.IsCancellationRequested)
            {
                var dist = Vector3.Distance(Game.PlayerPed.Position, _scenePos);
                if (dist <= ArrivalDistM) return true;

                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName(
                    $"~b~Scene~w~ ~y~{dist:F0}m~w~ away — respond Code 3");
                API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);

                await Task.Delay(PollMs, ct).ConfigureAwait(false);
            }
            return false;
        }

        private async Task PeacefulOutcomeAsync(CancellationToken ct)
        {
            _aggressor!.Task.StandStill(-1);
            _victim!.Task.StandStill(-1);

            ClientBrain.ShowNotification(
                "~g~Both parties cooperating~w~ | Separate and interview. " +
                "Scene secure — ~b~/er_end_callout~w~ when ready.");

            await Task.Delay(10_000, ct).ConfigureAwait(false);
            if (!ct.IsCancellationRequested)
                CalloutCompleted();
        }

        private async Task ResistiveOutcomeAsync(CancellationToken ct)
        {
            _aggressor!.Task.StandStill(-1);
            _victim!.Task.StandStill(-1);

            ClientBrain.ShowNotification(
                "~o~Aggressor refusing commands~w~ | Approach to detain.");

            const float DetainDistM = 4.0f;
            while (!ct.IsCancellationRequested)
            {
                var dist = Vector3.Distance(Game.PlayerPed.Position, _aggressor.Position);
                if (dist <= DetainDistM) break;
                await Task.Delay(300, ct).ConfigureAwait(false);
            }
            if (ct.IsCancellationRequested) return;

            _aggressor.BlockPermanentEvents = true;
            API.SetPedFleeAttributes(_aggressor.Handle, 0, false);
            API.TaskCombatPed(_aggressor.Handle, Game.PlayerPed.Handle, 0, 16);

            ClientBrain.ShowNotification(
                "~r~Aggressor resisting~w~ | Subdue and ~b~/er_cuff~w~.");

            await WaitForAggressorCuffedOrDown(ct);
        }

        private async Task ArmedOutcomeAsync(CancellationToken ct)
        {
            _aggressor!.Task.StandStill(-1);
            _victim!.Task.StandStill(-1);

            ClientBrain.ShowNotification(
                "~r~ARMED SUSPECT~w~ | Do not approach — tase or disarm first.");

            const float ThreatDistM = 8.0f;
            while (!ct.IsCancellationRequested)
            {
                var dist = Vector3.Distance(Game.PlayerPed.Position, _aggressor.Position);
                if (dist <= ThreatDistM) break;
                await Task.Delay(300, ct).ConfigureAwait(false);
            }
            if (ct.IsCancellationRequested) return;

            _aggressor.BlockPermanentEvents = true;
            API.SetPedFleeAttributes(_aggressor.Handle, 0, false);
            API.TaskCombatPed(_aggressor.Handle, Game.PlayerPed.Handle, 0, 16);

            ClientBrain.ShowNotification(
                "~r~WEAPON RAISED~w~ | Tase or shoot to disarm — then ~b~/er_cuff~w~.");

            await WaitForAggressorCuffedOrDown(ct);
        }

        private async Task WaitForAggressorCuffedOrDown(CancellationToken ct)
        {
            if (_aggressor is null) return;

            ArrestManager.RegisterSuspect(_aggressor);

            const int PollMs = 300;
            while (!ct.IsCancellationRequested)
            {
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName(
                    "Subdue suspect then type ~b~/er_cuff~w~ to arrest");
                API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);

                if (ArrestManager.IsCuffed &&
                    ArrestManager.CuffedPed?.Handle == _aggressor.Handle)
                {
                    ClientBrain.ShowNotification(
                        "~g~Aggressor arrested~w~ | Scene secure. " +
                        "Press ~b~/er_end_callout~w~ when ready.");
                    CalloutCompleted();
                    return;
                }

                if (!_aggressor.Exists() || _aggressor.IsDead)
                {
                    ArrestManager.UnregisterSuspect(_aggressor);
                    ClientBrain.ShowNotification("~g~Aggressor down~w~ | Scene secure.");
                    CalloutCompleted();
                    return;
                }

                await Task.Delay(PollMs, ct).ConfigureAwait(false);
            }

            ArrestManager.UnregisterSuspect(_aggressor);
        }

        public override void OnUpdate()
        {
            if (_aggressor is null || !_aggressor.Exists()) return;
            var dist = Vector3.Distance(Game.PlayerPed.Position, _aggressor.Position);
            if (dist is > 5f and < 60f)
            {
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName(
                    $"~o~Disturbance~w~ ~y~{dist:F0}m~w~ away");
                API.EndTextCommandDisplayHelp(0, false, false, 1_500);
            }
        }

        public override void OnCalloutDeclined()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Domestic disturbance call declined.");

        public override void OnCalloutFailed()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Domestic disturbance call cancelled.");
    }
}
