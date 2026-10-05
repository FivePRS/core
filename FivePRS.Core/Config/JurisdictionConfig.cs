using System.Collections.Generic;
using FivePRS.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FivePRS.Core.Config
{
    public sealed class TerritoryDef
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("polygon", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<float[]> Polygon { get; set; } = new();
    }

    public sealed class AgencyDef
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("department")]
        [JsonConverter(typeof(StringEnumConverter))]
        public Department Department { get; set; }

        [JsonProperty("callsignPrefix")]
        public string CallsignPrefix { get; set; } = string.Empty;

        [JsonProperty("territories", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> Territories { get; set; } = new();
    }

    public sealed class JurisdictionConfig
    {
        [JsonProperty("territories", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<TerritoryDef> Territories { get; set; } = new()
        {
            new TerritoryDef
            {
                Id      = "los_santos",
                Name    = "Los Santos",
                Polygon = new List<float[]>
                {
                    new[] { -4500f, -4500f }, new[] { 4500f, -4500f },
                    new[] {  4500f,  1000f }, new[] { -4500f, 1000f },
                },
            },
            new TerritoryDef
            {
                Id      = "blaine_county",
                Name    = "Blaine County",
                Polygon = new List<float[]>
                {
                    new[] { -4500f, 1000f }, new[] { 4500f, 1000f },
                    new[] {  4500f, 8500f }, new[] { -4500f, 8500f },
                },
            },
        };

        [JsonProperty("agencies", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<AgencyDef> Agencies { get; set; } = new()
        {
            new AgencyDef
            {
                Id             = "lspd",
                Name           = "Los Santos Police Department",
                Department     = Department.Police,
                CallsignPrefix = "LSPD",
                Territories    = new List<string> { "los_santos" },
            },
            new AgencyDef
            {
                Id             = "bcso",
                Name           = "Blaine County Sheriff's Office",
                Department     = Department.Police,
                CallsignPrefix = "BCSO",
                Territories    = new List<string> { "blaine_county" },
            },
        };
    }
}
