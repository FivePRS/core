using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Client.Tasks
{
    /// <summary>
    /// High-level task sequences exposed as an enum so callout authors never touch raw natives.
    /// </summary>
    public enum PedTaskType
    {
        StandStill,
        PutHandsUp,
        Kneel,
        EnterVehicle,
        LeaveVehicle,
        FleeFromPlayer,
        Surrender
    }

    /// <summary>
    /// Thin, static wrapper around FiveM ped task natives.
    /// All methods are null-safe and existence-checked so callout code stays clean.
    /// </summary>
    public static class TaskManager
    {
        public static async Task AssignTaskAsync(Ped ped, PedTaskType task, Vehicle? vehicle = null)
        {
            if (ped is null || !ped.Exists()) return;

            switch (task)
            {
                case PedTaskType.StandStill:
                    ped.Task.StandStill(-1);
                    break;

                case PedTaskType.PutHandsUp:
                    API.TaskHandsUp(ped.Handle, -1, Game.PlayerPed.Handle, -1, false);
                    break;

                case PedTaskType.Kneel:
                    await LoadAnimDictAsync("random@mugging3");
                    ped.Task.PlayAnimation(
                        "random@mugging3", "approach_stand_callback_victim_a",
                        blendInSpeed:  8f,
                        blendOutSpeed: -8f,
                        duration:      -1,
                        flags:         AnimationFlags.StayInEndFrame,
                        playbackRate:  0f);
                    break;

                case PedTaskType.EnterVehicle:
                    if (vehicle is not null && vehicle.Exists())
                        ped.Task.EnterVehicle(vehicle, VehicleSeat.Passenger, -1, 2f, 1);
                    break;

                case PedTaskType.LeaveVehicle:
                    ped.Task.LeaveVehicle(LeaveVehicleFlags.WarpOut);
                    break;

                case PedTaskType.FleeFromPlayer:
                    API.TaskSmartFleePed(ped.Handle, Game.PlayerPed.Handle, 200f, -1, false, false);
                    break;

                case PedTaskType.Surrender:
                    await LoadAnimDictAsync("missminuteman_1ig_2");
                    ped.Task.PlayAnimation(
                        "missminuteman_1ig_2", "handsup_base",
                        blendInSpeed:  8f,
                        blendOutSpeed: -8f,
                        duration:      -1,
                        flags:         AnimationFlags.Loop,
                        playbackRate:  0f);
                    break;
            }
        }

        public static void ClearTasks(Ped ped)
        {
            if (ped is null || !ped.Exists()) return;
            ped.Task.ClearAll();
        }

        public static async Task WaitForPedToReachPositionAsync(Ped ped, Vector3 target, float radius = 2f)
        {
            while (ped.Exists() && Vector3.Distance(ped.Position, target) > radius)
                await BaseScript.Delay(100);
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
