using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class RadarSettings
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("units")]
        public string Units { get; set; } = "mph";

        [JsonProperty("range")]
        public float Range { get; set; } = 150f;

        [JsonProperty("speedAlert")]
        public int SpeedAlert { get; set; }

        [JsonProperty("plateReader")]
        public bool PlateReader { get; set; } = true;

        [JsonProperty("plateReaderRange")]
        public float PlateReaderRange { get; set; } = 35f;

        [JsonProperty("lidar")]
        public bool Lidar { get; set; } = true;

        [JsonProperty("lidarRange")]
        public float LidarRange { get; set; } = 300f;

        [JsonIgnore]
        public bool Metric => string.Equals(Units, "kmh", System.StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(Units, "km/h", System.StringComparison.OrdinalIgnoreCase);

        public int ToDisplaySpeed(float metresPerSecond) =>
            (int)System.Math.Round(metresPerSecond * (Metric ? 3.6f : 2.23694f));
    }
}
