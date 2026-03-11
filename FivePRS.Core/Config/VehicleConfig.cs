using System;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    /// <summary>
    /// Describes one vehicle tier (patrol / senior / command) loaded from config/police_vehicles.json.
    /// </summary>
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
        public int[] ForcedExtras { get; set; } = Array.Empty<int>();

        [JsonProperty("disabledExtras")]
        public int[] DisabledExtras { get; set; } = Array.Empty<int>();
    }

    /// <summary>
    /// Loaded from config/police_vehicles.json.
    /// Defines the three vehicle tiers for the Police department.
    /// </summary>
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
    }
}
