using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Client
{
    public static class Teleporter
    {
        private const int FadeMs = 400;
        private const int CollisionTimeoutMs = 5000;

        public static async Task ToAsync(Vector3 position, float heading)
        {
            var ped = Game.PlayerPed.Handle;

            API.DoScreenFadeOut(FadeMs);
            await BaseScript.Delay(FadeMs);

            API.RequestCollisionAtCoord(position.X, position.Y, position.Z);
            API.SetEntityCoords(ped, position.X, position.Y, position.Z, false, false, false, false);
            API.SetEntityHeading(ped, heading);

            var until = API.GetGameTimer() + CollisionTimeoutMs;
            while (!API.HasCollisionLoadedAroundEntity(ped) && API.GetGameTimer() < until)
                await BaseScript.Delay(0);

            API.DoScreenFadeIn(FadeMs);
        }
    }
}
