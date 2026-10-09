using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class LicenseTypeDef
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("selfService")]
        public bool SelfService { get; set; }
    }

    public sealed class LicensesConfig
    {
        [JsonProperty("types")]
        public List<LicenseTypeDef> Types { get; set; } = new()
        {
            new LicenseTypeDef { Id = "driver",     Name = "Driver's License",            SelfService = true  },
            new LicenseTypeDef { Id = "motorcycle", Name = "Motorcycle License",          SelfService = true  },
            new LicenseTypeDef { Id = "commercial", Name = "Commercial Driver's License", SelfService = false },
            new LicenseTypeDef { Id = "weapon",     Name = "Weapon Permit",               SelfService = false },
            new LicenseTypeDef { Id = "pilot",      Name = "Pilot License",               SelfService = false },
            new LicenseTypeDef { Id = "boat",       Name = "Boating License",             SelfService = true  },
            new LicenseTypeDef { Id = "hunting",    Name = "Hunting License",             SelfService = true  },
        };

        public LicenseTypeDef? Find(string? id) =>
            Types.FirstOrDefault(t => string.Equals(t.Id, id, System.StringComparison.OrdinalIgnoreCase));
    }
}
