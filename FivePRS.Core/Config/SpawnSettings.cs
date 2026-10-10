using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class SpawnSettings
    {
        [JsonProperty("restoreLastLocation")]
        public bool RestoreLastLocation { get; set; } = true;

        [JsonProperty("saveIntervalSeconds")]
        public int SaveIntervalSeconds { get; set; } = 30;
    }
}
