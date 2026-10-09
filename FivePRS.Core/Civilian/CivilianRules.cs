using System;
using System.Globalization;

namespace FivePRS.Core.Civilian
{
    public static class CivilianRules
    {
        public const int MaxNameLength  = 24;
        public const int MaxPlateLength = 8;
        public const int MinimumAge     = 16;
        public const int MaxDescriptionLength = 500;
        public const int MinSearchLength = 2;
        public const int MaxEmergencyLength = 300;

        public static readonly string[] Genders = { "Male", "Female", "Other" };

        public static bool TryNormalizeName(string? input, out string name)
        {
            name = (input ?? string.Empty).Trim();
            if (name.Length == 0 || name.Length > MaxNameLength) return false;

            foreach (var c in name)
            {
                if (!char.IsLetter(c) && c != ' ' && c != '-' && c != '\'') return false;
            }

            name = char.ToUpperInvariant(name[0]) + name.Substring(1);
            return true;
        }

        public static bool TryParseDateOfBirth(string? input, DateTime today, out string dateOfBirth)
        {
            dateOfBirth = string.Empty;
            if (!DateTime.TryParseExact((input ?? string.Empty).Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var date))
            {
                return false;
            }

            if (date.Year < 1900 || date > today.AddYears(-MinimumAge)) return false;

            dateOfBirth = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return true;
        }

        public static bool TryNormalizeGender(string? input, out string gender)
        {
            gender = string.Empty;
            foreach (var option in Genders)
            {
                if (string.Equals(option, (input ?? string.Empty).Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    gender = option;
                    return true;
                }
            }
            return false;
        }

        public static bool TryNormalizeDescription(string? input, out string description)
        {
            description = (input ?? string.Empty).Trim();
            return description.Length > 0 && description.Length <= MaxDescriptionLength;
        }

        public static bool TryNormalizeSearch(string? input, out string term)
        {
            term = (input ?? string.Empty).Trim();
            if (term.Length < MinSearchLength || term.Length > MaxNameLength * 2 + 1) return false;

            foreach (var c in term)
            {
                if (!char.IsLetter(c) && c != ' ' && c != '-' && c != '\'') return false;
            }

            return true;
        }

        public static bool TryNormalizePlate(string? input, out string plate)
        {
            plate = (input ?? string.Empty).Trim().ToUpperInvariant();
            if (plate.Length == 0 || plate.Length > MaxPlateLength) return false;

            foreach (var c in plate)
            {
                var valid = (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ';
                if (!valid) return false;
            }

            return true;
        }
    }
}
