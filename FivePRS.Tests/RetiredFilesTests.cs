using System;
using System.Collections.Generic;
using System.IO;
using FivePRS.Server;
using Xunit;

namespace FivePRS.Tests
{
    public sealed class RetiredFilesTests : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"fiveprs-retired-{Guid.NewGuid():N}");

        public RetiredFilesTests() => Directory.CreateDirectory(_directory);

        [Fact]
        public void Remove_DeletesOnlyRetiredFiles()
        {
            File.WriteAllText(Path.Combine(_directory, "MySqlConnector.dll"), "old");
            File.WriteAllText(Path.Combine(_directory, "FivePRS.Server.net.dll"), "current");

            var warnings = new List<string>();
            var removed = RetiredFiles.Remove(_directory, warnings.Add);

            Assert.Equal(new[] { "MySqlConnector.dll" }, removed);
            Assert.Empty(warnings);
            Assert.False(File.Exists(Path.Combine(_directory, "MySqlConnector.dll")));
            Assert.True(File.Exists(Path.Combine(_directory, "FivePRS.Server.net.dll")));
        }

        [Fact]
        public void Remove_CleanFolder_RemovesNothing()
        {
            Assert.Empty(RetiredFiles.Remove(_directory, _ => { }));
        }

        [Fact]
        public void Retired_NeverIncludesAShippedFile()
        {
            var publish = File.ReadAllText(Path.Combine(Root(), "publish.ps1"));
            foreach (var name in RetiredFiles.Server)
                Assert.DoesNotContain($"\"{name}\"", publish);
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "publish.ps1"))) dir = dir.Parent;
            return dir?.FullName ?? throw new DirectoryNotFoundException("publish.ps1 not found above the test folder.");
        }

        public void Dispose()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }
    }
}
