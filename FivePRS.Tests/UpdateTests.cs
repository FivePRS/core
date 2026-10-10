using FivePRS.Core.Updates;
using Xunit;

namespace FivePRS.Tests
{
    public class ReleaseVersionTests
    {
        [Theory]
        [InlineData("1.0.0", "1.0.1", -1)]
        [InlineData("v1.2.0", "1.10.0", -1)]
        [InlineData("2.0.0", "1.99.99", 1)]
        [InlineData("1.0.0-beta.1", "1.0.0", -1)]
        [InlineData("1.0.0-beta.2", "1.0.0-beta.1", 1)]
        [InlineData("1.0", "1.0.0", 0)]
        [InlineData("v1.0.0+build.7", "1.0.0", 0)]
        public void CompareTo_OrdersSemanticVersions(string left, string right, int expected)
        {
            Assert.True(ReleaseVersion.TryParse(left, out var a));
            Assert.True(ReleaseVersion.TryParse(right, out var b));
            Assert.Equal(expected, System.Math.Sign(a.CompareTo(b)));
        }

        [Theory]
        [InlineData("")]
        [InlineData("latest")]
        [InlineData("1..0")]
        [InlineData("1.0.0.0")]
        public void TryParse_RejectsInvalidVersions(string text)
        {
            Assert.False(ReleaseVersion.TryParse(text, out _));
        }
    }

    public class ReleaseFeedTests
    {
        private const string Json = @"[
            { ""tag_name"": ""v1.3.0-beta.1"", ""name"": ""1.3 beta"", ""html_url"": ""https://example.com/b"", ""body"": """", ""draft"": false, ""prerelease"": true, ""assets"": [] },
            { ""tag_name"": ""v1.4.0"", ""name"": ""Draft"", ""html_url"": ""https://example.com/d"", ""body"": """", ""draft"": true, ""prerelease"": false, ""assets"": [] },
            { ""tag_name"": ""v1.2.0"", ""name"": ""FivePRS 1.2"", ""html_url"": ""https://example.com/r"", ""body"": ""## Changes\n- One\n- Two"", ""draft"": false, ""prerelease"": false,
              ""assets"": [ { ""name"": ""fiveprs.zip"", ""browser_download_url"": ""https://example.com/fiveprs.zip"" } ] },
            { ""tag_name"": ""nightly"", ""name"": ""Nightly"", ""html_url"": """", ""body"": """", ""draft"": false, ""prerelease"": false, ""assets"": [] }
        ]";

        [Fact]
        public void Latest_SkipsDraftsAndPreReleasesByDefault()
        {
            var release = ReleaseFeed.Latest(Json, "fiveprs.zip", includePreReleases: false);

            Assert.NotNull(release);
            Assert.Equal("1.2.0", release!.Version.ToString());
            Assert.Equal("https://example.com/fiveprs.zip", release.AssetUrl);
            Assert.Equal("- One\n- Two", ReleaseFeed.Summary(release.Notes));
        }

        [Fact]
        public void Latest_CanIncludePreReleases()
        {
            var release = ReleaseFeed.Latest(Json, "fiveprs.zip", includePreReleases: true);

            Assert.Equal("1.3.0-beta.1", release!.Version.ToString());
            Assert.Null(release.AssetUrl);
        }

        [Theory]
        [InlineData("[]")]
        [InlineData("{\"message\":\"Not Found\"}")]
        [InlineData("not json")]
        public void Latest_ReturnsNullWithoutReleases(string json)
        {
            Assert.Null(ReleaseFeed.Latest(json, "fiveprs.zip", false));
        }
    }
}
