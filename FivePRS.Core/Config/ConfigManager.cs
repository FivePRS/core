using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    /// <summary>
    /// Static configuration store. Populated once at resource startup by ClientBrain
    /// (and ServerBrain for server-side convars) before any agency code runs.
    ///
    /// All properties return safe defaults if the corresponding config file is missing
    /// or contains invalid JSON — the resource always starts successfully.
    /// </summary>
    public static class ConfigManager
    {
        public static ResourceSettings    Settings      { get; private set; } = new();
        public static PoliceVehiclesConfig PoliceVehicles { get; private set; } = new();
        public static PoliceLoadoutsConfig PoliceLoadouts { get; private set; } = new();

        public static void LoadSettings(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                Settings = JsonConvert.DeserializeObject<ResourceSettings>(json) ?? new();
                Debug.WriteLine("[FivePRS:Config] settings.json loaded.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS:Config] settings.json parse error — using defaults. ({ex.Message})");
            }
        }

        public static void LoadPoliceVehicles(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                PoliceVehicles = JsonConvert.DeserializeObject<PoliceVehiclesConfig>(json) ?? new();
                Debug.WriteLine("[FivePRS:Config] police_vehicles.json loaded.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS:Config] police_vehicles.json parse error — using defaults. ({ex.Message})");
            }
        }

        public static void LoadPoliceLoadouts(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                PoliceLoadouts = JsonConvert.DeserializeObject<PoliceLoadoutsConfig>(json) ?? new();
                Debug.WriteLine("[FivePRS:Config] police_loadouts.json loaded.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FivePRS:Config] police_loadouts.json parse error — using defaults. ({ex.Message})");
            }
        }
    }
}
