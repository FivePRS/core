using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class BackupSettings
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("cooldownSeconds")]
        public int CooldownSeconds { get; set; } = 30;

        [JsonProperty("npcEnabled")]
        public bool NpcEnabled { get; set; } = true;

        [JsonProperty("npcWaitSeconds")]
        public int NpcWaitSeconds { get; set; } = 45;

        [JsonProperty("npcVehicle")]
        public string NpcVehicle { get; set; } = "police";

        [JsonProperty("npcPedModel")]
        public string NpcPedModel { get; set; } = "s_m_y_cop_01";

        [JsonProperty("npcCrew")]
        public int NpcCrew { get; set; } = 2;
    }
}
