using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FivePRS.Core.Config
{
    public enum StationBlipMode
    {
        Always,
        OnDuty,
        Off
    }

    public enum StationPoint
    {
        Duty,
        Armory,
        Locker,
        Garage
    }

    public sealed class StationBlipDef
    {
        [JsonProperty("sprite")]
        public int Sprite { get; set; }

        [JsonProperty("color")]
        public int Color { get; set; }
    }

    public sealed class StationDef
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("department")]
        [JsonConverter(typeof(StringEnumConverter))]
        public Department Department { get; set; }

        [JsonProperty("agencies", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<string> Agencies { get; set; } = new();

        [JsonProperty("dutyPoint", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public float[] DutyPoint { get; set; } = new float[0];

        [JsonProperty("heading")]
        public float Heading { get; set; }

        [JsonProperty("parking", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<float[]> Parking { get; set; } = new();

        [JsonProperty("armory", NullValueHandling = NullValueHandling.Ignore)]
        public float[]? Armory { get; set; }

        [JsonProperty("locker", NullValueHandling = NullValueHandling.Ignore)]
        public float[]? Locker { get; set; }

        [JsonProperty("garage", NullValueHandling = NullValueHandling.Ignore)]
        public float[]? Garage { get; set; }

        [JsonProperty("blip", NullValueHandling = NullValueHandling.Ignore)]
        public StationBlipDef? Blip { get; set; }

        public bool IsValid => DutyPoint.Length >= 3 && Department != Department.None;

        public float[]? PointFor(StationPoint point)
        {
            var position = point switch
            {
                StationPoint.Armory => Armory,
                StationPoint.Locker => Locker,
                StationPoint.Garage => Garage,
                _                   => DutyPoint,
            };

            return position is { Length: >= 3 } ? position : null;
        }

        public bool Serves(Department department, string? agencyId) =>
            Department == department &&
            (Agencies.Count == 0 || agencyId is null || agencyId.Length == 0 ||
             Agencies.Any(a => string.Equals(a, agencyId, StringComparison.OrdinalIgnoreCase)));
    }

    public sealed class StationsConfig
    {
        [JsonProperty("blips")]
        [JsonConverter(typeof(StringEnumConverter))]
        public StationBlipMode Blips { get; set; } = StationBlipMode.Always;

        [JsonProperty("stations", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<StationDef> Stations { get; set; } = new();

        public IEnumerable<StationDef> Valid => Stations.Where(s => s.IsValid);

        public StationDef? Find(string? id) =>
            id is null ? null : Valid.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<StationDef> For(Department department, string? agencyId) =>
            Valid.Where(s => s.Serves(department, agencyId));

        public static StationBlipDef DefaultBlip(Department department) => department switch
        {
            Department.Fire => new StationBlipDef { Sprite = 436, Color = 1 },
            Department.EMS  => new StationBlipDef { Sprite = 61,  Color = 2 },
            _               => new StationBlipDef { Sprite = 60,  Color = 3 },
        };
    }
}
