using Newtonsoft.Json;

namespace FivePRS.Core.Config
{
    public sealed class RateLimit
    {
        public RateLimit()
        {
        }

        public RateLimit(int limit, int perSeconds)
        {
            Limit = limit;
            PerSeconds = perSeconds;
        }

        [JsonProperty("limit")]
        public int Limit { get; set; }

        [JsonProperty("perSeconds")]
        public int PerSeconds { get; set; }
    }

    public sealed class RateLimitSettings
    {
        [JsonProperty("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonProperty("emergency")]
        public RateLimit Emergency { get; set; } = new(4, 60);

        [JsonProperty("lookups")]
        public RateLimit Lookups { get; set; } = new(20, 10);

        [JsonProperty("records")]
        public RateLimit Records { get; set; } = new(10, 10);

        [JsonProperty("civilian")]
        public RateLimit Civilian { get; set; } = new(12, 10);

        [JsonProperty("dispatch")]
        public RateLimit Dispatch { get; set; } = new(20, 10);

        [JsonProperty("admin")]
        public RateLimit Admin { get; set; } = new(15, 10);

        [JsonProperty("positions")]
        public RateLimit Positions { get; set; } = new(6, 10);

        [JsonProperty("requests")]
        public RateLimit Requests { get; set; } = new(10, 10);
    }
}
