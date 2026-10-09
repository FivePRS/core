using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;

namespace FivePRS.Client.Arrest
{
    public enum ArrestState
    {
        None,
        Cuffed,
        Escorted
    }

    public static class ArrestManager
    {
        public const float CuffRangeM    = 2.5f;
        public const float EscortRangeM  = 5.0f;

        private const string CuffAnimDict = "mp_arresting";
        private const string CuffAnimClip = "idle";
        private const string OfficerAnimDict = "mp_arresting";
        private const string OfficerAnimClip = "a_uncuff";

        private static readonly HashSet<int> _registeredSuspects = new();

        public static Ped?       CuffedPed  { get; private set; }
        public static ArrestState State     { get; private set; } = ArrestState.None;

        public static bool IsCuffed  => State == ArrestState.Cuffed;
        public static bool IsEscorted => State == ArrestState.Escorted;
        public static bool HasCustody => CuffedPed is not null;

        public static bool IsInCustody(Ped ped) => CuffedPed is not null && CuffedPed.Handle == ped.Handle;

        public static void RegisterSuspect(Ped ped)
        {
            if (ped?.Exists() == true)
                _registeredSuspects.Add(ped.Handle);
        }

        public static void UnregisterSuspect(Ped ped)
        {
            if (ped is not null)
                _registeredSuspects.Remove(ped.Handle);
        }

        public static async Task<bool> TryCuffNearestAsync()
        {
            if (HasCustody)
            {
                ClientBrain.ShowNotification("~r~Already have a suspect in custody.");
                return false;
            }

            var suspect = FindNearestRegisteredSuspect();
            if (suspect is null)
            {
                ClientBrain.ShowNotification(
                    $"~r~No suspect within {CuffRangeM}m.~w~ Get closer first.");
                return false;
            }

            await ExecuteCuffSequenceAsync(suspect);
            return true;
        }

        public static async Task CuffPedAsync(Ped suspect)
        {
            if (suspect is null || !suspect.Exists() || HasCustody) return;
            await ExecuteCuffSequenceAsync(suspect);
        }

        private static async Task ExecuteCuffSequenceAsync(Ped suspect)
        {
            var player = Game.PlayerPed;

            player.Task.TurnTo(suspect);
            suspect.Task.TurnTo(player);
            await BaseScript.Delay(400);

            await LoadAnimDictAsync(OfficerAnimDict);
            await player.Task.PlayAnimation(OfficerAnimDict, OfficerAnimClip,
                blendInSpeed:  8f, blendOutSpeed: -8f,
                duration:      1_500,
                flags:         AnimationFlags.UpperBodyOnly | AnimationFlags.AllowRotation,
                playbackRate:  1f);

            suspect.Task.ClearAll();
            await ApplyCuffedAnimationAsync(suspect);

            await BaseScript.Delay(1_200);
            player.Task.ClearAnimation(OfficerAnimDict, OfficerAnimClip);

            suspect.IsPositionFrozen = false;
            suspect.BlockPermanentEvents = true;
            API.SetPedFleeAttributes(suspect.Handle, 0, false);
            API.SetPedCombatAttributes(suspect.Handle, 17, false);

            CuffedPed = suspect;
            State     = ArrestState.Cuffed;

            StartFollowTask(suspect);

            ClientBrain.ShowNotification(
                "~g~Suspect cuffed.");
            ClientBrain.ShowHelp($"{Binds.Uncuff} Release  {Binds.Escort} Place in vehicle", 6_000);

            ClientEvents.TriggerLocal(EventNames.LocalSuspectCuffed, suspect.Handle);

            Debug.WriteLine($"[ArrestManager] Suspect (handle {suspect.Handle}) cuffed.");
        }

        public static void Uncuff()
        {
            if (CuffedPed is null) return;

            var suspect = CuffedPed;
            CuffedPed = null;
            State     = ArrestState.None;
            _registeredSuspects.Remove(suspect.Handle);

            if (!suspect.Exists()) return;

            API.ClearPedTasksImmediately(suspect.Handle);
            suspect.BlockPermanentEvents = false;
            API.SetPedFleeAttributes(suspect.Handle, 512, true);

            var handle = suspect.Handle;
            API.SetEntityAsNoLongerNeeded(ref handle);

            ClientBrain.ShowNotification("~o~Suspect released.");
            ClientEvents.TriggerLocal(EventNames.LocalSuspectUncuffed, suspect.Handle);

            Debug.WriteLine($"[ArrestManager] Suspect (handle {suspect.Handle}) released.");
        }

        public static async Task<bool> EscortToVehicleAsync(Vehicle vehicle)
        {
            if (!IsCuffed || CuffedPed is null)
            {
                ClientBrain.ShowNotification("~r~No suspect in custody.");
                return false;
            }

            if (vehicle is null || !vehicle.Exists())
            {
                ClientBrain.ShowNotification("~r~No vehicle to escort to.");
                return false;
            }

            var distToVehicle = Vector3.Distance(Game.PlayerPed.Position, vehicle.Position);
            if (distToVehicle > EscortRangeM)
            {
                ClientBrain.ShowNotification(
                    $"~r~Too far from vehicle~w~ ({distToVehicle:F0}m). " +
                    $"Get within ~w~{EscortRangeM}m~r~ first.");
                return false;
            }

            var suspect = CuffedPed;

            suspect.Task.ClearAll();
            API.TaskWarpPedIntoVehicle(suspect.Handle, vehicle.Handle, (int)VehicleSeat.LeftRear);
            await BaseScript.Delay(500);

            await ApplyCuffedAnimationAsync(suspect);

            State = ArrestState.Escorted;

            ClientBrain.ShowNotification("~g~Suspect secured in vehicle.");
            ClientEvents.TriggerLocal(EventNames.LocalSuspectEscorted, suspect.Handle, vehicle.Handle);

            Debug.WriteLine($"[ArrestManager] Suspect escorted to vehicle {vehicle.Handle}.");
            return true;
        }

        private static Ped? FindNearestRegisteredSuspect()
        {
            var playerPos  = Game.PlayerPed.Position;
            Ped? nearest   = null;
            var nearestDist = float.MaxValue;

            foreach (var handle in _registeredSuspects)
            {
                if (Entity.FromHandle(handle) is not Ped ped || !ped.Exists() || ped.IsDead)
                    continue;

                var dist = Vector3.Distance(playerPos, ped.Position);
                if (dist < CuffRangeM && dist < nearestDist)
                {
                    nearest     = ped;
                    nearestDist = dist;
                }
            }

            return nearest;
        }

        internal static void StartFollowTask(Ped suspect)
        {
            if (suspect is null || !suspect.Exists()) return;

            API.TaskFollowToOffsetOfEntity(
                suspect.Handle,
                Game.PlayerPed.Handle,
                -0.5f, -0.6f, 0f,
                1.0f,
                -1,
                0.3f,
                true);
        }

        internal static async Task ApplyCuffedAnimationAsync(Ped suspect)
        {
            if (suspect is null || !suspect.Exists()) return;

            await LoadAnimDictAsync(CuffAnimDict);
            await suspect.Task.PlayAnimation(
                CuffAnimDict, CuffAnimClip,
                blendInSpeed:  8f,
                blendOutSpeed: -8f,
                duration:      -1,
                flags:         AnimationFlags.Loop | AnimationFlags.StayInEndFrame,
                playbackRate:  0f);
        }

        internal static void ClearCuffedState()
        {
            CuffedPed = null;
            State     = ArrestState.None;
            _registeredSuspects.Clear();
        }

        private static async Task LoadAnimDictAsync(string dict)
        {
            if (API.HasAnimDictLoaded(dict)) return;
            API.RequestAnimDict(dict);
            for (var i = 0; i < 100 && !API.HasAnimDictLoaded(dict); i++)
                await BaseScript.Delay(50);
        }
    }
}
