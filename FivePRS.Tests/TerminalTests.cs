using System.Threading.Tasks;
using FivePRS.Core.Config;
using Xunit;

namespace FivePRS.Tests
{
    public class TerminalSettingsTests
    {
        private readonly TerminalSettings _settings = new();

        [Theory]
        [InlineData("", true, "")]
        [InlineData("id:HEXGRID", true, "id:hexgrid")]
        [InlineData("id:missing", false, "")]
        [InlineData("url:https://example.com/wall.png", true, "url:https://example.com/wall.png")]
        [InlineData("url:http://example.com/wall.png", false, "")]
        [InlineData("url:https://example.com/a b.png", false, "")]
        [InlineData("url:https://example.com/x.png\");}", false, "")]
        [InlineData("https://example.com/wall.png", false, "")]
        public void TryNormalizeWallpaper_AcceptsKnownIdsAndSafeHttpsUrls(string value, bool valid, string expected)
        {
            Assert.Equal(valid, _settings.TryNormalizeWallpaper(value, out var normalized));
            if (valid) Assert.Equal(expected, normalized);
        }

        [Fact]
        public void CustomUrls_CanBeTurnedOff()
        {
            _settings.AllowCustomWallpapers = false;

            Assert.False(_settings.TryNormalizeWallpaper("url:https://example.com/wall.png", out _));
            Assert.Equal(_settings.Find("fiveprs")!.Url, _settings.ResolveWallpaperUrl("url:https://example.com/wall.png"));
        }

        [Fact]
        public void ResolveWallpaperUrl_FallsBackToDefault()
        {
            Assert.Equal(_settings.Find("dusk")!.Url, _settings.ResolveWallpaperUrl("id:dusk"));
            Assert.Equal("https://example.com/w.png", _settings.ResolveWallpaperUrl("url:https://example.com/w.png"));
            Assert.Equal(_settings.Find("fiveprs")!.Url, _settings.ResolveWallpaperUrl("id:gone"));
            Assert.Equal(_settings.Find("fiveprs")!.Url, _settings.ResolveWallpaperUrl(null));
        }
    }

    [Collection(TestDatabase.Collection)]
    public sealed class PreferencesStoreTests : IAsyncLifetime
    {
        private TestDatabase _database = null!;

        public async Task InitializeAsync() => _database = await TestDatabase.CreateAsync();

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Wallpaper_DefaultsToEmptyAndOverwrites()
        {
            Assert.Equal(string.Empty, await _database.Preferences.GetWallpaperAsync("license:a"));

            await _database.Preferences.SetWallpaperAsync("license:a", "id:dusk");
            await _database.Preferences.SetWallpaperAsync("license:a", "id:hexgrid");

            Assert.Equal("id:hexgrid", await _database.Preferences.GetWallpaperAsync("license:a"));
        }
    }
}
