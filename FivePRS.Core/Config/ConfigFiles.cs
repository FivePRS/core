using System.Collections.Generic;
using FivePRS.Core.Models;

namespace FivePRS.Core.Config
{
    public static class ConfigFiles
    {
        public const string Settings      = "config/settings.json";
        public const string Jurisdictions = "config/world/jurisdictions.json";
        public const string Stations      = "config/world/stations.json";
        public const string Licenses      = "config/civilian/licenses.json";
        public const string Citations     = "config/civilian/citations.json";
        public const string Framework     = "config/framework.json";

        public const string Ranks    = "ranks";
        public const string Loadouts = "loadouts";
        public const string Vehicles = "vehicles";

        public static string Department(Department department, string kind) =>
            $"config/departments/{ConfigManager.ConfigName(department)}/{kind}.json";

        public static IEnumerable<KeyValuePair<string, string>> LegacyLocations()
        {
            yield return Pair("config/jurisdictions.json", Jurisdictions);
            yield return Pair("config/stations.json", Stations);
            yield return Pair("config/licenses.json", Licenses);
            yield return Pair("config/citations.json", Citations);

            foreach (var department in ConfigManager.Departments)
            {
                var name = ConfigManager.ConfigName(department);
                yield return Pair($"config/{name}_loadouts.json", Department(department, Loadouts));
                yield return Pair($"config/{name}_vehicles.json", Department(department, Vehicles));
            }
        }

        public static string? LegacyPathFor(string path)
        {
            foreach (var location in LegacyLocations())
            {
                if (location.Value == path) return location.Key;
            }

            return null;
        }

        private static KeyValuePair<string, string> Pair(string legacy, string current) => new(legacy, current);
    }
}
