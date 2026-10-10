using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class DeathSettings
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("respawnSeconds")]
        public int RespawnSeconds { get; set; } = 180;

        [JsonProperty("notifyEms")]
        public bool NotifyEms { get; set; } = true;

        [JsonProperty("reviveXp")]
        public int ReviveXp { get; set; } = 25;
    }
}
