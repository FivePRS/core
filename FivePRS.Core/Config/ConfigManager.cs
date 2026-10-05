using System;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public static class ConfigManager
    {
        public static ResourceSettings     Settings       { get; private set; } = new();
        public static PoliceVehiclesConfig PoliceVehicles { get; private set; } = new();
        public static PoliceLoadoutsConfig PoliceLoadouts { get; private set; } = new();

        public static Action<string>? Log { get; set; }

        public static void LoadSettings(string? json)       => Settings       = Load(json, "settings.json",        Settings);
        public static void LoadPoliceVehicles(string? json) => PoliceVehicles = Load(json, "police_vehicles.json", PoliceVehicles);
        public static void LoadPoliceLoadouts(string? json) => PoliceLoadouts = Load(json, "police_loadouts.json", PoliceLoadouts);

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
