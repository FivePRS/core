using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class PedOption
    {
        [JsonProperty("model")]
        public string Model { get; set; } = string.Empty;

        [JsonProperty("label")]
        public string Label { get; set; } = string.Empty;
    }

    public sealed class CreatorSettings
    {
        [JsonProperty("allowStandardPeds")]
        public bool AllowStandardPeds { get; set; } = true;

        [JsonProperty("standardPeds", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<PedOption> StandardPeds { get; set; } = new()
        {
            Ped("a_m_y_skater_01", "Skater"),
            Ped("a_m_y_hipster_01", "Hipster (male)"),
            Ped("a_f_y_hipster_01", "Hipster (female)"),
            Ped("a_m_y_business_01", "Business (male)"),
            Ped("a_f_y_business_01", "Business (female)"),
            Ped("a_m_y_beach_01", "Beach (male)"),
            Ped("a_f_y_beach_01", "Beach (female)"),
            Ped("a_m_y_hiker_01", "Hiker (male)"),
            Ped("a_f_y_hiker_01", "Hiker (female)"),
            Ped("a_m_m_tourist_01", "Tourist (male)"),
            Ped("a_f_m_tourist_01", "Tourist (female)"),
            Ped("a_m_m_farmer_01", "Farmer"),
        };

        public IEnumerable<string> AllowedStandardModels =>
            AllowStandardPeds ? StandardPeds.Select(p => p.Model) : Enumerable.Empty<string>();

        private static PedOption Ped(string model, string label) => new() { Model = model, Label = label };
    }
}
