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
        public const int MaxPhotoDataLength = 120_000;
        public const int MaxPhotoUrlLength = 500;
        public const string PhotoDataPrefix = "data:image/jpeg;base64,";

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

        public static string StateId(string abbreviation, int characterId) =>
            $"{abbreviation}-{10000 + characterId}";

        public static string LicenseNumber(string type, int characterId)
        {
            var hash = 0;
            foreach (var c in type.ToLowerInvariant())
                hash = (hash * 31 + c) % 9973;

            var prefix = type.Length > 0 ? char.ToUpperInvariant(type[0]) : 'L';
            return $"{prefix}{(characterId * 7919L + hash * 104729L) % 10000000:0000000}";
        }

        public static bool TryNormalizePhoto(string? input, out string photo)
        {
            photo = (input ?? string.Empty).Trim();
            if (photo.Length == 0) return true;

            if (photo.StartsWith(PhotoDataPrefix, StringComparison.Ordinal))
            {
                if (photo.Length > MaxPhotoDataLength) return false;

                for (var i = PhotoDataPrefix.Length; i < photo.Length; i++)
                {
                    var c = photo[i];
                    var valid = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '+' || c == '/' || c == '=';
                    if (!valid) return false;
                }

                return true;
            }

            return photo.Length <= MaxPhotoUrlLength &&
                   Uri.TryCreate(photo, UriKind.Absolute, out var uri) &&
                   uri.Scheme == "https" &&
                   photo.IndexOfAny(new[] { '"', '\'', '<', '>', '(', ')', ' ', '\\' }) < 0;
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
