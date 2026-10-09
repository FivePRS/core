using FivePRS.Core.Text;
using Xunit;

namespace FivePRS.Tests
{
    public class WeaponLabelsTests
    {
        [Theory]
        [InlineData("WEAPON_STUNGUN", null, "Taser")]
        [InlineData("weapon_pumpshotgun", null, "Pump Shotgun")]
        [InlineData("WEAPON_HEAVY_SNIPER", null, "Heavy Sniper")]
        [InlineData("WEAPON_PISTOL", "Duty Pistol", "Duty Pistol")]
        public void For_UsesLabelKnownNameOrReadableFallback(string weapon, string? label, string expected)
        {
            Assert.Equal(expected, WeaponLabels.For(weapon, label));
        }
    }
}
