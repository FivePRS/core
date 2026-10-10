using System.Collections.Generic;
using CitizenFX.Core;
using CitizenFX.Core.Native;

namespace FivePRS.Fire
{
    public sealed class FireScene
    {
        private const int MaxSpread = 25;

        private readonly List<int> _fires = new();
        private int _quietChecks;

        public FireScene(Vector3 center, float radius)
        {
            Center = center;
            Radius = radius;
        }

        public Vector3 Center { get; }

        public float Radius { get; }

        public bool Ignited => _fires.Count > 0;

        public void Ignite(Vector3 position, bool gas = false) =>
            _fires.Add(API.StartScriptFire(position.X, position.Y, position.Z, MaxSpread, gas));

        public int ActiveFires => API.GetNumberOfFiresInRange(Center.X, Center.Y, Center.Z, Radius);

        public bool IsOut(Entity? burning = null)
        {
            var quiet = ActiveFires == 0 && (burning is null || !burning.Exists() || !API.IsEntityOnFire(burning.Handle));
            _quietChecks = quiet ? _quietChecks + 1 : 0;
            return _quietChecks >= 3;
        }

        public void Extinguish()
        {
            foreach (var fire in _fires)
                API.RemoveScriptFire(fire);
            _fires.Clear();
            API.StopFireInRange(Center.X, Center.Y, Center.Z, Radius);
        }

        public static bool TryGround(float x, float y, float nearZ, out Vector3 ground)
        {
            var z = 0f;
            var found = API.GetGroundZFor_3dCoord(x, y, nearZ + 50f, ref z, false);
            ground = new Vector3(x, y, z);

            var water = 0f;
            return found && !API.GetWaterHeight(x, y, z, ref water);
        }
    }
}
