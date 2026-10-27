using System;
using CitizenFX.Core.Native;
using Newtonsoft.Json;

namespace FivePRS.Police.Radar
{
    public sealed class RadarAntennaSetup
    {
        public bool Xmit { get; set; } = true;
        public string Mode { get; set; } = RadarOperator.Same;
    }

    public sealed class RadarOperator
    {
        public const string Same = "same";
        public const string Opposite = "opp";

        private const string Key = "fiveprs:radar";

        public RadarAntennaSetup Front { get; set; } = new() { Mode = Opposite };
        public RadarAntennaSetup Rear { get; set; } = new();
        public int FastLimit { get; set; } = -1;
        public bool FastLock { get; set; }
        public int Doppler { get; set; } = 40;
        public int Beep { get; set; } = 70;
        public int Scale { get; set; } = 100;
        public float? X { get; set; }
        public float? Y { get; set; }

        public static RadarOperator Load(int defaultFastLimit)
        {
            RadarOperator? loaded = null;
            try
            {
                var json = API.GetResourceKvpString(Key);
                if (!string.IsNullOrEmpty(json)) loaded = JsonConvert.DeserializeObject<RadarOperator>(json);
            }
            catch (JsonException)
            {
            }

            var result = loaded ?? new RadarOperator();
            if (result.FastLimit < 0) result.FastLimit = defaultFastLimit;
            return result.Clamped();
        }

        public void Save() => API.SetResourceKvp(Key, JsonConvert.SerializeObject(Clamped()));

        public RadarOperator Clamped()
        {
            Front ??= new RadarAntennaSetup();
            Rear ??= new RadarAntennaSetup { Mode = Same };
            Front.Mode = Front.Mode == Same ? Same : Opposite;
            Rear.Mode = Rear.Mode == Opposite ? Opposite : Same;
            FastLimit = Math.Max(0, Math.Min(250, FastLimit));
            Doppler = Math.Max(0, Math.Min(100, Doppler));
            Beep = Math.Max(0, Math.Min(100, Beep));
            Scale = Math.Max(60, Math.Min(160, Scale));
            if (X.HasValue) X = Math.Max(0f, Math.Min(0.95f, X.Value));
            if (Y.HasValue) Y = Math.Max(0f, Math.Min(0.95f, Y.Value));
            return this;
        }
    }
}
