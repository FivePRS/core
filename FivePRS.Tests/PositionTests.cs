using System.Threading.Tasks;
using FivePRS.Core.Models;
using Xunit;

namespace FivePRS.Tests
{
    public class SavedPositionTests
    {
        [Theory]
        [InlineData(float.NaN, 0f, 0f)]
        [InlineData(20_000f, 0f, 30f)]
        [InlineData(100f, 100f, 5_000f)]
        [InlineData(0f, 0f, 30f)]
        public void Create_RejectsImpossiblePositions(float x, float y, float z)
        {
            Assert.Null(SavedPosition.Create(x, y, z, 0f));
        }

        [Fact]
        public void Create_NormalisesHeading()
        {
            var position = SavedPosition.Create(433.6f, -981.8f, 30.7f, -90f);
            Assert.Equal(270f, position!.Heading);
        }
    }

    [Collection(TestDatabase.Collection)]
    public sealed class PositionStoreTests : IAsyncLifetime
    {
        private TestDatabase _database = null!;

        public async Task InitializeAsync() => _database = await TestDatabase.CreateAsync();

        public Task DisposeAsync()
        {
            _database.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Save_StoresAndOverwritesThePlayersPosition()
        {
            Assert.Null(await _database.Positions.GetAsync("license:a"));

            await _database.Positions.SaveAsync("license:a", SavedPosition.Create(1f, 2f, 3f, 4f)!);
            await _database.Positions.SaveAsync("license:a", SavedPosition.Create(-1108.4f, -845f, 19.3f, 130f)!);

            var position = await _database.Positions.GetAsync("license:a");
            Assert.Equal(-1108.4f, position!.X, 2);
            Assert.Equal(-845f, position.Y, 2);
            Assert.Equal(130f, position.Heading, 2);
        }
    }
}
