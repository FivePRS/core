using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FivePRS.Core.Updates
{
    public sealed class ReleaseInfo
    {
        public string Tag { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public string? AssetUrl { get; set; }
        public bool PreRelease { get; set; }
        public ReleaseVersion Version { get; set; } = null!;
    }

    public static class ReleaseFeed
    {
        private sealed class GitHubAsset
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;

            [JsonProperty("browser_download_url")]
            public string DownloadUrl { get; set; } = string.Empty;
        }

        private sealed class GitHubRelease
        {
            [JsonProperty("tag_name")]
            public string Tag { get; set; } = string.Empty;

            [JsonProperty("name")]
            public string? Name { get; set; }

            [JsonProperty("html_url")]
            public string Url { get; set; } = string.Empty;

            [JsonProperty("body")]
            public string? Body { get; set; }

            [JsonProperty("draft")]
            public bool Draft { get; set; }

            [JsonProperty("prerelease")]
            public bool PreRelease { get; set; }

            [JsonProperty("assets")]
            public List<GitHubAsset> Assets { get; set; } = new();
        }

        public static string ReleasesUrl(string repository) => $"https://api.github.com/repos/{repository}/releases?per_page=20";

        public static ReleaseInfo? Latest(string json, string assetName, bool includePreReleases)
        {
            List<GitHubRelease>? releases;
            try
            {
                releases = JsonConvert.DeserializeObject<List<GitHubRelease>>(json);
            }
            catch (JsonException)
            {
                return null;
            }

            return (releases ?? new List<GitHubRelease>())
                .Where(release => !release.Draft && (includePreReleases || !release.PreRelease))
                .Select(release => ReleaseVersion.TryParse(release.Tag, out var version)
                    ? new ReleaseInfo
                    {
                        Tag        = release.Tag,
                        Name       = string.IsNullOrWhiteSpace(release.Name) ? release.Tag : release.Name!,
                        Url        = release.Url,
                        Notes      = release.Body ?? string.Empty,
                        PreRelease = release.PreRelease,
                        Version    = version,
                        AssetUrl   = release.Assets
                            .FirstOrDefault(asset => IsAsset(asset.Name, assetName))
                            ?.DownloadUrl,
                    }
                    : null)
                .Where(release => release is not null)
                .OrderByDescending(release => release!.Version)
                .FirstOrDefault();
        }

        public static bool IsAsset(string fileName, string assetName)
        {
            if (string.Equals(fileName, assetName, StringComparison.OrdinalIgnoreCase)) return true;

            var dot = assetName.LastIndexOf('.');
            if (dot <= 0) return false;

            var prefix    = assetName.Substring(0, dot) + "-";
            var extension = assetName.Substring(dot);
            if (fileName.Length <= prefix.Length + extension.Length ||
                !fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
                !fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var version = fileName.Substring(prefix.Length, fileName.Length - prefix.Length - extension.Length);
            return ReleaseVersion.TryParse(version, out _);
        }

        public static string Summary(string notes, int maxLines = 6)
        {
            var lines = notes
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => line.Length > 0 && !line.StartsWith("#", StringComparison.Ordinal))
                .ToList();

            var summary = lines.Take(maxLines).ToList();
            if (lines.Count > maxLines) summary.Add($"…and {lines.Count - maxLines} more");
            return string.Join("\n", summary);
        }
    }
}
