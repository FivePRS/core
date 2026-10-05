using FivePRS.Core.Models;
using Xunit;

namespace FivePRS.Tests
{
    public class PlayerDataTests
    {
        [Fact]
        public void AddXP_BelowThreshold_DoesNotRankUp()
        {
            var player = new PlayerData();

            Assert.False(player.AddXP(99));
            Assert.Equal(1, player.Rank);
            Assert.Equal(99, player.XP);
        }

        [Fact]
        public void AddXP_CrossingSeveralThresholds_RanksUpEachTime()
        {
            var player = new PlayerData();

            Assert.True(player.AddXP(100 + 400 + 50));

            Assert.Equal(3, player.Rank);
            Assert.Equal(50, player.XP);
        }

        [Fact]
        public void AddXP_NegativeAmount_IsIgnored()
        {
            var player = new PlayerData { XP = 10 };

            player.AddXP(-500);

            Assert.Equal(10, player.XP);
        }
    }
}
