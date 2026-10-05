using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class ResourceSettings
    {
        [JsonProperty("dispatchIntervalMinutes")]
        public int DispatchIntervalMinutes { get; set; } = 5;

        [JsonProperty("acceptWindowSeconds")]
        public int AcceptWindowSeconds { get; set; } = 30;

        [JsonProperty("initialGraceSeconds")]
        public int InitialGraceSeconds { get; set; } = 45;

        [JsonProperty("postCompleteCooldownSeconds")]
        public int PostCompleteCooldownSeconds { get; set; } = 60;

        [JsonProperty("postDeclineCooldownSeconds")]
        public int PostDeclineCooldownSeconds { get; set; } = 45;

        [JsonProperty("postFailCooldownSeconds")]
        public int PostFailCooldownSeconds { get; set; } = 30;

        [JsonProperty("noCalloutRetrySeconds")]
        public int NoCalloutRetrySeconds { get; set; } = 30;
    }
}
