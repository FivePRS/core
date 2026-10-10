using System.Collections.Generic;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class OffenceDef
    {
        [JsonProperty("label")]
        public string Label { get; set; } = string.Empty;

        [JsonProperty("fine")]
        public int Fine { get; set; }
    }

    public sealed class CitationsConfig
    {
        [JsonProperty("offences", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<OffenceDef> Offences { get; set; } = new()
        {
            new OffenceDef { Label = "Speeding",                         Fine = 250 },
            new OffenceDef { Label = "Reckless driving",                 Fine = 750 },
            new OffenceDef { Label = "Running a red light",              Fine = 300 },
            new OffenceDef { Label = "Failure to signal",                Fine = 100 },
            new OffenceDef { Label = "Illegal lane change",              Fine = 150 },
            new OffenceDef { Label = "Driving without a license",        Fine = 500 },
            new OffenceDef { Label = "Driving with a suspended license", Fine = 1000 },
            new OffenceDef { Label = "Unregistered vehicle",             Fine = 400 },
            new OffenceDef { Label = "Broken tail light",                Fine = 75 },
            new OffenceDef { Label = "Open container",                   Fine = 200 },
        };
    }
}
