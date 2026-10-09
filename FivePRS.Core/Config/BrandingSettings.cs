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

        [JsonProperty("departments")]
        public DepartmentIcons Departments { get; set; } = new();
    }

    public sealed class DepartmentIcons
    {
        [JsonProperty("police")]
        public string Police { get; set; } = "https://cdn.fiveprs.org/fiveprs/departments/police.png";

        [JsonProperty("ems")]
        public string Ems { get; set; } = "https://cdn.fiveprs.org/fiveprs/departments/ems.png";

        [JsonProperty("fire")]
        public string Fire { get; set; } = "https://cdn.fiveprs.org/fiveprs/departments/fire.png";

        [JsonProperty("civilian")]
        public string Civilian { get; set; } = "https://cdn.fiveprs.org/fiveprs/departments/civilian.png";
    }
}
