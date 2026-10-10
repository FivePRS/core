using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class WorldSettings
    {
        [JsonProperty("disableWantedLevel")]
        public bool DisableWantedLevel { get; set; } = true;

        [JsonProperty("disableDispatchServices")]
        public bool DisableDispatchServices { get; set; } = true;

        [JsonProperty("disableRandomCops")]
        public bool DisableRandomCops { get; set; }
    }
}
