using System.Collections.Generic;
using FivePRS.Core.Config;
using FivePRS.Core.Jurisdiction;
using FivePRS.Core.Models;
using Newtonsoft.Json;
using Xunit;

namespace FivePRS.Tests
{
    public class TerritoryMapTests
    {
        private readonly TerritoryMap _map = new(new JurisdictionConfig());

        [Theory]
        [InlineData(25f, -1344f, "los_santos")]
        [InlineData(-3040f, 584f, "los_santos")]
        [InlineData(1960f, 3740f, "blaine_county")]
        [InlineData(-448f, 6012f, "blaine_county")]
        public void Resolve_DefaultConfig_ReturnsExpectedTerritory(float x, float y, string expected)
        {
            Assert.Equal(expected, _map.Resolve(x, y)?.Id);
        }

        [Fact]
        public void Resolve_OutsideEveryPolygon_ReturnsNull()
        {
            Assert.Null(_map.Resolve(9000f, 9000f));
        }

        [Fact]
        public void Contains_ConcavePolygon_HandlesNotch()
        {
            var lShape = new List<float[]>
            {
                new[] { 0f, 0f }, new[] { 10f, 0f }, new[] { 10f, 5f },
                new[] { 5f, 5f }, new[] { 5f, 10f }, new[] { 0f, 10f },
            };

            Assert.True(TerritoryMap.Contains(lShape, 2f, 8f));
            Assert.True(TerritoryMap.Contains(lShape, 8f, 2f));
            Assert.False(TerritoryMap.Contains(lShape, 8f, 8f));
        }

        [Fact]
        public void ResolveAgency_UsesRequestedAgencyWhenDepartmentMatches()
        {
            Assert.Equal("bcso", _map.ResolveAgency(Department.Police, "BCSO")?.Id);
            Assert.Equal("lspd", _map.ResolveAgency(Department.Police, null)?.Id);
            Assert.Null(_map.ResolveAgency(Department.Fire, "bcso"));
        }

        [Fact]
        public void IsInJurisdiction_AgencyWithoutTerritories_IsUnrestricted()
        {
            Assert.True(TerritoryMap.IsInJurisdiction(new AgencyDef(), null));
            Assert.True(TerritoryMap.IsInJurisdiction(null, "anything"));
        }

        [Fact]
        public void IsInJurisdiction_RequiresListedTerritory()
        {
            var lspd = _map.FindAgency("lspd");

            Assert.True(TerritoryMap.IsInJurisdiction(lspd, "los_santos"));
            Assert.False(TerritoryMap.IsInJurisdiction(lspd, "blaine_county"));
            Assert.False(TerritoryMap.IsInJurisdiction(lspd, null));
        }

        [Fact]
        public void Deserialize_ReplacesDefaultListsInsteadOfAppending()
        {
            var json = @"{ ""agencies"": [ { ""id"": ""sasp"", ""department"": ""Police"", ""territories"": [] } ] }";

            var config = JsonConvert.DeserializeObject<JurisdictionConfig>(json)!;

            var agency = Assert.Single(config.Agencies);
            Assert.Equal("sasp", agency.Id);
            Assert.Equal(Department.Police, agency.Department);
            Assert.Equal(2, config.Territories.Count);
        }

        [Fact]
        public void BundledConfigFile_MatchesBuiltInDefaults()
        {
            var path = System.IO.Path.Combine(RepoRoot(), "config", "jurisdictions.json");
            var fromFile = JsonConvert.DeserializeObject<JurisdictionConfig>(System.IO.File.ReadAllText(path))!;

            Assert.Equal(JsonConvert.SerializeObject(new JurisdictionConfig()), JsonConvert.SerializeObject(fromFile));
        }

        private static string RepoRoot()
        {
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir is not null && !System.IO.File.Exists(System.IO.Path.Combine(dir.FullName, "FivePRS.sln")))
                dir = dir.Parent;
            return dir!.FullName;
        }
    }
}
