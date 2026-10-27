using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;
using FivePRS.Client.Agency;
using FivePRS.Core.Config;
using FivePRS.Core.Models;
using FivePRS.Core.Text;
using Newtonsoft.Json;

namespace FivePRS.Police.Radar
{
    public class LidarGun : BaseScript
    {
        private const int AttackControl = 24;
        private const int VehicleShapeFlags = 2;
        private const int LspdTint = 5;
        private const uint Unarmed = 0xA2719263;

        private static readonly uint Device = (uint)API.GetHashKey("WEAPON_FLAREGUN");

        private bool _on;
        private bool _hadDevice;
        private int _previousTint;
        private uint _previousWeapon;

        public LidarGun()
        {
            API.RegisterCommand(KeyCommands.Lidar, new Action<int, List<object>, string>((_, __, ___) => Toggle()), false);
            API.RegisterKeyMapping(KeyCommands.Lidar, "FivePRS: Toggle lidar gun", "keyboard", "K");

            EventHandlers["onClientResourceStop"] += new Action<string>(name =>
            {
                if (name == API.GetCurrentResourceName()) Stop();
            });
        }

        private static RadarSettings Settings => ConfigManager.Settings.Radar;

        private static bool CanUse =>
            BaseAgency.Active?.Department == Department.Police && Game.PlayerPed.Exists() && !Game.PlayerPed.IsDead && !Game.PlayerPed.IsInVehicle();

        private void Toggle()
        {
            if (!Settings.Lidar) return;
            if (_on)
            {
                Stop();
                return;
            }

            if (!CanUse)
            {
                ClientBrain.ShowNotification("Use the lidar on foot while you're on duty with Police.", "~b~Lidar");
                return;
            }

            var ped = Game.PlayerPed.Handle;
            _previousWeapon = (uint)API.GetSelectedPedWeapon(ped);
            _hadDevice = API.HasPedGotWeapon(ped, Device, false);
            _previousTint = _hadDevice ? API.GetPedWeaponTintIndex(ped, Device) : 0;

            API.GiveWeaponToPed(ped, Device, 1, false, true);
            API.SetPedWeaponTintIndex(ped, Device, LspdTint);
            API.SetCurrentPedWeapon(ped, Device, true);

            _on = true;
            Send("show", new { units = Settings.Metric ? "KM/H" : "MPH", anchor = HudAnchor.Minimap() });
            Tick += OnTick;
        }

        private void Stop()
        {
            if (!_on) return;
            _on = false;
            Tick -= OnTick;
            Send("hide", null);

            var ped = Game.PlayerPed;
            if (!ped.Exists()) return;

            if (_hadDevice) API.SetPedWeaponTintIndex(ped.Handle, Device, _previousTint);
            else API.RemoveWeaponFromPed(ped.Handle, Device);
            var restore = _previousWeapon != Device && API.HasPedGotWeapon(ped.Handle, _previousWeapon, false) ? _previousWeapon : Unarmed;
            API.SetCurrentPedWeapon(ped.Handle, restore, true);
        }

        private async Task OnTick()
        {
            if (!CanUse || (uint)API.GetSelectedPedWeapon(Game.PlayerPed.Handle) != Device)
            {
                Stop();
                return;
            }

            API.DisablePlayerFiring(API.PlayerId(), true);
            API.DisableControlAction(0, AttackControl, true);

            if (API.IsDisabledControlJustPressed(0, AttackControl)) Measure();
            await Task.FromResult(0);
        }

        private void Measure()
        {
            var target = AimedVehicle() ?? RaycastVehicle();
            if (target is null)
            {
                Send("reading", new { miss = true });
                return;
            }

            var distance = Vector3.Distance(Game.PlayerPed.Position, API.GetEntityCoords(target.Value, false));
            Send("reading", new
            {
                speed = Settings.ToDisplaySpeed(API.GetEntitySpeed(target.Value)),
                distance = (int)Math.Round(Settings.Metric ? distance : distance * 3.28084f),
                distanceUnits = Settings.Metric ? "m" : "ft",
            });
        }

        private static int? AimedVehicle()
        {
            var entity = 0;
            if (!API.IsPlayerFreeAiming(API.PlayerId()) || !API.GetEntityPlayerIsFreeAimingAt(API.PlayerId(), ref entity) || entity == 0)
                return null;

            if (API.IsEntityAVehicle(entity)) return entity;
            if (API.IsEntityAPed(entity) && API.IsPedInAnyVehicle(entity, false)) return API.GetVehiclePedIsIn(entity, false);
            return null;
        }

        private static int? RaycastVehicle()
        {
            var origin = API.GetGameplayCamCoord();
            var rotation = API.GetGameplayCamRot(2);
            var pitch = rotation.X * Math.PI / 180.0;
            var yaw = rotation.Z * Math.PI / 180.0;
            var direction = new Vector3(
                (float)(-Math.Sin(yaw) * Math.Abs(Math.Cos(pitch))),
                (float)(Math.Cos(yaw) * Math.Abs(Math.Cos(pitch))),
                (float)Math.Sin(pitch));
            var end = origin + direction * Settings.LidarRange;

            var test = API.StartExpensiveSynchronousShapeTestLosProbe(origin.X, origin.Y, origin.Z, end.X, end.Y, end.Z,
                VehicleShapeFlags, Game.PlayerPed.Handle, 7);

            var hit = false;
            var position = Vector3.Zero;
            var normal = Vector3.Zero;
            var entity = 0;
            API.GetShapeTestResult(test, ref hit, ref position, ref normal, ref entity);
            return hit && entity != 0 && API.IsEntityAVehicle(entity) ? entity : null;
        }

        private static void Send(string type, object? payload) =>
            API.SendNuiMessage(JsonConvert.SerializeObject(new { screen = "lidar", type, payload }));
    }
}
