using System;
using System.IO;
using System.Linq;
using FivePRS.Core.Config;
using FivePRS.Core.Models;
using FivePRS.Server;
using Newtonsoft.Json;
using Xunit;

namespace FivePRS.Tests
{
    public class RankLadderTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"fiveprs-config-{Guid.NewGuid():N}");

        private static RankLadder Police() => new(DepartmentRanksConfig.Police());

        [Theory]
        [InlineData(1, LoadoutTier.Recruit)]
        [InlineData(3, LoadoutTier.Officer)]
        [InlineData(5, LoadoutTier.Senior)]
        [InlineData(8, LoadoutTier.Command)]
        [InlineData(20, LoadoutTier.Command)]
        public void DefaultLadder_KeepsTheOriginalTierBands(int rank, LoadoutTier expected)
        {
            Assert.Equal(expected, Police().TierFor(rank));
        }

        [Fact]
        public void DefaultLadder_KeepsTheOriginalXpCurve()
        {
            var ladder = Police();

            for (var rank = 1; rank < ladder.Count; rank++)
                Assert.Equal(rank * rank * 100, ladder.XpToNext(rank));
        }

        [Fact]
        public void AddXp_CrossingSeveralThresholds_RanksUpEachTime()
        {
            var player = new PlayerData { Rank = 1 };

            Assert.True(Police().AddXp(player, 100 + 400 + 50));

            Assert.Equal(3, player.Rank);
            Assert.Equal(50, player.XP);
        }

        [Fact]
        public void AddXp_BelowThreshold_DoesNotRankUp()
        {
            var player = new PlayerData { Rank = 1 };

            Assert.False(Police().AddXp(player, 99));
            Assert.Equal(99, player.XP);
        }

        [Fact]
        public void AddXp_NegativeAmount_IsIgnored()
        {
            var player = new PlayerData { Rank = 1, XP = 10 };

            Police().AddXp(player, -500);

            Assert.Equal(10, player.XP);
        }

        [Fact]
        public void AddXp_StopsAtTheTopRankWithoutOverflowing()
        {
            var ladder = Police();
            var player = new PlayerData { Rank = 1 };

            Assert.True(ladder.AddXp(player, int.MaxValue));
            Assert.False(ladder.AddXp(player, int.MaxValue));

            Assert.Equal(ladder.Count, player.Rank);
            Assert.Equal(0, ladder.XpToNext(player.Rank));
        }

        [Fact]
        public void RanksAboveTheLadder_AreClampedToTheTop()
        {
            var ladder = Police();
            var player = new PlayerData { Rank = 50 };

            ladder.AddXp(player, 1);

            Assert.Equal(ladder.Count, player.Rank);
            Assert.Equal("Chief of Police", ladder.NameFor(50));
        }

        [Fact]
        public void NameFor_UsesAgencyNamesAndFallsBack()
        {
            var config = DepartmentRanksConfig.Police();
            config.Agencies["bcso"] = new() { "Deputy" };
            var ladder = new RankLadder(config);

            Assert.Equal("Deputy", ladder.NameFor(1, "BCSO"));
            Assert.Equal("Probationary Officer", ladder.NameFor(2, "bcso"));
            Assert.Equal("Cadet", ladder.NameFor(1, "lspd"));
        }

        [Fact]
        public void Ladder_SortsByXpAndSkipsInvalidRanks()
        {
            var ladder = new RankLadder(JsonConvert.DeserializeObject<DepartmentRanksConfig>(
                "{ \"ranks\": [ { \"name\": \"Two\", \"xp\": 50, \"tier\": \"senior\" }, { \"name\": \"\", \"xp\": 10 }, " +
                "{ \"name\": \"One\", \"xp\": 0, \"tier\": \"Recruit\" } ] }")!);

            Assert.Equal(2, ladder.Count);
            Assert.Equal("One", ladder.NameFor(1));
            Assert.Equal(LoadoutTier.Senior, ladder.TierFor(2));
            Assert.Equal(50, ladder.XpToNext(1));
        }

        [Fact]
        public void MinRankFor_PointsPastTheLadderWhenNoRankHasTheTier()
        {
            var ladder = new RankLadder(new DepartmentRanksConfig
            {
                Ranks = { new RankDef { Name = "Only", Tier = LoadoutTier.Recruit } },
            });

            Assert.Equal(1, ladder.MinRankFor(LoadoutTier.Recruit));
            Assert.Equal(2, ladder.MinRankFor(LoadoutTier.Command));
        }

        [Theory]
        [InlineData(Department.Police)]
        [InlineData(Department.Fire)]
        [InlineData(Department.EMS)]
        public void BundledRanks_MatchTheBuiltInDefaults(Department department)
        {
            var file = Path.Combine(RepoRoot(), ConfigFiles.Department(department, ConfigFiles.Ranks));
            var fromFile = JsonConvert.DeserializeObject<DepartmentRanksConfig>(File.ReadAllText(file));

            Assert.Equal(JsonConvert.SerializeObject(ConfigManager.DefaultRanks(department)), JsonConvert.SerializeObject(fromFile));
        }

        [Fact]
        public void ConfigLayout_MovesLegacyFilesAndAddsRanks()
        {
            Directory.CreateDirectory(Path.Combine(_directory, "config"));
            File.WriteAllText(Path.Combine(_directory, "config", "police_loadouts.json"), "{}");
            File.WriteAllText(Path.Combine(_directory, "config", "stations.json"), "{}");

            Assert.Equal(2, ConfigLayout.MoveLegacyFiles(_directory));
            Assert.Equal(3, ConfigLayout.CreateMissingRanks(_directory));

            Assert.True(File.Exists(Path.Combine(_directory, "config", "departments", "police", "loadouts.json")));
            Assert.True(File.Exists(Path.Combine(_directory, "config", "world", "stations.json")));
            Assert.False(File.Exists(Path.Combine(_directory, "config", "stations.json")));

            var written = JsonConvert.DeserializeObject<DepartmentRanksConfig>(
                File.ReadAllText(Path.Combine(_directory, "config", "departments", "fire", "ranks.json")))!;
            Assert.Equal("Fire Chief", written.Ranks.Last().Name);
        }

        [Fact]
        public void ConfigLayout_KeepsTheNewFileWhenBothExist()
        {
            Directory.CreateDirectory(Path.Combine(_directory, "config", "world"));
            File.WriteAllText(Path.Combine(_directory, "config", "stations.json"), "old");
            File.WriteAllText(Path.Combine(_directory, "config", "world", "stations.json"), "new");

            Assert.Equal(0, ConfigLayout.MoveLegacyFiles(_directory));
            Assert.Equal("new", File.ReadAllText(Path.Combine(_directory, "config", "world", "stations.json")));
        }

        [Fact]
        public void LoadAll_FallsBackToLegacyLocations()
        {
            var files = new System.Collections.Generic.Dictionary<string, string>
            {
                ["config/police_vehicles.json"] = "{ \"patrol\": { \"models\": [\"legacy\"] } }",
            };

            ConfigManager.LoadAll(path => files.TryGetValue(path, out var json) ? json : null);

            Assert.Equal(new[] { "legacy" }, ConfigManager.VehiclesFor(Department.Police).Patrol.Models);
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FivePRS.sln")))
                dir = dir.Parent;
            return dir!.FullName;
        }
    }
}
