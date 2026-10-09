using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class BrandingSettings
    {
        [JsonProperty("serverIcon")]
        public string ServerIcon { get; set; } = "https://cdn.fiveprs.org/fiveprs/server_icon.png";

        [JsonProperty("notificationLogo")]
        public string NotificationLogo { get; set; } = "https://cdn.fiveprs.org/fiveprs/fiveprs_logo.png";

        [JsonProperty("nameplate")]
        public string Nameplate { get; set; } = "https://cdn.fiveprs.org/fiveprs/nameplate.png";
    }
}
