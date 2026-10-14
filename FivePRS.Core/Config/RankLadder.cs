using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Core.Models;

namespace FivePRS.Core.Config
{
    public sealed class RankLadder
    {
        public const int MaxRanks = 255;

        private readonly List<RankDef> _ranks;
        private readonly Dictionary<string, List<string>> _agencies;

        public RankLadder(DepartmentRanksConfig config)
        {
            _ranks = Valid(config).ToList();
            _agencies = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var agency in config.Agencies.Where(a => a.Value is not null))
                _agencies[agency.Key] = agency.Value;
        }

        public int Count => _ranks.Count;

        public static IEnumerable<RankDef> Valid(DepartmentRanksConfig config) =>
            config.Ranks
                .Where(rank => rank is not null && !string.IsNullOrWhiteSpace(rank.Name) && rank.Xp >= 0)
                .OrderBy(rank => rank.Xp)
                .Take(MaxRanks);

        public int Clamp(int rank) => Math.Max(1, Math.Min(Count, rank));

        public string NameFor(int rank, string? agencyId = null)
        {
            var index = Clamp(rank) - 1;
            if (agencyId is not null && _agencies.TryGetValue(agencyId, out var names) &&
                index < names.Count && !string.IsNullOrWhiteSpace(names[index]))
            {
                return names[index].Trim();
            }

            return _ranks[index].Name.Trim();
        }

        public LoadoutTier TierFor(int rank) => _ranks[Clamp(rank) - 1].Tier;

        public int XpToNext(int rank)
        {
            var index = Clamp(rank) - 1;
            return index + 1 >= Count ? 0 : Math.Max(1, _ranks[index + 1].Xp - _ranks[index].Xp);
        }

        public bool IsTop(int rank) => Clamp(rank) >= Count;

        public int MinRankFor(LoadoutTier tier)
        {
            for (var i = 0; i < _ranks.Count; i++)
            {
                if (_ranks[i].Tier >= tier) return i + 1;
            }

            return Count + 1;
        }

        public bool AddXp(PlayerData player, int amount)
        {
            player.Rank = Clamp(player.Rank);
            player.XP   = (int)Math.Min(int.MaxValue, (long)player.XP + Math.Max(0, amount));

            var rankedUp = false;
            while (!IsTop(player.Rank) && player.XP >= XpToNext(player.Rank))
            {
                player.XP -= XpToNext(player.Rank);
                player.Rank++;
                rankedUp = true;
            }

            return rankedUp;
        }
    }
}
