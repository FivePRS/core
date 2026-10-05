using System;
using System.IO;
using FivePRS.Core.Config;
using Newtonsoft.Json;
using Xunit;

namespace FivePRS.Tests
{
    public class LoadoutConfigTests
    {
        [Theory]
        [InlineData(1, LoadoutTier.Recruit)]
        [InlineData(3, LoadoutTier.Officer)]
        [InlineData(5, LoadoutTier.Senior)]
        [InlineData(8, LoadoutTier.Command)]
        [InlineData(20, LoadoutTier.Command)]
        public void TierForRank_MapsRankBands(int rank, LoadoutTier expected)
        {
            Assert.Equal(expected, PoliceLoadoutsConfig.TierForRank(rank));
        }

        [Fact]
        public void Vehicles_Bcso_UsesSheriffModels()
        {
            var config = new PoliceVehiclesConfig();

            Assert.Equal(new[] { "sheriff", "sheriff2" }, config.TierFor("BCSO", LoadoutTier.Recruit).Models);
            Assert.Equal(new[] { "police", "police2" }, config.TierFor("lspd", LoadoutTier.Recruit).Models);
            Assert.Equal(new[] { "police3" }, config.TierFor(null, LoadoutTier.Command).Models);
        }

        [Fact]
        public void Vehicles_AgencyMissingTier_FallsBackToDefault()
        {
            var config = new PoliceVehiclesConfig();
            config.Agencies["sasp"] = new AgencyVehicleDef { Patrol = new VehicleTierDef { Models = new[] { "police4" } } };

            Assert.Equal(new[] { "police4" }, config.TierFor("sasp", LoadoutTier.Officer).Models);
            Assert.Equal(new[] { "police3" }, config.TierFor("sasp", LoadoutTier.Command).Models);
        }

        [Fact]
        public void Loadouts_AgencyOverride_ReplacesOnlyGivenParts()
        {
            var config = new PoliceLoadoutsConfig();
            var sheriffUniform = new UniformDef
            {
                PedModels = new PedModelsDef { Male = "s_m_y_sheriff_01", Female = "s_f_y_sheriff_01" },
            };
            config.Agencies["bcso"] = new AgencyLoadoutDef { Uniform = sheriffUniform };

            Assert.Same(sheriffUniform, config.UniformFor("bcso", LoadoutTier.Recruit));
            Assert.Same(sheriffUniform, config.UniformFor("bcso", LoadoutTier.Command));
            Assert.Same(config.Officer, config.WeaponsFor("bcso", LoadoutTier.Officer));
            Assert.Same(config.CommandUniform, config.UniformFor("lspd", LoadoutTier.Command));
        }

        [Theory]
        [InlineData("police_loadouts.json", typeof(PoliceLoadoutsConfig))]
        [InlineData("police_vehicles.json", typeof(PoliceVehiclesConfig))]
        [InlineData("settings.json", typeof(ResourceSettings))]
        public void BundledConfigFile_MatchesBuiltInDefaults(string file, Type type)
        {
            var json     = File.ReadAllText(Path.Combine(RepoRoot(), "config", file));
            var fromFile = JsonConvert.DeserializeObject(json, type);

            Assert.Equal(JsonConvert.SerializeObject(Activator.CreateInstance(type)), JsonConvert.SerializeObject(fromFile));
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
