using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class LicenseCardDef
    {
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        [JsonProperty("issuer")]
        public string Issuer { get; set; } = string.Empty;

        [JsonProperty("color")]
        public string Color { get; set; } = "#1d3a6e";

        [JsonProperty("accent")]
        public string Accent { get; set; } = "#f2c94c";

        [JsonProperty("image")]
        public string Image { get; set; } = string.Empty;
    }

    public sealed class LicenseTypeDef
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("selfService")]
        public bool SelfService { get; set; }

        [JsonProperty("class")]
        public string Class { get; set; } = string.Empty;

        [JsonProperty("validDays")]
        public int ValidDays { get; set; } = 1825;

        [JsonProperty("card")]
        public LicenseCardDef Card { get; set; } = new();
    }

    public sealed class StateDef
    {
        [JsonProperty("name")]
        public string Name { get; set; } = "San Andreas";

        [JsonProperty("abbreviation")]
        public string Abbreviation { get; set; } = "SA";
    }

    public sealed class LicensesConfig
    {
        [JsonProperty("state")]
        public StateDef State { get; set; } = new();

        [JsonProperty("types", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<LicenseTypeDef> Types { get; set; } = new()
        {
            Type("driver",     "Driver's License",            true,  "C", 1825, "DRIVER LICENSE",          "Department of Motor Vehicles", "#1d3a6e", "#f2c94c"),
            Type("motorcycle", "Motorcycle License",          true,  "M", 1825, "MOTORCYCLE LICENSE",      "Department of Motor Vehicles", "#1d3a6e", "#f2c94c"),
            Type("commercial", "Commercial Driver's License", false, "A", 1460, "COMMERCIAL DRIVER LICENSE", "Department of Motor Vehicles", "#3b2a6e", "#f2c94c"),
            Type("weapon",     "Weapon Permit",               false, "",  1095, "WEAPON PERMIT",           "Department of Justice",        "#7f1d1d", "#f8d7a8"),
            Type("pilot",      "Pilot License",               false, "",  1825, "PILOT CERTIFICATE",       "Aviation Administration",      "#0f4c5c", "#9be7f0"),
            Type("boat",       "Boating License",             true,  "",  1825, "BOATING LICENSE",         "Fish and Wildlife",            "#0e5a8a", "#bfe3ff"),
            Type("hunting",    "Hunting License",             true,  "",  365,  "HUNTING LICENSE",         "Fish and Wildlife",            "#3f6b2a", "#e9f5c9"),
        };

        public LicenseTypeDef? Find(string? id) =>
            Types.FirstOrDefault(t => string.Equals(t.Id, id, System.StringComparison.OrdinalIgnoreCase));

        private static LicenseTypeDef Type(string id, string name, bool selfService, string licenseClass, int validDays,
            string title, string issuer, string color, string accent) => new()
        {
            Id          = id,
            Name        = name,
            SelfService = selfService,
            Class       = licenseClass,
            ValidDays   = validDays,
            Card        = new LicenseCardDef { Title = title, Issuer = issuer, Color = color, Accent = accent },
        };
    }
}
