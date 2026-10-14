using System;
using Newtonsoft.Json;

namespace FivePRS.Core.Models
{
    public sealed class TerminalPreferences
    {
        private static readonly string[] TextSizes = { "s", "m", "l" };

        [JsonProperty("textSize")]
        public string TextSize { get; set; } = "m";

        [JsonProperty("clock24")]
        public bool Clock24 { get; set; }

        [JsonProperty("reduceMotion")]
        public bool ReduceMotion { get; set; }

        [JsonProperty("terminalVolume")]
        public int TerminalVolume { get; set; } = 70;

        [JsonProperty("dispatchVolume")]
        public int DispatchVolume { get; set; } = 90;

        [JsonProperty("holdChannel")]
        public bool HoldChannel { get; set; } = true;

        public static TerminalPreferences Parse(string? json)
        {
            TerminalPreferences? settings = null;
            if (json is not null && json.Length > 0 && json.Length <= 2000)
            {
                try
                {
                    settings = JsonConvert.DeserializeObject<TerminalPreferences>(json);
                }
                catch (JsonException)
                {
                }
            }

            return Sanitize(settings ?? new TerminalPreferences());
        }

        public static TerminalPreferences Sanitize(TerminalPreferences settings)
        {
            if (Array.IndexOf(TextSizes, settings.TextSize) < 0) settings.TextSize = "m";
            settings.TerminalVolume = Math.Max(0, Math.Min(100, settings.TerminalVolume));
            settings.DispatchVolume = Math.Max(0, Math.Min(100, settings.DispatchVolume));
            return settings;
        }

        public string ToJson() => JsonConvert.SerializeObject(this);
    }
}
