using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FivePRS.Core.Civilian
{
    public static class ReportRules
    {
        public const int MaxTitleLength = 60;
        public const int MaxLocationLength = 80;
        public const int MaxNarrativeLength = 2000;
        public const int MaxSearchLength = 60;
        public const int AutoFileMinutes = 30;

        public static bool TryNormalizeNarrative(string? input, out string narrative)
        {
            narrative = Clean(input, true);
            return narrative.Length > 0 && narrative.Length <= MaxNarrativeLength;
        }

        public static string NormalizeLine(string? input, int max, string fallback)
        {
            var text = Clean(input, false);
            if (text.Length == 0) return fallback;
            return text.Length > max ? text.Substring(0, max).TrimEnd() : text;
        }

        public static bool TryParseDate(string? input, out DateTime? date)
        {
            date = null;
            var text = (input ?? string.Empty).Trim();
            if (text.Length == 0) return true;

            if (!DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return false;
            date = parsed;
            return true;
        }

        public static string NormalizeSearch(string? input) => NormalizeLine(input, MaxSearchLength, string.Empty);

        public static string MedicalSummary(ReportInfo report)
        {
            var text = $"{report.Title} at {report.Location}. {report.Narrative}";
            return text.Length > CivilianRules.MaxDescriptionLength
                ? text.Substring(0, CivilianRules.MaxDescriptionLength - 1).TrimEnd() + "…"
                : text;
        }

        private static string Clean(string? input, bool multiline)
        {
            var builder = new StringBuilder();
            foreach (var c in (input ?? string.Empty).Replace("\r\n", "\n"))
            {
                if (c == '\n' && multiline) builder.Append(c);
                else if (char.IsControl(c)) builder.Append(' ');
                else builder.Append(c);
            }

            var lines = builder.ToString().Split('\n').Select(line => string.Join(" ", line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)));
            return string.Join("\n", lines).Trim();
        }
    }
}
