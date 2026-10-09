using System.Threading.Tasks;
using FivePRS.Core.Models;
using FivePRS.Server.Permissions;
using Xunit;

namespace FivePRS.Tests
{
    [Collection(TestDatabase.Collection)]
    public sealed class RosterServiceTests : IAsyncLifetime
    {
        private const string Alice = "license:alice";

        private TestDatabase _database = null!;
        private RosterService _roster = null!;

        public async Task InitializeAsync()
        {
            _database = await TestDatabase.CreateAsync();
            await _database.Provider.SavePlayerAsync(new PlayerData { License = Alice, Name = "Alice Cooper", Rank = 4 });
            await _database.Provider.SavePlayerAsync(new PlayerData { License = "license:bob", Name = "Bob Ross" });
            _roster = new RosterService(_database.Roster);
            await _roster.LoadAsync();
        }

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task SetAsync_GrantsAndRevokes_AndPersists()
        {
            Assert.True(await _roster.SetAsync(Alice, Department.Police, true, "Admin"));
            Assert.False(await _roster.SetAsync(Alice, Department.Police, true, "Admin"));
            Assert.True(_roster.Contains(Alice, Department.Police));
            Assert.False(_roster.Contains(Alice, Department.EMS));

            var reloaded = new RosterService(_database.Roster);
            await reloaded.LoadAsync();
            Assert.True(reloaded.Contains(Alice, Department.Police));

            Assert.True(await _roster.SetAsync(Alice, Department.Police, false, "Admin"));
            Assert.False(_roster.Contains(Alice, Department.Police));

            await reloaded.LoadAsync();
            Assert.False(reloaded.Contains(Alice, Department.Police));
        }

        [Fact]
        public async Task SetAsync_RejectsNoneDepartment()
        {
            Assert.False(await _roster.SetAsync(Alice, Department.None, true, "Admin"));
        }

        [Fact]
        public async Task SearchAsync_FindsOfflinePlayersWithTheirDepartments()
        {
            await _roster.SetAsync(Alice, Department.Fire, true, "Admin");

            var match = Assert.Single(await _roster.SearchAsync("alice"));
            Assert.Equal("Alice Cooper", match.Name);
            Assert.Equal(4, match.Rank);
            Assert.Equal(new[] { Department.Fire }, match.Departments);

            Assert.Empty(await _roster.SearchAsync("a"));
        }
    }
}
