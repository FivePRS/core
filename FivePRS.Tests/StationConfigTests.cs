using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FivePRS.Core.Config;
using FivePRS.Core.Models;
using Newtonsoft.Json;
using Xunit;

namespace FivePRS.Tests
{
    public class StationConfigTests
    {
        private static StationsConfig Sample() => new()
        {
            Stations = new List<StationDef>
            {
                new() { Id = "mrpd",   Department = Department.Police, Agencies = new List<string> { "lspd" }, DutyPoint = new[] { 1f, 2f, 3f } },
                new() { Id = "sandy",  Department = Department.Police, Agencies = new List<string> { "bcso" }, DutyPoint = new[] { 1f, 2f, 3f } },
                new() { Id = "davis",  Department = Department.Fire,   DutyPoint = new[] { 1f, 2f, 3f } },
                new() { Id = "broken", Department = Department.Police, DutyPoint = new[] { 1f, 2f } },
            },
        };

        [Fact]
        public void For_FiltersByDepartmentAndAgency()
        {
            var config = Sample();

            Assert.Equal(new[] { "mrpd" }, config.For(Department.Police, "LSPD").Select(s => s.Id));
            Assert.Equal(new[] { "mrpd", "sandy" }, config.For(Department.Police, null).Select(s => s.Id));
            Assert.Equal(new[] { "davis" }, config.For(Department.Fire, "anything").Select(s => s.Id));
        }

        [Fact]
        public void Find_IgnoresCaseAndInvalidStations()
        {
            var config = Sample();

            Assert.Equal("mrpd", config.Find("MRPD")?.Id);
            Assert.Null(config.Find("broken"));
            Assert.Null(config.Find(null));
        }

        [Fact]
        public void PointFor_ReturnsOnlyCompletePoints()
        {
            var station = new StationDef
            {
                Department = Department.Police,
                DutyPoint  = new[] { 1f, 2f, 3f },
                Armory     = new[] { 4f, 5f, 6f },
                Garage     = new[] { 7f, 8f },
            };

            Assert.Equal(new[] { 1f, 2f, 3f }, station.PointFor(StationPoint.Duty));
            Assert.Equal(new[] { 4f, 5f, 6f }, station.PointFor(StationPoint.Armory));
            Assert.Null(station.PointFor(StationPoint.Locker));
            Assert.Null(station.PointFor(StationPoint.Garage));
        }

        [Fact]
        public void BundledStations_AreValidAndMatchAgencies()
        {
            var stations      = Load<StationsConfig>("stations.json");
            var jurisdictions = Load<JurisdictionConfig>("jurisdictions.json");
            var agencies      = jurisdictions.Agencies.ToDictionary(a => a.Id, StringComparer.OrdinalIgnoreCase);

            Assert.NotEmpty(stations.Stations);
            Assert.All(stations.Stations, s => Assert.True(s.IsValid, s.Id));
            Assert.Equal(stations.Stations.Count, stations.Stations.Select(s => s.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(stations.Stations.SelectMany(s => s.Agencies.Select(a => new { Station = s, Agency = a })), entry =>
            {
                Assert.True(agencies.TryGetValue(entry.Agency, out var agency), $"{entry.Station.Id}: unknown agency {entry.Agency}");
                Assert.Equal(entry.Station.Department, agency!.Department);
            });
        }

        private static T Load<T>(string file)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FivePRS.sln")))
                dir = dir.Parent;

            return JsonConvert.DeserializeObject<T>(File.ReadAllText(Path.Combine(dir!.FullName, "config", file)))!;
        }
    }
}
