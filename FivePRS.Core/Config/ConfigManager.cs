using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Core.Jurisdiction;
using FivePRS.Core.Logging;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public static class ConfigManager
    {
        private static readonly Logger Log = Logger.For("Config");

        public static ResourceSettings     Settings       { get; private set; } = new();
        public static JurisdictionConfig   Jurisdictions  { get; private set; } = new();
        public static LicensesConfig       Licenses       { get; private set; } = new();
        public static StationsConfig       Stations       { get; private set; } = new();
        public static CitationsConfig      Citations      { get; private set; } = new();
        public static TerritoryMap         Territories    { get; private set; } = new(new JurisdictionConfig());

        public static readonly Department[] Departments = { Department.Police, Department.Fire, Department.EMS };

        private static readonly Dictionary<Department, DepartmentLoadoutsConfig> DepartmentLoadouts = new();
        private static readonly Dictionary<Department, DepartmentVehiclesConfig> DepartmentVehicles = new();
        private static readonly Dictionary<Department, RankLadder> DepartmentRanks = new();

        public static string ConfigName(Department department) => department.ToString().ToLowerInvariant();

        public static DepartmentLoadoutsConfig LoadoutsFor(Department department) =>
            DepartmentLoadouts.TryGetValue(department, out var config) ? config : DefaultLoadouts(department);

        public static DepartmentVehiclesConfig VehiclesFor(Department department) =>
            DepartmentVehicles.TryGetValue(department, out var config) ? config : DefaultVehicles(department);

        public static RankLadder RanksFor(Department department)
        {
            if (department == Department.None) department = Department.Police;
            if (DepartmentRanks.TryGetValue(department, out var ladder)) return ladder;

            ladder = new RankLadder(DefaultRanks(department));
            DepartmentRanks[department] = ladder;
            return ladder;
        }

        public static void LoadAll(Func<string, string?> readFile)
        {
            string? Read(string path)
            {
                var json = readFile(path);
                if (!string.IsNullOrWhiteSpace(json)) return json;

                var legacy = ConfigFiles.LegacyPathFor(path);
                return legacy is null ? json : readFile(legacy);
            }

            LoadSettings(Read(ConfigFiles.Settings));
            foreach (var department in Departments)
            {
                LoadDepartment(department,
                    Read(ConfigFiles.Department(department, ConfigFiles.Ranks)),
                    Read(ConfigFiles.Department(department, ConfigFiles.Loadouts)),
                    Read(ConfigFiles.Department(department, ConfigFiles.Vehicles)));
            }

            LoadJurisdictions(Read(ConfigFiles.Jurisdictions));
            LoadLicenses(Read(ConfigFiles.Licenses));
            LoadStations(Read(ConfigFiles.Stations));
            LoadCitations(Read(ConfigFiles.Citations));
        }

        public static void LoadDepartment(Department department, string? ranksJson, string? loadoutsJson, string? vehiclesJson)
        {
            var name = ConfigName(department);
            DepartmentLoadouts[department] = Populate(loadoutsJson, $"{name}/loadouts.json", () => DefaultLoadouts(department));
            DepartmentVehicles[department] = Populate(vehiclesJson, $"{name}/vehicles.json", () => DefaultVehicles(department));

            var ranks = Populate(ranksJson, $"{name}/ranks.json", () => DefaultRanks(department));
            if (!RankLadder.Valid(ranks).Any())
            {
                Log.Warn($"{name}/ranks.json has no valid ranks, using defaults.");
                ranks = DefaultRanks(department);
            }

            DepartmentRanks[department] = new RankLadder(ranks);
        }

        public static DepartmentRanksConfig DefaultRanks(Department department) => department switch
        {
            Department.Fire => DepartmentRanksConfig.Fire(),
            Department.EMS  => DepartmentRanksConfig.Ems(),
            _               => DepartmentRanksConfig.Police(),
        };

        private static DepartmentLoadoutsConfig DefaultLoadouts(Department department) => department switch
        {
            Department.Fire => DepartmentLoadoutsConfig.Fire(),
            Department.EMS  => DepartmentLoadoutsConfig.Ems(),
            _               => new DepartmentLoadoutsConfig(),
        };

        private static DepartmentVehiclesConfig DefaultVehicles(Department department) => department switch
        {
            Department.Fire => DepartmentVehiclesConfig.Fire(),
            Department.EMS  => DepartmentVehiclesConfig.Ems(),
            _               => new DepartmentVehiclesConfig(),
        };

        private static T Populate<T>(string? json, string fileName, Func<T> defaults) where T : class
        {
            if (json is null || string.IsNullOrWhiteSpace(json))
            {
                Log.Warn($"{fileName} is missing or empty, using defaults.");
                return defaults();
            }

            try
            {
                var config = defaults();
                JsonConvert.PopulateObject(json, config);
                Log.Debug($"{fileName} loaded.");
                return config;
            }
            catch (Exception ex)
            {
                Log.Error($"{fileName} has an error, using defaults", ex);
                return defaults();
            }
        }

        public static void LoadSettings(string? json)       => Settings       = Load(json, "settings.json",        Settings);
        public static void LoadLicenses(string? json)       => Licenses       = Load(json, "licenses.json",        Licenses);
        public static void LoadStations(string? json)       => Stations       = Load(json, "stations.json",        Stations);
        public static void LoadCitations(string? json)      => Citations      = Load(json, "citations.json",       Citations);

        public static void LoadJurisdictions(string? json)
        {
            Jurisdictions = Load(json, "jurisdictions.json", Jurisdictions);
            Territories   = new TerritoryMap(Jurisdictions);
        }

        private static T Load<T>(string? json, string fileName, T fallback) where T : class
        {
            if (json is null || string.IsNullOrWhiteSpace(json))
            {
                Log.Warn($"{fileName} is missing or empty, using defaults.");
                return fallback;
            }

            try
            {
                var result = JsonConvert.DeserializeObject<T>(json) ?? fallback;
                Log.Debug($"{fileName} loaded.");
                return result;
            }
            catch (Exception ex)
            {
                Log.Error($"{fileName} has an error, using defaults", ex);
                return fallback;
            }
        }
    }
}
