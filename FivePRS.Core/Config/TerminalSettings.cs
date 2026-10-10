using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class WallpaperOption
    {
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        [JsonProperty("label")]
        public string Label { get; set; } = string.Empty;

        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;
    }

    public sealed class TerminalSettings
    {
        public const int MaxWallpaperUrlLength = 512;

        private const string IdPrefix = "id:";
        private const string UrlPrefix = "url:";
        private const string HttpsScheme = "https://";
        private const string UnsafeUrlCharacters = "\"'<>\\()";

        [JsonProperty("defaultWallpaper")]
        public string DefaultWallpaper { get; set; } = "fiveprs";

        [JsonProperty("allowCustomWallpapers")]
        public bool AllowCustomWallpapers { get; set; } = true;

        [JsonProperty("wallpapers", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<WallpaperOption> Wallpapers { get; set; } = new()
        {
            Wallpaper("fiveprs", "FivePRS"),
            Wallpaper("lightbar", "Lightbar"),
            Wallpaper("hexgrid", "Hex grid"),
            Wallpaper("dusk", "Los Santos dusk"),
        };

        public WallpaperOption? Find(string? id) =>
            Wallpapers.FirstOrDefault(w => string.Equals(w.Id, id, StringComparison.OrdinalIgnoreCase));

        public bool TryNormalizeWallpaper(string? value, out string normalized)
        {
            normalized = string.Empty;
            var trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length == 0) return true;

            if (trimmed.StartsWith(IdPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var option = Find(trimmed.Substring(IdPrefix.Length));
                if (option is null) return false;

                normalized = IdPrefix + option.Id;
                return true;
            }

            if (!AllowCustomWallpapers || !trimmed.StartsWith(UrlPrefix, StringComparison.OrdinalIgnoreCase)) return false;

            var url = trimmed.Substring(UrlPrefix.Length);
            if (!IsSafeImageUrl(url)) return false;

            normalized = UrlPrefix + url;
            return true;
        }

        public string ResolveWallpaperUrl(string? preference)
        {
            if (TryNormalizeWallpaper(preference, out var normalized) && normalized.Length > 0)
            {
                if (normalized.StartsWith(UrlPrefix, StringComparison.Ordinal)) return normalized.Substring(UrlPrefix.Length);

                var chosen = Find(normalized.Substring(IdPrefix.Length));
                if (chosen is not null) return chosen.Url;
            }

            return (Find(DefaultWallpaper) ?? Wallpapers.FirstOrDefault())?.Url ?? string.Empty;
        }

        public static bool IsSafeImageUrl(string url) =>
            url.Length <= MaxWallpaperUrlLength &&
            url.StartsWith(HttpsScheme, StringComparison.OrdinalIgnoreCase) &&
            url.Length > HttpsScheme.Length && char.IsLetterOrDigit(url[HttpsScheme.Length]) &&
            !url.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || UnsafeUrlCharacters.IndexOf(c) >= 0);

        private static WallpaperOption Wallpaper(string id, string label) =>
            new() { Id = id, Label = label, Url = $"https://cdn.fiveprs.org/fiveprs/wallpapers/{id}.png" };
    }
}
