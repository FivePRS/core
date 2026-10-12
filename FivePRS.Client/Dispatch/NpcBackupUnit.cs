using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Config;

namespace FivePRS.Client.Dispatch
{
    internal sealed class NpcBackupUnit
    {
        private const float SpawnDistance    = 200f;
        private const float MinSpawnDistance = 100f;
        private const float ArriveDistance   = 35f;
        private const float RedirectDistance = 60f;
        private const float FollowDistance   = 10f;
        private const float StandDistance    = 5f;
        private const int   DrivingStyle     = 786603;
        private const int   CopPedType       = 6;
        private const int   LeaveTimeoutMs   = 25_000;

        private enum Stage { Driving, OnScene, Leaving }

        private sealed class SpawnPoint
        {
            public SpawnPoint(Vector3 position, float heading)
            {
                Position = position;
                Heading  = heading;
            }

            public Vector3 Position { get; }
            public float   Heading  { get; }
        }

        private readonly Vehicle         _vehicle;
        private readonly List<Ped>       _crew;
        private readonly HashSet<int>    _following = new();
        private readonly bool            _code3;
        private readonly Blip            _blip;
        private Stage   _stage = Stage.Driving;
        private Vector3 _target;
        private int     _leavingSince;

        private NpcBackupUnit(Vehicle vehicle, List<Ped> crew, Vector3 target, bool code3)
        {
            _vehicle = vehicle;
            _crew    = crew;
            _code3   = code3;

            _blip       = vehicle.AttachBlip();
            _blip.Sprite = BlipSprite.PoliceCar;
            _blip.Color  = BlipColor.Blue;
            _blip.Name   = "Backup unit";

            API.SetVehicleSiren(vehicle.Handle, code3);
            DriveTo(target);
        }

        public bool Released { get; private set; }

        public static async Task<NpcBackupUnit?> SpawnAsync(Vector3 target, bool code3, BackupSettings settings)
        {
            var spawn = FindSpawn(target);
            if (spawn is null) return null;

            var vehicleModel = new Model(settings.NpcVehicle);
            var pedModel     = new Model(settings.NpcPedModel);
            if (!await vehicleModel.Request(7_000) || !await pedModel.Request(7_000))
            {
                vehicleModel.MarkAsNoLongerNeeded();
                pedModel.MarkAsNoLongerNeeded();
                return null;
            }

            var vehicle = await World.CreateVehicle(vehicleModel, spawn.Position, spawn.Heading);
            vehicleModel.MarkAsNoLongerNeeded();
            if (vehicle is null || !vehicle.Exists())
            {
                pedModel.MarkAsNoLongerNeeded();
                return null;
            }

            API.SetVehicleEngineOn(vehicle.Handle, true, true, false);

            var crew  = new List<Ped>();
            var seats = Math.Max(1, Math.Min(settings.NpcCrew, 2));
            for (var seat = -1; seat < seats - 1; seat++)
            {
                var handle = API.CreatePedInsideVehicle(vehicle.Handle, CopPedType, (uint)pedModel.Hash, seat, true, false);
                if (handle == 0) continue;

                var ped = new Ped(handle);
                API.SetPedRelationshipGroupHash(handle, (uint)API.GetHashKey("COP"));
                API.GiveWeaponToPed(handle, (uint)API.GetHashKey("WEAPON_PISTOL"), 250, false, false);
                API.SetPedKeepTask(handle, true);
                ped.BlockPermanentEvents = true;
                crew.Add(ped);
            }
            pedModel.MarkAsNoLongerNeeded();

            if (crew.Count == 0)
            {
                vehicle.Delete();
                return null;
            }

            return new NpcBackupUnit(vehicle, crew, target, code3);
        }

        public void Update(Ped player)
        {
            if (Released) return;

            if (!_vehicle.Exists() || _crew.All(ped => !ped.Exists() || ped.IsDead))
            {
                Release();
                return;
            }

            switch (_stage)
            {
                case Stage.Driving:
                    UpdateDriving(player);
                    break;
                case Stage.OnScene:
                    UpdateOnScene(player);
                    break;
                case Stage.Leaving:
                    UpdateLeaving();
                    break;
            }
        }

        public void Dismiss()
        {
            if (Released || _stage == Stage.Leaving) return;

            _stage        = Stage.Leaving;
            _leavingSince = API.GetGameTimer();
            API.SetVehicleSiren(_vehicle.Handle, false);

            for (var i = 0; i < _crew.Count; i++)
            {
                var ped = _crew[i];
                if (!ped.Exists() || ped.IsDead || API.IsPedInVehicle(ped.Handle, _vehicle.Handle, false)) continue;

                ped.BlockPermanentEvents = true;
                API.TaskEnterVehicle(ped.Handle, _vehicle.Handle, LeaveTimeoutMs, i == 0 ? -1 : i - 1, 1.5f, 1, 0);
            }
        }

        public void Delete()
        {
            if (_blip.Exists()) _blip.Delete();
            foreach (var ped in _crew.Where(ped => ped.Exists())) ped.Delete();
            if (_vehicle.Exists()) _vehicle.Delete();
            Released = true;
        }

        private void UpdateDriving(Ped player)
        {
            var driver = _crew[0];
            if (!driver.Exists() || driver.IsDead || !API.IsPedInVehicle(driver.Handle, _vehicle.Handle, false))
            {
                ArriveOnScene();
                return;
            }

            if (Vector3.Distance(player.Position, _target) > RedirectDistance)
                DriveTo(player.Position);

            if (Vector3.Distance(_vehicle.Position, player.Position) <= ArriveDistance && _vehicle.Speed < 3f)
                ArriveOnScene();
        }

        private void ArriveOnScene()
        {
            _stage = Stage.OnScene;

            foreach (var ped in _crew.Where(ped => ped.Exists() && !ped.IsDead))
            {
                ped.BlockPermanentEvents = false;
                if (API.IsPedInVehicle(ped.Handle, _vehicle.Handle, false))
                    API.TaskLeaveVehicle(ped.Handle, _vehicle.Handle, 0);
            }
        }

        private void UpdateOnScene(Ped player)
        {
            foreach (var ped in _crew.Where(ped => ped.Exists() && !ped.IsDead))
            {
                if (API.IsPedInAnyVehicle(ped.Handle, false) || API.IsPedInCombat(ped.Handle, 0)) continue;

                var distance = Vector3.Distance(ped.Position, player.Position);
                if (distance > FollowDistance && !_following.Contains(ped.Handle))
                {
                    API.TaskGoToEntity(ped.Handle, player.Handle, -1, StandDistance, 2f, 0f, 0);
                    _following.Add(ped.Handle);
                }
                else if (distance <= StandDistance + 1f && _following.Remove(ped.Handle))
                {
                    API.TaskTurnPedToFaceEntity(ped.Handle, player.Handle, -1);
                }
            }
        }

        private void UpdateLeaving()
        {
            var aboard = _crew.Where(ped => ped.Exists() && !ped.IsDead)
                .All(ped => API.IsPedInVehicle(ped.Handle, _vehicle.Handle, false));
            if (!aboard && API.GetGameTimer() - _leavingSince < LeaveTimeoutMs) return;

            var driver = _crew.FirstOrDefault(ped => ped.Exists() && !ped.IsDead && API.GetPedInVehicleSeat(_vehicle.Handle, -1) == ped.Handle);
            if (driver is not null)
                API.TaskVehicleDriveWander(driver.Handle, _vehicle.Handle, 15f, DrivingStyle);

            Release();
        }

        private void DriveTo(Vector3 target)
        {
            _target = target;
            var driver = _crew[0];
            if (!driver.Exists()) return;

            API.TaskVehicleDriveToCoordLongrange(driver.Handle, _vehicle.Handle, target.X, target.Y, target.Z,
                _code3 ? 30f : 17f, DrivingStyle, ArriveDistance - 10f);
        }

        private void Release()
        {
            if (_blip.Exists()) _blip.Delete();
            foreach (var ped in _crew.Where(ped => ped.Exists())) ped.MarkAsNoLongerNeeded();
            if (_vehicle.Exists()) _vehicle.MarkAsNoLongerNeeded();
            Released = true;
        }

        private static SpawnPoint? FindSpawn(Vector3 target)
        {
            var start = new Random().Next(360);
            for (var step = 0; step < 8; step++)
            {
                var angle = (start + step * 45) * Math.PI / 180.0;
                var probe = target + new Vector3((float)Math.Cos(angle) * SpawnDistance, (float)Math.Sin(angle) * SpawnDistance, 0f);

                var node    = Vector3.Zero;
                var heading = 0f;
                if (!API.GetClosestVehicleNodeWithHeading(probe.X, probe.Y, probe.Z, ref node, ref heading, 1, 3f, 0)) continue;
                if (Vector3.Distance(node, target) < MinSpawnDistance) continue;

                return new SpawnPoint(node, heading);
            }

            return null;
        }
    }
}
