using System;
using System.Linq;

namespace FivePRS.Core.Updates
{
    public sealed class ReleaseVersion : IComparable<ReleaseVersion>
    {
        public int Major { get; }
        public int Minor { get; }
        public int Patch { get; }
        public string PreRelease { get; }

        private ReleaseVersion(int major, int minor, int patch, string preRelease)
        {
            Major      = major;
            Minor      = minor;
            Patch      = patch;
            PreRelease = preRelease;
        }

        public static bool TryParse(string? text, out ReleaseVersion version)
        {
            version = new ReleaseVersion(0, 0, 0, string.Empty);

            var value = text?.Trim() ?? string.Empty;
            if (value.StartsWith("v", StringComparison.OrdinalIgnoreCase)) value = value.Substring(1);

            var build = value.IndexOf('+');
            if (build >= 0) value = value.Substring(0, build);

            var dash = value.IndexOf('-');
            var preRelease = dash >= 0 ? value.Substring(dash + 1) : string.Empty;
            var core = (dash >= 0 ? value.Substring(0, dash) : value).Split('.');

            if (core.Length is < 1 or > 3 || core.Any(part => part.Length == 0 || !part.All(char.IsDigit))) return false;

            var numbers = core.Select(int.Parse).Concat(Enumerable.Repeat(0, 3)).Take(3).ToArray();
            version = new ReleaseVersion(numbers[0], numbers[1], numbers[2], preRelease);
            return true;
        }

        public int CompareTo(ReleaseVersion? other)
        {
            if (other is null) return 1;

            var core = Major != other.Major ? Major.CompareTo(other.Major)
                : Minor != other.Minor ? Minor.CompareTo(other.Minor)
                : Patch.CompareTo(other.Patch);
            if (core != 0) return core;

            if (PreRelease.Length == 0) return other.PreRelease.Length == 0 ? 0 : 1;
            if (other.PreRelease.Length == 0) return -1;
            return string.CompareOrdinal(PreRelease, other.PreRelease);
        }

        public bool IsNewerThan(ReleaseVersion other) => CompareTo(other) > 0;

        public override string ToString() =>
            PreRelease.Length == 0 ? $"{Major}.{Minor}.{Patch}" : $"{Major}.{Minor}.{Patch}-{PreRelease}";
    }
}
