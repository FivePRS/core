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
        name:            "Traffic Stop",
        department:      Department.Police,
        priority:        CalloutPriority.Low,
        weight:          30,
        cooldownSeconds: 180,
        xpReward:        60)]
    public sealed class TrafficStopCallout : CalloutBase
    {

        private  const int   WarrantChancePct      = 35;
        private  const int   FleeChancePct         = 45;
        private  const float ApproachDistM         = 5.0f;
        private  const float PursuitEscapeDistM    = 400.0f;
        private  const float ForceStopDistM        = 12.0f;

        private static readonly string[] VehicleModels =
        {
            "sultan", "tailgater", "oracle2", "fugitive",
            "primo", "emperor", "stanier", "stratum",
            "ingot", "premier", "manana", "greenwood",
        };

        private static readonly string[] DriverModels =
        {
            "a_m_m_business_01", "a_f_m_business_02",
            "a_m_y_hipster_01",  "a_f_y_hipster_01",
            "a_m_m_trampbeac_01","a_m_y_skater_01",
            "a_m_m_tourist_01",  "a_f_y_tourist_01",
        };

        private static readonly string[] WarrantPlates  = { "WNT 422", "FUG 819", "V4NT3D", "HOTSHOT" };
        private static readonly string[] CleanPlates    = { "LAX 501", "8YRE 44", "BLVD22", "COAST 7" };

        private readonly bool   _hasWarrant;
        private readonly bool   _willFlee;
        private readonly string _plate;
        private readonly string _vehicleModel;
        private readonly string _driverModel;
        private readonly Vector3 _spawnPos;

        private Vehicle? _suspectVehicle;
        private Ped?     _driver;

        public TrafficStopCallout()
        {
            var rng   = new Random();
            _hasWarrant  = rng.Next(100) < WarrantChancePct;
            _willFlee    = _hasWarrant && rng.Next(100) < FleeChancePct;
            _plate       = _hasWarrant
                ? WarrantPlates[rng.Next(WarrantPlates.Length)]
                : CleanPlates[rng.Next(CleanPlates.Length)];
            _vehicleModel = VehicleModels[rng.Next(VehicleModels.Length)];
            _driverModel  = DriverModels[rng.Next(DriverModels.Length)];

            var playerPos = Game.PlayerPed.Position;
            var heading   = Game.PlayerPed.Heading;
            var fwd       = new Vector3(
                -(float)Math.Sin(heading * Math.PI / 180.0),
                 (float)Math.Cos(heading * Math.PI / 180.0),
                 0f);
            var roughSpawn = playerPos + fwd * (80f + rng.Next(40));
            _spawnPos = World.GetNextPositionOnStreet(roughSpawn);

            Data.Description = _hasWarrant
                ? "Plate check returned an active warrant. Stop the vehicle — suspect may resist."
                : "Routine traffic stop. Approach the driver window to run plates.";
        }

        public override Vector3 GetDispatchLocation() => _spawnPos;

        public override bool CanBeDispatched() => true;

        public override async Task OnCalloutAccepted(CancellationToken ct)
        {
            var vehModel = new Model(_vehicleModel);
            if (!await vehModel.Request(7_000))
            {
                Debug.WriteLine($"[TrafficStopCallout] Vehicle model '{_vehicleModel}' failed.");
                CalloutFailed();
                return;
            }

            _suspectVehicle = TrackEntity(await World.CreateVehicle(vehModel, _spawnPos, 0f));
            vehModel.MarkAsNoLongerNeeded();

            if (_suspectVehicle is null || !_suspectVehicle.Exists())
            {
                Debug.WriteLine("[TrafficStopCallout] Vehicle creation failed.");
                CalloutFailed();
                return;
            }

            API.SetVehicleNumberPlateText(_suspectVehicle.Handle, _plate);

            var pedModel = new Model(_driverModel);
            if (!await pedModel.Request(7_000))
            {
                Debug.WriteLine($"[TrafficStopCallout] Driver model '{_driverModel}' failed.");
                CalloutFailed();
                return;
            }

            _driver = TrackEntity(await World.CreatePed(pedModel, _spawnPos, 0f));
            pedModel.MarkAsNoLongerNeeded();

            if (_driver is null || !_driver.Exists())
            {
                Debug.WriteLine("[TrafficStopCallout] Driver creation failed.");
                CalloutFailed();
                return;
            }

            API.TaskWarpPedIntoVehicle(_driver.Handle, _suspectVehicle.Handle, (int)VehicleSeat.Driver);
            _driver.BlockPermanentEvents = true;
            _driver.IsInvincible         = false;

            API.TaskVehicleDriveToCoordLongrange(
                _driver.Handle,
                _suspectVehicle.Handle,
                Game.PlayerPed.Position.X,
                Game.PlayerPed.Position.Y,
                Game.PlayerPed.Position.Z,
                20f,
                262144,
                5f);

            var vehBlip        = TrackBlip(_suspectVehicle.AttachBlip());
            vehBlip.Sprite     = BlipSprite.PersonalVehicleCar;
            vehBlip.Color      = BlipColor.Yellow;
            vehBlip.Name       = $"Suspect Vehicle · {_plate}";
            vehBlip.ShowRoute  = true;

            ClientBrain.ShowNotification(
                $"~b~Traffic Stop~w~ | Target plate: ~y~{_plate}~w~ | " +
                $"Activate lights to pull them over.");

            bool pulledOver = await WaitForPullOverAsync(ct);
            if (!pulledOver) return;

            bool approached = await WaitForPlayerApproachAsync(ct);
            if (!approached) return;

            await RunPlateCheckAsync(ct);
            if (ct.IsCancellationRequested) return;

            if (!_hasWarrant)
            {
                await CleanDriverOutcomeAsync(ct);
            }
            else if (_willFlee)
            {
                await FleeingDriverOutcomeAsync(ct);
            }
            else
            {
                await WarrantArrestOutcomeAsync(ct);
            }
        }

        private async Task<bool> WaitForPullOverAsync(CancellationToken ct)
        {
            ClientBrain.ShowNotification(
                "~b~Traffic Stop~w~ | Get behind the vehicle and activate ~y~lights/sirens~w~.");

            const int PollMs = 500;
            var timeout = DateTime.UtcNow.AddSeconds(90);

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollMs, ct).ConfigureAwait(false);

                if (_suspectVehicle is null || !_suspectVehicle.Exists())
                {
                    ClientBrain.ShowNotification("~r~Suspect vehicle destroyed~w~ | Callout failed.");
                    CalloutFailed();
                    return false;
                }

                if (_driver is null || !_driver.Exists() || _driver.IsDead)
                {
                    ClientBrain.ShowNotification("~r~Driver is down~w~ | Callout complete.");
                    CalloutCompleted();
                    return false;
                }

                var speed   = _suspectVehicle.Speed;
                var distToPlayer = Vector3.Distance(Game.PlayerPed.Position, _suspectVehicle.Position);

                if (speed < 1.0f && distToPlayer < 40f)
                {
                    _suspectVehicle.Speed = 0f;
                    API.SetVehicleEngineOn(_suspectVehicle.Handle, true, true, false);
                    _driver.Task.StandStill(-1);
                    return true;
                }

                if (DateTime.UtcNow > timeout)
                {
                    ClientBrain.ShowNotification("~r~Vehicle didn't stop~w~ | Callout failed.");
                    CalloutFailed();
                    return false;
                }
            }

            return false;
        }

        private async Task<bool> WaitForPlayerApproachAsync(CancellationToken ct)
        {
            var windowBlip        = TrackBlip(World.CreateBlip(_suspectVehicle!.Position));
            windowBlip.Sprite     = BlipSprite.Standard;
            windowBlip.Color      = BlipColor.Green;
            windowBlip.Name       = "Driver Window";
            windowBlip.Scale      = 0.7f;

            const int PollMs = 300;

            while (!ct.IsCancellationRequested)
            {
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName(
                    "Approach the driver window to run plates");
                API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);

                await Task.Delay(PollMs, ct).ConfigureAwait(false);

                if (_driver is null || !_driver.Exists() || _driver.IsDead)
                {
                    ClientBrain.ShowNotification("~g~Driver down~w~ | Callout complete.");
                    CalloutCompleted();
                    return false;
                }

                var dist = Vector3.Distance(Game.PlayerPed.Position, _driver.Position);
                if (dist <= ApproachDistM)
                    return true;
            }

            return false;
        }

        private async Task RunPlateCheckAsync(CancellationToken ct)
        {
            ClientBrain.ShowNotification($"~b~MDT~w~ | Running plate ~y~{_plate}~w~…");
            await Task.Delay(2_500, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            if (_hasWarrant)
            {
                ClientBrain.ShowNotification(
                    $"~r~WARRANT HIT~w~ | Plate ~y~{_plate}~w~ — active warrant for arrest. " +
                    $"Proceed with ~b~caution~w~.");
            }
            else
            {
                ClientBrain.ShowNotification(
                    $"~g~PLATE CLEAR~w~ | Plate ~y~{_plate}~w~ — no warrants on record.");
            }
        }

        private async Task CleanDriverOutcomeAsync(CancellationToken ct)
        {
            if (_driver is null) return;

            _driver.Task.LeaveVehicle();
            await Task.Delay(2_000, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            ClientBrain.ShowNotification(
                "~g~Driver cooperating~w~ | Issue verbal warning and release.");

            await Task.Delay(3_000, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            API.TaskWarpPedIntoVehicle(_driver.Handle, _suspectVehicle!.Handle, (int)VehicleSeat.Driver);
            await Task.Delay(500, ct).ConfigureAwait(false);

            API.TaskVehicleDriveWander(_driver.Handle, _suspectVehicle.Handle, 15f, 262144);

            ClientBrain.ShowNotification("~g~Traffic stop complete~w~ | Driver released with a warning.");
            CalloutCompleted();
        }

        private async Task FleeingDriverOutcomeAsync(CancellationToken ct)
        {
            if (_driver is null || _suspectVehicle is null) return;

            await Task.Delay(800, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            _driver.BlockPermanentEvents = true;
            API.SetDriverAggressiveness(_driver.Handle, 1.0f);
            API.SetDriverAbility(_driver.Handle, 1.0f);
            API.TaskVehicleEscort(
                _driver.Handle,
                _suspectVehicle.Handle,
                Game.PlayerPed.CurrentVehicle?.Handle ?? 0,
                -1,
                40f,
                262144,
                -1, 0, 30f);

            API.TaskVehicleDriveWander(_driver.Handle, _suspectVehicle.Handle, 35f, 786603);

            var vehBlips = _suspectVehicle.AttachedBlips;
            foreach (var b in vehBlips) { b.Color = BlipColor.Red; b.Name = $"PURSUIT · {_plate}"; }

            ClientBrain.ShowNotification(
                "~r~DRIVER FLEEING~w~ | Vehicle in pursuit! " +
                $"Uses plate ~y~{_plate}~w~.");

            await RunPursuitLoopAsync(ct);
        }

        private async Task WarrantArrestOutcomeAsync(CancellationToken ct)
        {
            if (_driver is null || _suspectVehicle is null) return;

            await Task.Delay(800, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            _driver.Task.LeaveVehicle();
            await Task.Delay(2_000, ct).ConfigureAwait(false);
            if (ct.IsCancellationRequested) return;

            _driver.Task.StandStill(-1);
            await TaskManager.AssignTaskAsync(_driver, PedTaskType.PutHandsUp);

            ArrestManager.RegisterSuspect(_driver);

            ClientBrain.ShowNotification(
                "~o~SUSPECT COMPLYING~w~ | Driver has a warrant — type ~b~/er_cuff~w~ to arrest.");

            const int PollMs = 250;
            while (!ct.IsCancellationRequested)
            {
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName("Type ~b~/er_cuff~w~ to arrest the driver");
                API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);

                if (ArrestManager.IsCuffed &&
                    ArrestManager.CuffedPed?.Handle == _driver.Handle)
                {
                    ClientBrain.ShowNotification(
                        "~g~Driver arrested~w~ | Warrant served. " +
                        "Press ~b~/er_end_callout~w~ when ready.");
                    CalloutCompleted();
                    return;
                }

                if (!_driver.Exists() || _driver.IsDead)
                {
                    ArrestManager.UnregisterSuspect(_driver);
                    ClientBrain.ShowNotification("~g~Driver down~w~ | Callout complete.");
                    CalloutCompleted();
                    return;
                }

                await Task.Delay(PollMs, ct).ConfigureAwait(false);
            }

            ArrestManager.UnregisterSuspect(_driver);
        }

        private async Task RunPursuitLoopAsync(CancellationToken ct)
        {
            const int PollMs = 500;

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(PollMs, ct).ConfigureAwait(false);

                if (_driver is null || !_driver.Exists() || _driver.IsDead)
                {
                    ClientBrain.ShowNotification("~g~Suspect down~w~ | Pursuit ended.");
                    CalloutCompleted();
                    return;
                }

                if (_suspectVehicle is null || !_suspectVehicle.Exists())
                {
                    ClientBrain.ShowNotification(
                        "~o~Vehicle disabled~w~ | Suspect on foot — pursue and arrest.");
                    await RunFootPursuitAsync(ct);
                    return;
                }

                var distToPlayer = Vector3.Distance(
                    Game.PlayerPed.Position, _suspectVehicle.Position);

                if (distToPlayer > PursuitEscapeDistM)
                {
                    ClientBrain.ShowNotification("~r~Suspect escaped~w~ | Pursuit failed.");
                    CalloutFailed();
                    return;
                }

                if (_suspectVehicle.Speed < 0.5f && distToPlayer < ForceStopDistM)
                {
                    bool driverIn = API.GetPedInVehicleSeat(
                        _suspectVehicle.Handle, (int)VehicleSeat.Driver) == _driver.Handle;

                    if (driverIn)
                    {
                        _driver.Task.LeaveVehicle();
                        await Task.Delay(1_500, ct).ConfigureAwait(false);
                        if (ct.IsCancellationRequested) return;

                        ClientBrain.ShowNotification(
                            "~g~Vehicle stopped~w~ | Suspect exiting — " +
                            "approach and ~b~/er_cuff~w~.");
                        await RunFootPursuitAsync(ct);
                        return;
                    }
                }

                if (distToPlayer < 80f)
                {
                    API.BeginTextCommandDisplayHelp("STRING");
                    API.AddTextComponentSubstringPlayerName(
                        $"~r~PURSUIT~w~ | Suspect vehicle ~y~{distToPlayer:F0}m");
                    API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);
                }
            }
        }

        private async Task RunFootPursuitAsync(CancellationToken ct)
        {
            if (_driver is null || !_driver.Exists()) { CalloutCompleted(); return; }

            _driver.BlockPermanentEvents = true;
            API.SetPedFleeAttributes(_driver.Handle, 0,    false);
            API.SetPedCombatAttributes(_driver.Handle, 17, true);
            await TaskManager.AssignTaskAsync(_driver, PedTaskType.FleeFromPlayer);

            ClientBrain.ShowNotification("~r~Suspect fleeing on foot~w~ | Pursue and arrest!");

            const float ArrestDistM   = 3.0f;
            const float EscapeDistM   = 300.0f;
            const int   PollMs        = 400;

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

                if (dist <= ArrestDistM)
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

                if (dist > EscapeDistM)
                {
                    ClientBrain.ShowNotification("~r~Suspect escaped~w~ | Callout failed.");
                    CalloutFailed();
                    return;
                }

                if (dist < 80f)
                {
                    API.BeginTextCommandDisplayHelp("STRING");
                    API.AddTextComponentSubstringPlayerName(
                        $"~r~Suspect~w~ ~y~{dist:F0}m~w~ away");
                    API.EndTextCommandDisplayHelp(0, false, false, PollMs + 50);
                }
            }
        }

        public override void OnUpdate()
        {
            if (_suspectVehicle is null || !_suspectVehicle.Exists()) return;

            var dist = Vector3.Distance(Game.PlayerPed.Position, _suspectVehicle.Position);
            if (dist is > 5f and < 80f && _suspectVehicle.Speed > 5f)
            {
                API.BeginTextCommandDisplayHelp("STRING");
                API.AddTextComponentSubstringPlayerName(
                    $"~r~Pursuit~w~ | Suspect ~y~{dist:F0}m");
                API.EndTextCommandDisplayHelp(0, false, false, 1_500);
            }
        }

        public override void OnCalloutDeclined()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Traffic stop declined.");

        public override void OnCalloutFailed()
            => ClientBrain.ShowNotification("~r~[ DISPATCH ]~w~ Traffic stop cancelled.");
    }
}
