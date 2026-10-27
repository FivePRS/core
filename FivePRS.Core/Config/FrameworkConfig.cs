using System;
using System.Collections.Generic;
using System.Globalization;
using FivePRS.Core.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace FivePRS.Core.Config
{
    public sealed class FrameworkJob
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public Department Department { get; set; }

        public string? Agency { get; set; }

        public Dictionary<string, int>? Ranks { get; set; }
    }

    public sealed class FrameworkFines
    {
        public bool Collect { get; set; } = true;
        public string Account { get; set; } = "bank";
    }

    public sealed class FrameworkJobMatch
    {
        public string Job { get; set; } = string.Empty;
        public Department Department { get; set; }
        public string? Agency { get; set; }
        public int Rank { get; set; }
    }

    public sealed class FrameworkConfig
    {
        public const string Auto = "auto";
        public const string Standalone = "standalone";

        public static readonly string[] Supported = { "qbox", "qbcore", "esx", "ox", "nd" };

        public string Framework { get; set; } = Auto;

        public FrameworkFines Fines { get; set; } = new();

        public Dictionary<string, FrameworkJob> Jobs { get; set; } = new(StringComparer.OrdinalIgnoreCase)
        {
            ["police"]    = new FrameworkJob { Department = Department.Police, Agency = "lspd" },
            ["lspd"]      = new FrameworkJob { Department = Department.Police, Agency = "lspd" },
            ["sheriff"]   = new FrameworkJob { Department = Department.Police, Agency = "bcso" },
            ["bcso"]      = new FrameworkJob { Department = Department.Police, Agency = "bcso" },
            ["ambulance"] = new FrameworkJob { Department = Department.EMS,    Agency = "sams" },
            ["ems"]       = new FrameworkJob { Department = Department.EMS,    Agency = "sams" },
            ["fire"]      = new FrameworkJob { Department = Department.Fire,   Agency = "lsfd" },
            ["lsfd"]      = new FrameworkJob { Department = Department.Fire,   Agency = "lsfd" },
        };

        public string Preferred => (Framework ?? Auto).Trim().ToLowerInvariant();

        public FrameworkJobMatch? Resolve(string? job, int grade, Func<Department, int> rankCount)
        {
            if (string.IsNullOrWhiteSpace(job) || !TryFindJob(job!, out var name, out var mapping)) return null;
            if (mapping.Department == Department.None) return null;

            var count = Math.Max(1, rankCount(mapping.Department));
            var rank = mapping.Ranks is not null && mapping.Ranks.TryGetValue(grade.ToString(CultureInfo.InvariantCulture), out var mapped)
                ? mapped
                : grade + 1;

            return new FrameworkJobMatch
            {
                Job        = name,
                Department = mapping.Department,
                Agency     = string.IsNullOrWhiteSpace(mapping.Agency) ? null : mapping.Agency,
                Rank       = Math.Min(Math.Max(rank, 1), count),
            };
        }

        private bool TryFindJob(string job, out string name, out FrameworkJob mapping)
        {
            foreach (var entry in Jobs)
            {
                if (!string.Equals(entry.Key, job.Trim(), StringComparison.OrdinalIgnoreCase) || entry.Value is null) continue;
                name = entry.Key;
                mapping = entry.Value;
                return true;
            }

            name = string.Empty;
            mapping = null!;
            return false;
        }
    }
}
