using FivePRS.Core.Config;
using Newtonsoft.Json;
using Xunit;

namespace FivePRS.Tests
{
    public class WorldSettingsTests
    {
        [Fact]
        public void Defaults_DisableWantedLevelAndDispatchButKeepRandomCops()
        {
            var world = new ResourceSettings().World;

            Assert.True(world.DisableWantedLevel);
            Assert.True(world.DisableDispatchServices);
            Assert.False(world.DisableRandomCops);
        }

        [Fact]
        public void PartialOverride_KeepsOtherDefaults()
        {
            var world = JsonConvert.DeserializeObject<ResourceSettings>("{\"world\":{\"disableRandomCops\":true}}")!.World;

            Assert.True(world.DisableRandomCops);
            Assert.True(world.DisableWantedLevel);
        }
    }
}
