using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Civilian;
using FivePRS.Core.Models;
using FivePRS.Server.Database;

namespace FivePRS.Server.Permissions
{
    public sealed class RosterService
    {
        private const int SearchLimit = 25;

        private readonly RosterStore _store;
        private readonly HashSet<string> _entries = new(StringComparer.Ordinal);

        public RosterService(RosterStore store) => _store = store;

        public async Task LoadAsync()
        {
            _entries.Clear();
            foreach (var (license, department) in await _store.GetAllAsync())
                _entries.Add(Key(license, department));
        }

        public bool Contains(string? license, Department department) =>
            license is not null && _entries.Contains(Key(license, department));

        public List<Department> DepartmentsFor(string license) =>
            Enum.GetValues(typeof(Department)).Cast<Department>().Where(d => Contains(license, d)).ToList();

        public async Task<bool> SetAsync(string license, Department department, bool granted, string grantedBy)
        {
            if (department == Department.None || string.IsNullOrWhiteSpace(license)) return false;
            if (Contains(license, department) == granted) return false;

            if (granted)
            {
                await _store.GrantAsync(license, department, grantedBy);
                _entries.Add(Key(license, department));
            }
            else
            {
                await _store.RevokeAsync(license, department);
                _entries.Remove(Key(license, department));
            }

            return true;
        }

        public async Task<List<RosterPlayer>> SearchAsync(string? term)
        {
            if (!CivilianRules.TryNormalizeSearch(term, out var normalized)) return new List<RosterPlayer>();

            var players = await _store.SearchPlayersAsync(normalized, SearchLimit);
            foreach (var player in players)
                player.Departments = DepartmentsFor(player.License);
            return players;
        }

        private static string Key(string license, Department department) => $"{license}|{(int)department}";
    }
}
