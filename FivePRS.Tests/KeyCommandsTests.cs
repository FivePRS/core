using System.Linq;
using System.Reflection;
using System.Text;
using FivePRS.Core.Text;
using Xunit;

namespace FivePRS.Tests
{
    public class KeyCommandsTests
    {
        [Fact]
        public void PromptedCommands_HashWithHighBitSoKeyTokensRender()
        {
            var commands = typeof(KeyCommands)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral)
                .Select(f => (string)f.GetRawConstantValue()!)
                .ToList();

            Assert.NotEmpty(commands);
            Assert.All(commands, command =>
                Assert.True((Joaat(command) & 0x80000000) != 0, $"'{command}' needs a name whose joaat hash has the high bit set"));
        }

        private static uint Joaat(string text)
        {
            uint hash = 0;
            foreach (var b in Encoding.UTF8.GetBytes(text.ToLowerInvariant()))
            {
                hash += b;
                hash += hash << 10;
                hash ^= hash >> 6;
            }

            hash += hash << 3;
            hash ^= hash >> 11;
            hash += hash << 15;
            return hash;
        }
    }
}
