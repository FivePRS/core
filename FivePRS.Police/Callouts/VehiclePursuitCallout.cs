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
        name:            "Vehicle Pursuit",
        department:      Department.Police,
        priority:        CalloutPriority.High,
        weight:          10,
        cooldownSeconds: 420,
        xpReward:        150)]
    public sealed class VehiclePursuitCallout : CalloutBase
    {
        private const float EscapeDistM       = 500.0f;
        private const float SurrenderDistM    = 8.0f;
        private const float FootEscapeDistM   = 280.0f;
        private const float FootArrestDistM   = 3.5f;

        private const int   LostPursuitSec    = 25;

        private static readonly string[] StolenVehicles =
        {
            "buffalo3", "elegy2",  "jester3", "schafter3",
            "sultan",   "kuruma",  "issi2",   "sentinel",
            "gauntlet2","dominator","felon",   "jackal",
        };

        private static readonly string[] DriverModels =
        {
            "a_m_y_methhead_01", "a_m_y_lost01",   "a_m_m_trampbeac_01",
            "a_m_y_musclbeac_01","a_m_m_salton_01", "a_m_y_skater_01",
        };

        private static readonly Vector3[] FleeWaypoints =
        {
            new(  22.0f, -1420.0f,  29.0f),
            new( 750.0f,  -850.0f,  26.0f),
            new(-700.0f,  -240.0f,  36.0f),
            new( 200.0f,  3000.0f,  42.0f),
            new(-1800.0f, 800.0f,   138.0f),
            new( 2700.0f, 3300.0f,  55.0f),
            new(-2700.0f, 200.0f,   20.0f),
        };

        private readonly string  _vehicleModel;
        private readonly string  _driverModel;
        private readonly string  _plate;
        private readonly Vector3 _spawnPos;
        private readonly Vector3 _fleeDest;

        private Vehicle? _car;
        private Ped?     _driver;

        private DateTime _lastSeenTime = DateTime.UtcNow;

        public VehiclePursuitCallout()
        {
            var rng      = new Random();
            _vehicleModel = StolenVehicles[rng.Next(StolenVehicles.Length)];
            _driverModel  = DriverModels[rng.Next(DriverModels.Length)];
            _fleeDest     = FleeWaypoints[rng.Next(FleeWaypoints.Length)];

            _plate = $"S{rng.Next(10)}{rng.Next(10)}{rng.Next(10)} {(char)('A' + rng.Next(26))}{(char)('A' + rng.Next(26))}";

            var playerPos = Game.PlayerPed.Position;
            var heading   = Game.PlayerPed.Heading;
            var fwd = new Vector3(
                -(float)Math.Sin(heading * Math.PI / 180.0),
                 (float)Math.Cos(heading * Math.PI / 180.0),
                 0f);
            var roughSpawn = playerPos + fwd * (100f + rng.Next(50));
            _spawnPos = World.GetNextPositionOnStreet(roughSpawn);

            Data.Description =
                $"Stolen vehicle reported — plate ~y~{_plate}~w~. Vehicle already in motion. Intercept and stop.";
        }

        public override Vector3 GetDispatchLocation() => _spawnPos;

        public override bool CanBeDispatched() => true;

        public override async Task OnCalloutAccepted(CancellationToken ct)
        {
            var vehModel = new Model(_vehicleModel);
            if (!await vehModel.Request(7_000))
            {
                Debug.WriteLine($"[VehiclePursuitCallout] Model '{_vehicleModel}' failed.");
                CalloutFailed(); return;
            }

            _car = TrackEntity(await World.CreateVehicle(vehModel, _spawnPos, Game.PlayerPed.Heading));
            vehModel.MarkAsNoLongerNeeded();
            if (_car is null || !_car.Exists()) { CalloutFailed(); return; }

            API.SetVehicleNumberPlateText(_car.Handle, _plate);
            API.SetVehicleEngineOn(_car.Handle, true, true, false);

            var pedModel = new Model(_driverModel);
            if (!await pedModel.Request(7_000))
            {
                Debug.WriteLine($"[VehiclePursuitCallout] Driver model '{_driverModel}' failed.");
                CalloutFailed(); return;
            }

            _driver = TrackEntity(await World.CreatePed(pedModel, _spawnPos, 0f));
            pedModel.MarkAsNoLongerNeeded();
            if (_driver is null || !_driver.Exists()) { CalloutFailed(); return; }

            API.TaskWarpPedIntoVehicle(_driver.Handle, _car.Handle, (int)VehicleSeat.Driver);
            _driver.BlockPermanentEvents = true;
            _driver.IsInvincible         = false;

            BeginVehicleFlee();

            var blip       = TrackBlip(_car.AttachBlip());
            blip.Sprite    = BlipSprite.PersonalVehicleCar;
            blip.Color     = BlipColor.Red;
            blip.Name      = $"STOLEN · {_plate}";
            blip.ShowRoute = true;

            ClientBrain.ShowNotification(
                $"~r~VEHICLE PURSUIT~w~ | Stolen {_vehicleModel.ToUpper()} · plate ~y~{_plate}~w~. " +
                "Intercept and stop the vehicle!");

            await RunPursuitLoopAsync(ct);
        }

        private void BeginVehicleFlee()
        {
            if (_driver is null || _car is null) return;

            API.SetDriverAggressiveness(_driver.Handle, 1.0f);
            API.SetDriverAbility(_driver.Handle, 1.0f);
            API.TaskVehicleDriveToCoordLongrange(
                _driver.Handle,
                _car.Handle,
                _fleeDest.X, _fleeDest.Y, _fleeDest.Z,
                45f,
                786603,
                3f);
        }

        private async Task RunPursuitLoopAsync(CancellationToken ct)
        {
            const int PollMs = 400;

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollMs, ct).ConfigureAwait(false);

                if (_driver is null || !_driver.Exists() || _driver.IsDead)
                {
                    ClientBrain.ShowNotification("~g~Suspect down~w~ | Pursuit ended.");
                    CalloutCompleted();
                    return;
                }

                if (_car is null || !_car.Exists())
                {
                    ClientBrain.ShowNotification(
                        "~o~Vehicle disabled~w~ | Suspect on foot — pursue and arrest!");
                    await RunFootPursuitAsync(ct);
                    return;
                }

                var distToPlayer = Vector3.Distance(Game.PlayerPed.Position, _car.Position);

                if (distToPlayer > EscapeDistM)
                {
                    if ((DateTime.UtcNow - _lastSeenTime).TotalSeconds > LostPursuitSec)
                    {
                        ClientBrain.ShowNotification("~r~Suspect escaped~w~ | Pursuit lost.");
                        CalloutFailed();
                        return;
                    }
                }
                else
                {
                    _lastSeenTime = DateTime.UtcNow;
                }

                if (_car.Speed < 0.5f && distToPlayer < SurrenderDistM)
                {
                    bool driverIn = API.GetPedInVehicleSeat(
                        _car.Handle, (int)VehicleSeat.Driver) == _driver.Handle;

                    if (driverIn)
                    {
                        var willSurrender = new Random().Next(100) < 60;
                        if (willSurrender)
                        {
                            await SurrenderOutcomeAsync(ct);
                        }
                        else
                        {
                            _driver.Task.LeaveVehicle();
                            await Task.Delay(1_200, ct).ConfigureAwait(false);
                            ClientBrain.ShowNotification(
                                "~o~Suspect bailing~w~ | On foot — pursue and arrest!");
                            await RunFootPursuitAsync(ct);
                        }
                        return;
                    }
                }

                if (distToPlayer < 120f)
                {
                    API.BeginTextCommandDisplayHelp("STRING");
                    API.AddTextComponentSubstringPlayerName(
                        $"~r~PURSUIT~w~ | ~y~{distToPlayer:F0}m~w~ · " +
                        $"{(_car.Speed * 3.6f):F0} km/h");
                    API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);
                }
            }
        }

        private async Task SurrenderOutcomeAsync(CancellationToken ct)
        {
            if (_driver is null || _car is null) return;

            _driver.Task.LeaveVehicle();
            await Task.Delay(1_500, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            _driver.Task.StandStill(-1);
            await TaskManager.AssignTaskAsync(_driver, PedTaskType.PutHandsUp);

            ArrestManager.RegisterSuspect(_driver);

            ClientBrain.ShowNotification(
                "~g~Suspect surrendering~w~ | Approach and ~b~/er_cuff~w~ to arrest.");

            const int PollMs = 250;
            while (!ct.IsCancellationRequested)
            {
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName("Type ~b~/er_cuff~w~ to arrest the suspect");
                API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);

                if (ArrestManager.IsCuffed && ArrestManager.CuffedPed?.Handle == _driver.Handle)
                {
                    ClientBrain.ShowNotification(
                        "~g~Suspect arrested~w~ | Press ~b~/er_end_callout~w~ when ready.");
                    CalloutCompleted();
                    return;
                }

                if (!_driver.Exists() || _driver.IsDead)
                {
                    ArrestManager.UnregisterSuspect(_driver);
                    ClientBrain.ShowNotification("~g~Suspect down~w~ | Callout complete.");
                    CalloutCompleted();
                    return;
                }

                await Task.Delay(PollMs, ct).ConfigureAwait(false);
            }

            ArrestManager.UnregisterSuspect(_driver);
        }

        private async Task RunFootPursuitAsync(CancellationToken ct)
        {
            if (_driver is null || !_driver.Exists()) { CalloutCompleted(); return; }

            _driver.BlockPermanentEvents = true;
            API.SetPedFleeAttributes(_driver.Handle, 0,    false);
            API.SetPedCombatAttributes(_driver.Handle, 17, true);
            await TaskManager.AssignTaskAsync(_driver, PedTaskType.FleeFromPlayer);

            ClientBrain.ShowNotification("~r~Suspect fleeing on foot~w~ | Pursue and arrest!");

            const int PollMs = 350;

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollMs, ct).ConfigureAwait(false);

                if (!_driver.Exists() || _driver.IsDead)
                {
                    ClientBrain.ShowNotification("~g~Suspect down~w~ | Callout complete.");
                    CalloutCompleted();
                    return;
                }

                var dist = Vector3.Distance(Game.PlayerPed.Position, _driver.Position);

                if (dist <= FootArrestDistM)
                {
                    _driver.Task.StandStill(-1);
                    await TaskManager.AssignTaskAsync(_driver, PedTaskType.PutHandsUp);
                    ArrestManager.RegisterSuspect(_driver);

                    ClientBrain.ShowNotification(
                        "~g~Suspect cornered~w~ | Type ~b~/er_cuff~w~ to arrest.");

                    while (!ct.IsCancellationRequested)
                    {
                        API.BeginTextCommandDisplayHelp("STRING");
                        API.AddTextComponentSubstringPlayerName(
                            "Type ~b~/er_cuff~w~ to arrest the suspect");
                        API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);

                        if (ArrestManager.IsCuffed &&
                            ArrestManager.CuffedPed?.Handle == _driver.Handle)
                        {
                            ClientBrain.ShowNotification(
                                "~g~Suspect arrested~w~ | Press ~b~/er_end_callout~w~ when ready.");
                            CalloutCompleted();
                            return;
                        }

                        if (!_driver.Exists() || _driver.IsDead)
                        {
                            ArrestManager.UnregisterSuspect(_driver);
                            ClientBrain.ShowNotification("~g~Suspect down~w~ | Callout complete.");
                            CalloutCompleted();
                            return;
                        }

                        await Task.Delay(PollMs, ct).ConfigureAwait(false);
                    }

                    ArrestManager.UnregisterSuspect(_driver);
                    return;
                }

                if (dist > FootEscapeDistM)
                {
                    ClientBrain.ShowNotification("~r~Suspect escaped on foot~w~ | Pursuit lost.");
                    CalloutFailed();
                    return;
                }

                if (dist < 80f)
                {
                    API.BeginTextCommandDisplayHelp("STRING");
                    API.AddTextComponentSubstringPlayerName(
                        $"~r~Suspect on foot~w~ ~y~{dist:F0}m~w~ away");
                    API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);
                }
            }
        }

        public override void OnUpdate()
        {
            if (_car is null || !_car.Exists()) return;
            var dist = Vector3.Distance(Game.PlayerPed.Position, _car.Position);
            if (dist is > 5f and < 100f)
            {
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName(
                    $"~r~PURSUIT~w~ | ~y~{dist:F0}m~w~ · {(_car.Speed * 3.6f):F0} km/h");
                API.EndTextCommandDisplayHelp(0, false, false, 1_500);
            }
        }

        public override void OnCalloutDeclined()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Vehicle pursuit declined.");

        public override void OnCalloutFailed()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Vehicle pursuit cancelled.");
    }
}
