using System.Linq;
using System.Text;
using FivePRS.Core.Text;
using Xunit;

namespace FivePRS.Tests
{
    public class GameTextTests
    {
        [Fact]
        public void SplitComponents_ShortText_IsOnePart()
        {
            Assert.Equal(new[] { "~g~Suspect cuffed" }, GameText.SplitComponents("~g~Suspect cuffed"));
        }

        [Theory]
        [InlineData("~y~Welcome to FivePRS!~w~ Press ~b~F5~w~ or use ~b~/duty~w~ to go on duty.")]
        [InlineData("~b~Los Santos Police Department~w~ | ~g~ON DUTY~w~ | Recruit loadout applied.~n~~w~ Your patrol vehicle is marked on the map.")]
        [InlineData("~y~FivePRS Commands~w~~n~~b~/duty~w~ — Choose department, agency and callsign, or go off duty~n~~b~/er_profile~w~ — View rank and XP~n~~b~/er_accept~w~ — Accept incoming callout")]
        public void SplitComponents_LongText_RejoinsExactlyWithinByteLimit(string text)
        {
            var parts = GameText.SplitComponents(text);

            Assert.Equal(text, string.Concat(parts));
            Assert.All(parts, part => Assert.True(Encoding.UTF8.GetByteCount(part) <= GameText.MaxComponentBytes));
        }

        [Fact]
        public void SplitComponents_NeverSplitsInsideColourCode()
        {
            var text = new string('a', GameText.MaxComponentBytes - 1) + "~b~blue text";

            var parts = GameText.SplitComponents(text);

            Assert.All(parts, part => Assert.Equal(0, part.Count(c => c == '~') % 2));
            Assert.Equal(text, string.Concat(parts));
        }

        [Fact]
        public void SplitComponents_NeverSplitsMultiByteCharacter()
        {
            var text = new string('a', GameText.MaxComponentBytes - 1) + "——";

            var parts = GameText.SplitComponents(text);

            Assert.Equal(text, string.Concat(parts));
            Assert.All(parts, part => Assert.True(Encoding.UTF8.GetByteCount(part) <= GameText.MaxComponentBytes));
        }
    }
}
