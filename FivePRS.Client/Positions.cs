using System;
using CitizenFX.Core;

namespace FivePRS.Client
{
    public static class Positions
    {
        public static Vector3 Around(Vector3 center, float distance, Random rng)
        {
            var angle = rng.NextDouble() * Math.PI * 2;
            return new Vector3(center.X + (float)Math.Cos(angle) * distance, center.Y + (float)Math.Sin(angle) * distance, center.Z);
        }
    }
}
