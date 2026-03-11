using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Client.Arrest
{
    /// <summary>
    /// Drives the per-frame logic for a cuffed suspect:
    /// ─ Keeps the suspect walking behind the officer.
    /// ─ Re-applies the cuffed idle animation every ~1 s if the engine clears it
    ///   (task changes such as entering a doorway will break the animation).
    ///
    /// Registered as a CitizenFX Tick handler; self-suspends when nobody is cuffed.
    /// </summary>
    public class ArrestTick : BaseScript
    {
        private const int AnimCheckIntervalMs = 1_000;

        private const int FollowRecheckIntervalMs = 2_000;

        private const float StandStillThresholdM = 0.5f;

        private int _animTickCounter   = 0;
        private int _followTickCounter = 0;

        public ArrestTick()
        {
            Tick += OnTickAsync;
        }

        private async Task OnTickAsync()
        {
            if (!ArrestManager.IsCuffed || ArrestManager.CuffedPed is null)
            {
                await Delay(1_000);
                return;
            }

            var suspect = ArrestManager.CuffedPed;

            if (!suspect.Exists() || suspect.IsDead)
            {
                ArrestManager.ClearCuffedState();
                await Delay(500);
                return;
            }

            if (ArrestManager.IsEscorted)
            {
                await Delay(500);
                return;
            }

            _animTickCounter += 50;
            if (_animTickCounter >= AnimCheckIntervalMs)
            {
                _animTickCounter = 0;

                bool isPlayingCuffAnim = API.IsEntityPlayingAnim(
                    suspect.Handle,
                    "mp_arresting",
                    "idle",
                    3);

                if (!isPlayingCuffAnim)
                {
                    _ = ArrestManager.ApplyCuffedAnimationAsync(suspect);
                }
            }

            _followTickCounter += 50;
            if (_followTickCounter >= FollowRecheckIntervalMs)
            {
                _followTickCounter = 0;

                var distToPlayer = Vector3.Distance(
                    suspect.Position, Game.PlayerPed.Position);

                if (distToPlayer <= StandStillThresholdM)
                {
                    suspect.Task.StandStill(-1);
                }
                else
                {
                    ArrestManager.StartFollowTask(suspect);
                }
            }

            await Delay(50);
        }
    }
}
