using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class VehicleTierDef
    {
        [JsonProperty("models")]
        public string[] Models { get; set; } = new[] { "police" };

        [JsonProperty("primaryColor")]
        public int PrimaryColor { get; set; } = 0;

        [JsonProperty("secondaryColor")]
        public int SecondaryColor { get; set; } = 0;

        [JsonProperty("dirtLevel")]
        public int DirtLevel { get; set; } = 2;

        [JsonProperty("livery")]
        public int Livery { get; set; } = -1;

        [JsonProperty("plateText")]
        public string PlateText { get; set; } = "";

        [JsonProperty("forcedExtras")]
        public int[] ForcedExtras { get; set; } = new int[0];

        [JsonProperty("disabledExtras")]
        public int[] DisabledExtras { get; set; } = new int[0];
    }

    public sealed class AgencyVehicleDef
    {
        [JsonProperty("patrol", NullValueHandling = NullValueHandling.Ignore)]
        public VehicleTierDef? Patrol { get; set; }

        [JsonProperty("senior", NullValueHandling = NullValueHandling.Ignore)]
        public VehicleTierDef? Senior { get; set; }

        [JsonProperty("command", NullValueHandling = NullValueHandling.Ignore)]
        public VehicleTierDef? Command { get; set; }
    }

    public sealed class PoliceVehiclesConfig
    {
        [JsonProperty("patrol")]
        public VehicleTierDef Patrol { get; set; } = new()
        {
            Models         = new[] { "police", "police2" },
            PrimaryColor   = 0,
            SecondaryColor = 0,
            DirtLevel      = 2,
            Livery         = 0,
            ForcedExtras   = new[] { 1, 2 },
            DisabledExtras = new[] { 5 },
        };

        [JsonProperty("senior")]
        public VehicleTierDef Senior { get; set; } = new()
        {
            Models         = new[] { "police2", "police4" },
            PrimaryColor   = 0,
            SecondaryColor = 0,
            DirtLevel      = 1,
            Livery         = 0,
            ForcedExtras   = new[] { 1, 2 },
        };

        [JsonProperty("command")]
        public VehicleTierDef Command { get; set; } = new()
        {
            Models         = new[] { "police3" },
            PrimaryColor   = 111,
            SecondaryColor = 111,
            DirtLevel      = 0,
            Livery         = -1,
            PlateText      = "CMND",
        };

        [JsonProperty("agencies", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, AgencyVehicleDef> Agencies { get; set; } = new()
        {
            ["bcso"] = new AgencyVehicleDef
            {
                Patrol = new VehicleTierDef
                {
                    Models         = new[] { "sheriff", "sheriff2" },
                    PrimaryColor   = -1,
                    SecondaryColor = -1,
                    DirtLevel      = 3,
                    Livery         = -1,
                },
                Senior = new VehicleTierDef
                {
                    Models         = new[] { "sheriff2" },
                    PrimaryColor   = -1,
                    SecondaryColor = -1,
                    DirtLevel      = 1,
                    Livery         = -1,
                },
                Command = new VehicleTierDef
                {
                    Models         = new[] { "sheriff2" },
                    PrimaryColor   = -1,
                    SecondaryColor = -1,
                    DirtLevel      = 0,
                    Livery         = -1,
                    PlateText      = "BCSO",
                },
            },
        };

        public VehicleTierDef TierFor(string? agencyId, LoadoutTier tier)
        {
            var agency = agencyId is null
                ? null
                : Agencies.FirstOrDefault(a => string.Equals(a.Key, agencyId, StringComparison.OrdinalIgnoreCase)).Value;

            return tier switch
            {
                LoadoutTier.Command => agency?.Command ?? Command,
                LoadoutTier.Senior  => agency?.Senior  ?? Senior,
                _                   => agency?.Patrol  ?? Patrol,
            };
        }
    }
}
