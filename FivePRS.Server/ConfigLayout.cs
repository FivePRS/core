using System;
using System.IO;
using FivePRS.Core.Config;
using FivePRS.Core.Logging;
using Newtonsoft.Json;

namespace FivePRS.Server
{
    public static class ConfigLayout
    {
        private static readonly Logger Log = Logger.For("Config");

        public static void Update(string resourcePath)
        {
            try
            {
                var moved   = MoveLegacyFiles(resourcePath);
                var created = CreateMissingRanks(resourcePath);

                if (moved > 0)
                    Log.Info($"Moved {moved} config files into config/departments, config/world and config/civilian.");
                if (created > 0)
                    Log.Info($"Added ranks.json for {created} departments. Edit it to rename ranks or change promotion XP.");
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                Log.Warn("Could not update the config folder layout", ex);
            }
        }

        public static int MoveLegacyFiles(string resourcePath)
        {
            var moved = 0;
            foreach (var location in ConfigFiles.LegacyLocations())
            {
                var from = Resolve(resourcePath, location.Key);
                if (!File.Exists(from)) continue;

                var to = Resolve(resourcePath, location.Value);
                if (File.Exists(to))
                {
                    Log.Warn($"{location.Key} is ignored because {location.Value} exists. Delete the old file.");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                File.Move(from, to);
                moved++;
            }

            return moved;
        }

        public static int CreateMissingRanks(string resourcePath)
        {
            var created = 0;
            foreach (var department in ConfigManager.Departments)
            {
                var path = Resolve(resourcePath, ConfigFiles.Department(department, ConfigFiles.Ranks));
                if (File.Exists(path)) continue;

                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, Serialize(ConfigManager.DefaultRanks(department)));
                created++;
            }

            return created;
        }

        private static string Serialize(object value)
        {
            using var text   = new StringWriter();
            using var writer = new JsonTextWriter(text) { Formatting = Formatting.Indented, Indentation = 4 };
            JsonSerializer.CreateDefault().Serialize(writer, value);
            return text + Environment.NewLine;
        }

        private static string Resolve(string resourcePath, string relative) =>
            Path.Combine(resourcePath, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
