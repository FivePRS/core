using System;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Client;

namespace FivePRS.EMS
{
    public static class Treatment
    {
        private const string MedicDict = "amb@medic@standing@tendtodead@idle_a";
        private const string MedicClip = "idle_a";
        private const float AbortRange = 3.5f;

        public static async Task<bool> RunAsync(Func<Vector3?> patientPosition, int durationMs, string label)
        {
            var medic = Game.PlayerPed;
            await Animations.PlayAsync(medic, MedicDict, MedicClip);

            var started = API.GetGameTimer();
            try
            {
                while (API.GetGameTimer() - started < durationMs)
                {
                    var position = patientPosition();
                    if (position is null || Vector3.Distance(medic.Position, position.Value) > AbortRange)
                    {
                        ClientBrain.ShowNotification("Treatment interrupted.", "~b~EMS");
                        return false;
                    }

                    ClientBrain.ShowHelp($"{label}… ~g~{(API.GetGameTimer() - started) * 100 / durationMs}%", 300);
                    await BaseScript.Delay(250);
                }

                return true;
            }
            finally
            {
                medic.Task.ClearAnimation(MedicDict, MedicClip);
            }
        }
    }

    public static class Animations
    {
        public static async Task PlayAsync(Ped ped, string dict, string clip)
        {
            API.RequestAnimDict(dict);
            for (var i = 0; i < 100 && !API.HasAnimDictLoaded(dict); i++)
                await BaseScript.Delay(50);

            if (ped.Exists())
                await ped.Task.PlayAnimation(dict, clip, 8f, -8f, -1, AnimationFlags.Loop, 0f);
        }
    }
}
