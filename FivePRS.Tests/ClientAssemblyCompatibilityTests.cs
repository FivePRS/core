using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace FivePRS.Tests
{
    public class ClientAssemblyCompatibilityTests
    {
        private static readonly string[] UnsupportedEmbeddedTypes =
        {
            "IsReadOnlyAttribute",
            "IsByRefLikeAttribute",
            "IsUnmanagedAttribute",
            "RequiresLocationAttribute",
            "RefSafetyRulesAttribute",
            "ScopedRefAttribute",
            "IsExternalInit",
        };

        public static IEnumerable<object[]> ClientAssemblies()
        {
            var dir = Path.Combine(RepoRoot(), "bin", "client");
            if (!Directory.Exists(dir))
                throw new InvalidOperationException($"{dir} not found; build FivePRS.sln before running tests.");

            return Directory.GetFiles(dir, "FivePRS.*.dll").Select(path => new object[] { Path.GetFileName(path) });
        }

        [Theory]
        [MemberData(nameof(ClientAssemblies))]
        public void ClientAssembly_HasNoMetadataUnsupportedByFiveMMono(string fileName)
        {
            using var stream = File.OpenRead(Path.Combine(RepoRoot(), "bin", "client", fileName));
            using var pe     = new PEReader(stream);
            var reader       = pe.GetMetadataReader();

            var embedded = reader.TypeDefinitions
                .Select(handle => reader.GetString(reader.GetTypeDefinition(handle).Name))
                .Where(UnsupportedEmbeddedTypes.Contains)
                .ToList();

            Assert.True(embedded.Count == 0,
                $"{fileName} embeds {string.Join(", ", embedded)}, which FiveM's client runtime rejects with BadImageFormatException. " +
                "Avoid readonly structs, readonly members, in/ref readonly parameters, ref structs, unmanaged constraints and init accessors in client code.");
        }

        [Theory]
        [MemberData(nameof(ClientAssemblies))]
        public void ClientAssembly_HasNoAttributesOnGenericParameters(string fileName)
        {
            using var stream = File.OpenRead(Path.Combine(RepoRoot(), "bin", "client", fileName));
            using var pe     = new PEReader(stream);
            var reader       = pe.GetMetadataReader();

            var count = reader.CustomAttributes
                .Select(handle => reader.GetCustomAttribute(handle).Parent.Kind)
                .Count(kind => kind == HandleKind.GenericParameter || kind == HandleKind.GenericParameterConstraint);

            Assert.True(count == 0,
                $"{fileName} has {count} custom attribute(s) on generic parameters or constraints, which FiveM's client runtime rejects with BadImageFormatException. " +
                "With nullable enabled these come from constrained generics such as 'where T : SomeClass'; use non-generic overloads in client code.");
        }

        private static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "FivePRS.sln")))
                dir = dir.Parent;
            return dir!.FullName;
        }
    }
}
