using System;
using System.Collections.Generic;
using FivePRS.Core.Jurisdiction;
using FivePRS.Core.Models;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public static class ConfigManager
    {
        public static ResourceSettings     Settings       { get; private set; } = new();
        public static JurisdictionConfig   Jurisdictions  { get; private set; } = new();
        public static LicensesConfig       Licenses       { get; private set; } = new();
        public static StationsConfig       Stations       { get; private set; } = new();
        public static CitationsConfig      Citations      { get; private set; } = new();
        public static TerritoryMap         Territories    { get; private set; } = new(new JurisdictionConfig());

        public static Action<string>? Log { get; set; }

        public static readonly Department[] Departments = { Department.Police, Department.Fire, Department.EMS };

        private static readonly Dictionary<Department, DepartmentLoadoutsConfig> DepartmentLoadouts = new();
        private static readonly Dictionary<Department, DepartmentVehiclesConfig> DepartmentVehicles = new();

        public static string ConfigName(Department department) => department.ToString().ToLowerInvariant();

        public static DepartmentLoadoutsConfig LoadoutsFor(Department department) =>
            DepartmentLoadouts.TryGetValue(department, out var config) ? config : DefaultLoadouts(department);

        public static DepartmentVehiclesConfig VehiclesFor(Department department) =>
            DepartmentVehicles.TryGetValue(department, out var config) ? config : DefaultVehicles(department);

        public static void LoadDepartment(Department department, string? loadoutsJson, string? vehiclesJson)
        {
            var name = ConfigName(department);
            DepartmentLoadouts[department] = Populate(loadoutsJson, $"{name}_loadouts.json", () => DefaultLoadouts(department));
            DepartmentVehicles[department] = Populate(vehiclesJson, $"{name}_vehicles.json", () => DefaultVehicles(department));
        }

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
                Log?.Invoke($"[FivePRS:Config] {fileName} missing or empty, using defaults.");
                return defaults();
            }

            try
            {
                var config = defaults();
                JsonConvert.PopulateObject(json, config);
                Log?.Invoke($"[FivePRS:Config] {fileName} loaded.");
                return config;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[FivePRS:Config] {fileName} parse error, using defaults. ({ex.Message})");
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
                Log?.Invoke($"[FivePRS:Config] {fileName} missing or empty, using defaults.");
                return fallback;
            }

            try
            {
                var result = JsonConvert.DeserializeObject<T>(json) ?? fallback;
                Log?.Invoke($"[FivePRS:Config] {fileName} loaded.");
                return result;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[FivePRS:Config] {fileName} parse error, using defaults. ({ex.Message})");
                return fallback;
            }
        }
    }
}
