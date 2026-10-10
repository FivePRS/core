using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class UpdateSettings
    {
        [JsonProperty("checkForUpdates")]
        public bool CheckForUpdates { get; set; } = true;

        [JsonProperty("repository")]
        public string Repository { get; set; } = "FivePRS/core";

        [JsonProperty("assetName")]
        public string AssetName { get; set; } = "fiveprs.zip";

        [JsonProperty("checkIntervalHours")]
        public int CheckIntervalHours { get; set; } = 6;

        [JsonProperty("includePreReleases")]
        public bool IncludePreReleases { get; set; }
    }
}
