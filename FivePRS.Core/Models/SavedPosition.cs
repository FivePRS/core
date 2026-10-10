using System;

namespace FivePRS.Core.Models
{
    public sealed class SavedPosition
    {
        public const float MapLimit = 10_000f;
        public const float MinHeight = -200f;
        public const float MaxHeight = 3_000f;

        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Heading { get; set; }

        public static SavedPosition? Create(float x, float y, float z, float heading)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z) || !IsFinite(heading)) return null;
            if (Math.Abs(x) > MapLimit || Math.Abs(y) > MapLimit || z < MinHeight || z > MaxHeight) return null;
            if (Math.Abs(x) < 1f && Math.Abs(y) < 1f) return null;

            return new SavedPosition { X = x, Y = y, Z = z, Heading = ((heading % 360f) + 360f) % 360f };
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
