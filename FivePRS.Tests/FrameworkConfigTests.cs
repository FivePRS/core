using System.Collections.Generic;
using FivePRS.Core.Config;
using FivePRS.Core.Models;
using Xunit;

namespace FivePRS.Tests
{
    public sealed class FrameworkConfigTests
    {
        private static int FiveRanks(Department _) => 5;

        private static string RepoRoot()
        {
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "FivePRS.sln")))
                dir = dir.Parent;
            return dir!.FullName;
        }

        [Fact]
        public void Resolve_MapsDefaultJobsCaseInsensitively()
        {
            var config = new FrameworkConfig();

            var police = config.Resolve("Police", 0, FiveRanks);
            Assert.Equal(Department.Police, police!.Department);
            Assert.Equal("lspd", police.Agency);
            Assert.Equal(1, police.Rank);

            Assert.Equal("bcso", config.Resolve("sheriff", 2, FiveRanks)!.Agency);
            Assert.Equal(Department.EMS, config.Resolve("ambulance", 1, FiveRanks)!.Department);
            Assert.Null(config.Resolve("mechanic", 0, FiveRanks));
            Assert.Null(config.Resolve(null, 0, FiveRanks));
        }

        [Fact]
        public void Resolve_UsesGradeMapAndClampsToTheLadder()
        {
            var config = new FrameworkConfig
            {
                Jobs = new Dictionary<string, FrameworkJob>
                {
                    ["police"] = new() { Department = Department.Police, Ranks = new Dictionary<string, int> { ["0"] = 1, ["1"] = 3 } },
                },
            };

            Assert.Equal(3, config.Resolve("police", 1, FiveRanks)!.Rank);
            Assert.Equal(5, config.Resolve("police", 9, FiveRanks)!.Rank);
            Assert.Equal(1, config.Resolve("police", 0, FiveRanks)!.Rank);
        }

        [Fact]
        public void LoadFramework_ReplacesTheDefaultJobs()
        {
            ConfigManager.LoadFramework(@"{ ""framework"": ""QBCore"", ""jobs"": { ""lspd_patrol"": { ""department"": ""Police"" } } }");
            try
            {
                Assert.Equal("qbcore", ConfigManager.Framework.Preferred);
                Assert.Single(ConfigManager.Framework.Jobs);
                Assert.NotNull(ConfigManager.Framework.Resolve("LSPD_PATROL", 0, FiveRanks));
                Assert.Null(ConfigManager.Framework.Resolve("police", 0, FiveRanks));
            }
            finally
            {
                ConfigManager.LoadFramework(null);
            }

            Assert.Equal(FrameworkConfig.Auto, ConfigManager.Framework.Preferred);
            Assert.NotNull(ConfigManager.Framework.Resolve("police", 0, FiveRanks));
        }

        [Fact]
        public void ShippedFrameworkFile_Loads()
        {
            var json = System.IO.File.ReadAllText(System.IO.Path.Combine(RepoRoot(), "config", "framework.json"));
            ConfigManager.LoadFramework(json);
            try
            {
                Assert.Equal(FrameworkConfig.Auto, ConfigManager.Framework.Preferred);
                Assert.Equal("sams", ConfigManager.Framework.Resolve("ambulance", 0, FiveRanks)!.Agency);
            }
            finally
            {
                ConfigManager.LoadFramework(null);
            }
        }
    }
}
