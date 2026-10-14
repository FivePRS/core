using System;
using System.IO;
using FivePRS.Core.Config;
using Newtonsoft.Json;
using Xunit;

namespace FivePRS.Tests
{
    public class LoadoutConfigTests
    {
        [Fact]
        public void Vehicles_Bcso_UsesSheriffModels()
        {
            var config = new DepartmentVehiclesConfig();

            Assert.Equal(new[] { "sheriff", "sheriff2" }, config.TierFor("BCSO", LoadoutTier.Recruit).Models);
            Assert.Same(config.Patrol, config.TierFor("lspd", LoadoutTier.Recruit));
            Assert.Same(config.Command, config.TierFor(null, LoadoutTier.Command));
        }

        [Fact]
        public void Vehicles_AgencyMissingTier_FallsBackToDefault()
        {
            var config = new DepartmentVehiclesConfig();
            config.Agencies["sasp"] = new AgencyVehicleDef { Patrol = new VehicleTierDef { Models = new[] { "police4" } } };

            Assert.Equal(new[] { "police4" }, config.TierFor("sasp", LoadoutTier.Officer).Models);
            Assert.Same(config.Command, config.TierFor("sasp", LoadoutTier.Command));
        }

        [Fact]
        public void Loadouts_AgencyOverride_ReplacesOnlyGivenParts()
        {
            var config = new DepartmentLoadoutsConfig();
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

        [Fact]
        public void Uniform_ConfigWithoutFemale_KeepsBuiltInFemaleOutfit()
        {
            var config = JsonConvert.DeserializeObject<DepartmentLoadoutsConfig>(
                "{ \"uniform\": { \"components\": [ { \"slot\": 11, \"drawable\": 99, \"texture\": 0 } ], \"props\": [] } }")!;

            Assert.Equal(99, Assert.Single(config.Uniform.Components).Drawable);
            Assert.Contains(config.Uniform.Female!.Components, c => c.Slot == 3 && c.Drawable == 14);
        }

        [Fact]
        public void Uniform_FemaleOutfit_IsReadFromConfig()
        {
            var config = JsonConvert.DeserializeObject<DepartmentLoadoutsConfig>(
                "{ \"uniform\": { \"components\": [], \"props\": [], \"female\": { \"components\": [ { \"slot\": 11, \"drawable\": 7, \"texture\": 2 } ], \"props\": [] } } }")!;

            var top = Assert.Single(config.Uniform.Female!.Components);
            Assert.Equal(7, top.Drawable);
            Assert.Equal(2, top.Texture);
        }

        [Fact]
        public void Uniform_AgencyWithoutFemale_HasNoFemaleOutfit()
        {
            var config = JsonConvert.DeserializeObject<DepartmentLoadoutsConfig>(
                "{ \"agencies\": { \"bcso\": { \"uniform\": { \"components\": [], \"props\": [] } } } }")!;

            Assert.Null(config.UniformFor("bcso", LoadoutTier.Officer).Female);
        }

        [Theory]
        [InlineData("departments/police/loadouts.json", typeof(DepartmentLoadoutsConfig))]
        [InlineData("departments/police/vehicles.json", typeof(DepartmentVehiclesConfig))]
        [InlineData("settings.json", typeof(ResourceSettings))]
        public void BundledConfigFile_MatchesBuiltInDefaults(string file, Type type)
        {
            var json     = File.ReadAllText(Path.Combine(RepoRoot(), "config", file));
            var fromFile = JsonConvert.DeserializeObject(json, type);

            Assert.Equal(JsonConvert.SerializeObject(Activator.CreateInstance(type)), JsonConvert.SerializeObject(fromFile));
        }

        [Fact]
        public void Licenses_FileReplacesTheDefaultTypes()
        {
            var config = JsonConvert.DeserializeObject<LicensesConfig>("{ \"types\": [ { \"id\": \"driver\", \"name\": \"Driver\" } ] }")!;

            Assert.Single(config.Types);
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
