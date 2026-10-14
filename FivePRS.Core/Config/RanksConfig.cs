using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace FivePRS.Core.Config
{
    public sealed class RankDef
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("xp")]
        public int Xp { get; set; }

        [JsonProperty("tier")]
        [JsonConverter(typeof(StringEnumConverter), typeof(CamelCaseNamingStrategy))]
        public LoadoutTier Tier { get; set; }
    }

    public sealed class DepartmentRanksConfig
    {
        private static readonly int[] DefaultXp = { 0, 100, 500, 1400, 3000, 5500, 9100, 14000, 20400, 28500 };

        private static readonly LoadoutTier[] DefaultTiers =
        {
            LoadoutTier.Recruit, LoadoutTier.Recruit, LoadoutTier.Officer, LoadoutTier.Officer, LoadoutTier.Senior,
            LoadoutTier.Senior, LoadoutTier.Senior, LoadoutTier.Command, LoadoutTier.Command, LoadoutTier.Command,
        };

        [JsonProperty("ranks", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<RankDef> Ranks { get; set; } = new();

        [JsonProperty("agencies", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, List<string>> Agencies { get; set; } = new();

        public static DepartmentRanksConfig Police() => Ladder(
            new[] { "Cadet", "Probationary Officer", "Officer", "Senior Officer", "Corporal", "Sergeant", "Staff Sergeant", "Lieutenant", "Captain", "Chief of Police" },
            new Dictionary<string, List<string>>
            {
                ["bcso"] = new() { "Deputy Trainee", "Probationary Deputy", "Deputy", "Senior Deputy", "Corporal", "Sergeant", "Staff Sergeant", "Lieutenant", "Captain", "Sheriff" },
            });

        public static DepartmentRanksConfig Fire() => Ladder(
            new[] { "Recruit Firefighter", "Probationary Firefighter", "Firefighter", "Senior Firefighter", "Engineer", "Lieutenant", "Captain", "Battalion Chief", "Deputy Chief", "Fire Chief" });

        public static DepartmentRanksConfig Ems() => Ladder(
            new[] { "EMT Trainee", "EMT", "Advanced EMT", "Paramedic", "Senior Paramedic", "Field Supervisor", "Shift Supervisor", "Operations Manager", "Deputy Director", "EMS Director" });

        private static DepartmentRanksConfig Ladder(string[] names, Dictionary<string, List<string>>? agencies = null)
        {
            var config = new DepartmentRanksConfig { Agencies = agencies ?? new Dictionary<string, List<string>>() };
            for (var i = 0; i < names.Length; i++)
                config.Ranks.Add(new RankDef { Name = names[i], Xp = DefaultXp[i], Tier = DefaultTiers[i] });
            return config;
        }
    }
}
