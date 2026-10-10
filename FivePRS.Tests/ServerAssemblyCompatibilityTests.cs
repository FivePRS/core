using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using FivePRS.Server.Database;
using Xunit;

namespace FivePRS.Tests
{
    public class ServerAssemblyCompatibilityTests
    {
        [Fact]
        public void ServerAssembly_HasNoPInvoke()
        {
            using var stream = File.OpenRead(typeof(SqlStore).Assembly.Location);
            using var pe = new PEReader(stream);
            var metadata = pe.GetMetadataReader();

            var pinvokes = metadata.MethodDefinitions
                .Select(metadata.GetMethodDefinition)
                .Where(method => (method.Attributes & MethodAttributes.PinvokeImpl) != 0)
                .Select(method => metadata.GetString(method.Name))
                .ToList();

            Assert.Empty(pinvokes);
        }
    }
}
