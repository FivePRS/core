namespace FivePRS.Core.Models
{
    public static class Callsign
    {
        public const int MaxLength = 12;

        public static bool TryNormalize(string? input, out string callsign)
        {
            callsign = (input ?? string.Empty).Trim().ToUpperInvariant();
            if (callsign.Length > MaxLength) return false;

            foreach (var c in callsign)
            {
                var valid = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '-';
                if (!valid) return false;
            }

            return true;
        }
    }
}
