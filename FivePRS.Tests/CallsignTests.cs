using FivePRS.Core.Models;
using Xunit;

namespace FivePRS.Tests
{
    public class CallsignTests
    {
        [Theory]
        [InlineData("1-adam-12", "1-ADAM-12")]
        [InlineData("  lspd21  ", "LSPD21")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void TryNormalize_ValidInput_UppercasesAndTrims(string? input, string expected)
        {
            Assert.True(Callsign.TryNormalize(input, out var callsign));
            Assert.Equal(expected, callsign);
        }

        [Theory]
        [InlineData("ADAM 12")]
        [InlineData("ADAM_12")]
        [InlineData("ÄDAM")]
        [InlineData("ABCDEFGHIJKLM")]
        public void TryNormalize_InvalidInput_IsRejected(string input)
        {
            Assert.False(Callsign.TryNormalize(input, out _));
        }
    }
}
