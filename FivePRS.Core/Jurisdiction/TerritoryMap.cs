using System;
using System.Collections.Generic;
using System.Linq;
using FivePRS.Core.Config;
using FivePRS.Core.Models;

namespace FivePRS.Core.Jurisdiction
{
    public sealed class TerritoryMap
    {
        private readonly JurisdictionConfig _config;

        public TerritoryMap(JurisdictionConfig config)
        {
            _config = config;
        }

        public IReadOnlyList<TerritoryDef> Territories => _config.Territories;

        public TerritoryDef? Resolve(float x, float y) =>
            _config.Territories.FirstOrDefault(t => Contains(t.Polygon, x, y));

        public TerritoryDef? FindTerritory(string? id) =>
            id is null ? null : _config.Territories.FirstOrDefault(t => Matches(t.Id, id));

        public AgencyDef? FindAgency(string? id) =>
            id is null ? null : _config.Agencies.FirstOrDefault(a => Matches(a.Id, id));

        public IEnumerable<AgencyDef> AgenciesFor(Department department) =>
            _config.Agencies.Where(a => a.Department == department);

        public AgencyDef? DefaultAgency(Department department) =>
            AgenciesFor(department).FirstOrDefault();

        public AgencyDef? ResolveAgency(Department department, string? agencyId)
        {
            var agency = FindAgency(agencyId);
            return agency is not null && agency.Department == department ? agency : DefaultAgency(department);
        }

        public static bool IsInJurisdiction(AgencyDef? agency, string? territoryId) =>
            agency is null ||
            agency.Territories.Count == 0 ||
            (territoryId is not null && agency.Territories.Any(t => Matches(t, territoryId)));

        public static bool Contains(IReadOnlyList<float[]> polygon, float x, float y)
        {
            var inside = false;

            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                var a = polygon[i];
                var b = polygon[j];
                if (a.Length < 2 || b.Length < 2) continue;

                if ((a[1] > y) != (b[1] > y) &&
                    x < (b[0] - a[0]) * (y - a[1]) / (b[1] - a[1]) + a[0])
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static bool Matches(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
