using System;
using System.Linq;
using FivePRS.Core.Civilian;
using FivePRS.Core.Config;
using FivePRS.Core.Models;
using Xunit;

namespace FivePRS.Tests
{
    public class IdentityTests
    {
        [Theory]
        [InlineData("")]
        [InlineData("https://cdn.example.com/photo.png")]
        [InlineData("data:image/jpeg;base64,/9j/4AAQSkZJRgABAQ==")]
        public void Photo_AcceptsLinksMugshotsAndRemoval(string photo)
        {
            Assert.True(CivilianRules.TryNormalizePhoto(photo, out _));
        }

        [Theory]
        [InlineData("http://example.com/photo.png")]
        [InlineData("javascript:alert(1)")]
        [InlineData("https://example.com/a\"onerror=\"x.png")]
        [InlineData("data:image/png;base64,AAAA")]
        [InlineData("data:image/jpeg;base64,<script>")]
        public void Photo_RejectsUnsafeValues(string photo)
        {
            Assert.False(CivilianRules.TryNormalizePhoto(photo, out _));
        }

        [Fact]
        public void Photo_RejectsOversizedMugshots()
        {
            var photo = CivilianRules.PhotoDataPrefix + new string('A', CivilianRules.MaxPhotoDataLength);
            Assert.False(CivilianRules.TryNormalizePhoto(photo, out _));
        }

        [Fact]
        public void LicenseNumber_IsStablePerCharacterAndType()
        {
            var first = CivilianRules.LicenseNumber("driver", 3);

            Assert.Equal(first, CivilianRules.LicenseNumber("driver", 3));
            Assert.NotEqual(first, CivilianRules.LicenseNumber("driver", 4));
            Assert.NotEqual(first, CivilianRules.LicenseNumber("weapon", 3));
            Assert.StartsWith("D", first);
            Assert.Equal(8, first.Length);
        }

        [Fact]
        public void StateId_UsesTheStateAbbreviation()
        {
            Assert.Equal("SA-10003", CivilianRules.StateId("SA", 3));
        }

        [Fact]
        public void LicenseViews_FillCardDetailsForHeldLicenses()
        {
            var issued = new DateTime(2026, 10, 13, 0, 0, 0, DateTimeKind.Utc);
            var config = new LicensesConfig();
            var views = LicenseViews.Build(3, config, new[]
            {
                new LicenseInfo { CharacterId = 3, Type = "driver", Status = LicenseStatus.Valid, IssuedAt = issued },
            });

            var driver = views.Single(v => v.Type == "driver");
            Assert.Equal(LicenseStatus.Valid, driver.Status);
            Assert.Equal("C", driver.Class);
            Assert.Equal(issued.AddDays(1825), driver.ExpiresAt);
            Assert.Equal("DRIVER LICENSE", driver.Card.Title);

            var weapon = views.Single(v => v.Type == "weapon");
            Assert.Null(weapon.Status);
            Assert.Null(weapon.ExpiresAt);
        }

        [Fact]
        public void LicenseViews_NoExpiryWhenValidDaysIsZero()
        {
            var config = new LicensesConfig();
            config.Types[0].ValidDays = 0;

            var view = LicenseViews.Build(1, config, new[] { new LicenseInfo { Type = config.Types[0].Id, IssuedAt = DateTime.UtcNow } }).First();

            Assert.Null(view.ExpiresAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not json")]
        [InlineData("{\"textSize\":\"huge\",\"terminalVolume\":400}")]
        public void TerminalPreferences_ParseFallsBackToSafeValues(string? json)
        {
            var preferences = TerminalPreferences.Parse(json);

            Assert.Contains(preferences.TextSize, new[] { "s", "m", "l" });
            Assert.InRange(preferences.TerminalVolume, 0, 100);
        }

        [Fact]
        public void TerminalPreferences_RoundTrip()
        {
            var preferences = TerminalPreferences.Parse("{\"textSize\":\"l\",\"clock24\":true,\"dispatchVolume\":40}");

            Assert.Equal("l", preferences.TextSize);
            Assert.True(preferences.Clock24);
            Assert.Equal(40, preferences.DispatchVolume);
            Assert.Equal(preferences.ToJson(), TerminalPreferences.Parse(preferences.ToJson()).ToJson());
        }
    }
}
