using System;
using FivePRS.Core.Config;
using FivePRS.Server.Security;
using Newtonsoft.Json;
using Xunit;

namespace FivePRS.Tests
{
    public sealed class RateLimiterTests
    {
        private static readonly RateLimit ThreePerTen = new(3, 10);

        private DateTime _now = new(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc);
        private bool _enabled = true;
        private readonly RateLimiter _limiter;

        public RateLimiterTests()
        {
            _limiter = new RateLimiter(() => _now, () => _enabled);
        }

        [Fact]
        public void Check_UnderLimit_Allows()
        {
            for (var i = 0; i < 3; i++)
                Assert.Equal(RateLimitResult.Allowed, _limiter.Check("1", "records", ThreePerTen));
        }

        [Fact]
        public void Check_OverLimit_ReportsFirstThenLimits()
        {
            for (var i = 0; i < 3; i++) _limiter.Check("1", "records", ThreePerTen);

            Assert.Equal(RateLimitResult.LimitedFirst, _limiter.Check("1", "records", ThreePerTen));
            Assert.Equal(RateLimitResult.Limited, _limiter.Check("1", "records", ThreePerTen));
        }

        [Fact]
        public void Check_AfterWindow_AllowsAgain()
        {
            for (var i = 0; i < 4; i++) _limiter.Check("1", "records", ThreePerTen);

            _now = _now.AddSeconds(10);

            Assert.Equal(RateLimitResult.Allowed, _limiter.Check("1", "records", ThreePerTen));
        }

        [Fact]
        public void Check_PlayersAndBucketsAreSeparate()
        {
            for (var i = 0; i < 3; i++) _limiter.Check("1", "records", ThreePerTen);

            Assert.Equal(RateLimitResult.Allowed, _limiter.Check("2", "records", ThreePerTen));
            Assert.Equal(RateLimitResult.Allowed, _limiter.Check("1", "lookups", ThreePerTen));
        }

        [Fact]
        public void Forget_ResetsPlayer()
        {
            for (var i = 0; i < 4; i++) _limiter.Check("1", "records", ThreePerTen);

            _limiter.Forget("1");

            Assert.Equal(RateLimitResult.Allowed, _limiter.Check("1", "records", ThreePerTen));
        }

        [Fact]
        public void Check_DisabledOrZeroLimit_AlwaysAllows()
        {
            for (var i = 0; i < 10; i++)
                Assert.Equal(RateLimitResult.Allowed, _limiter.Check("1", "zero", new RateLimit(0, 10)));

            _enabled = false;
            for (var i = 0; i < 10; i++)
                Assert.Equal(RateLimitResult.Allowed, _limiter.Check("1", "records", ThreePerTen));
        }

        [Fact]
        public void Settings_PartialOverride_KeepsOtherDefaults()
        {
            var settings = JsonConvert.DeserializeObject<ResourceSettings>(
                "{\"rateLimits\":{\"records\":{\"limit\":2}}}")!;

            Assert.Equal(2, settings.RateLimits.Records.Limit);
            Assert.Equal(10, settings.RateLimits.Records.PerSeconds);
            Assert.Equal(4, settings.RateLimits.Emergency.Limit);
            Assert.True(settings.RateLimits.Enabled);
        }
    }
}
