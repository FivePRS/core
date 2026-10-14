using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using FivePRS.Core.Logging;
using Xunit;

namespace FivePRS.Tests
{
    public class LoggingTests
    {
        private static LogEntry Entry(LogLevel level, string message, Exception? exception = null) =>
            new(DateTime.Now, level, "Test", message, exception);

        [Theory]
        [InlineData(LogLevel.Info, "^7")]
        [InlineData(LogLevel.Success, "^2")]
        [InlineData(LogLevel.Warning, "^3")]
        [InlineData(LogLevel.Error, "^1")]
        public void ConsoleSink_ColorsEachLevel(LogLevel level, string color)
        {
            var lines = new List<string>();
            new ConsoleSink(lines.Add).Write(Entry(level, "Hello"));

            Assert.Equal(new[] { $"^5[FivePRS]^7 {color}Hello^7" }, lines);
        }

        [Fact]
        public void ConsoleSink_HidesDebugUnlessEnabled()
        {
            var lines = new List<string>();
            new ConsoleSink(lines.Add).Write(Entry(LogLevel.Debug, "Hidden"));

            Assert.Empty(lines);
        }

        [Fact]
        public void ConsoleSink_ShowsTheCauseAndHintsOnce()
        {
            var lines = new List<string>();
            var sink  = new ConsoleSink(lines.Add, "See the log file.");
            var error = new AggregateException(new InvalidOperationException("Disk full"));

            sink.Write(Entry(LogLevel.Error, "Save failed", error));
            sink.Write(Entry(LogLevel.Error, "Save failed again", error));

            Assert.Equal(new[]
            {
                "^5[FivePRS]^7 ^1Save failed: Disk full^7",
                "^5[FivePRS]^7 See the log file.",
                "^5[FivePRS]^7 ^1Save failed again: Disk full^7",
            }, lines);
        }

        [Fact]
        public void Cause_UnwrapsWrapperExceptions()
        {
            var inner   = new InvalidOperationException("Root");
            var wrapped = new TargetInvocationException(new AggregateException(new TypeInitializationException("X", inner)));

            Assert.Same(inner, LogEntry.Cause(wrapped));
        }

        [Fact]
        public void Cause_KeepsPlainExceptions()
        {
            var error = new TaskCanceledException("Stopped");

            Assert.Same(error, LogEntry.Cause(error));
        }
    }
}
