using System;
using System.IO;
using FivePRS.Core.Logging;

namespace FivePRS.Server.Logging
{
    internal sealed class FileLogSink : ILogSink
    {
        private const int KeepDays = 14;
        private const string Pattern = "fiveprs-*.log";

        private readonly object _gate = new();
        private readonly string _directory;
        private bool _failed;

        public FileLogSink(string directory)
        {
            _directory = directory;
        }

        public void Prune()
        {
            if (!Directory.Exists(_directory)) return;

            var cutoff = DateTime.Now.AddDays(-KeepDays);
            foreach (var file in Directory.GetFiles(_directory, Pattern))
            {
                try
                {
                    if (File.GetLastWriteTime(file) < cutoff) File.Delete(file);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        public void Write(LogEntry entry)
        {
            if (_failed) return;

            var text = $"{entry.Time:yyyy-MM-dd HH:mm:ss} {Label(entry.Level),-7} {entry.Source}: {entry.Message}{Environment.NewLine}";
            if (entry.Exception is not null)
                text += entry.Exception + Environment.NewLine;

            lock (_gate)
            {
                try
                {
                    Directory.CreateDirectory(_directory);
                    File.AppendAllText(Path.Combine(_directory, $"fiveprs-{entry.Time:yyyy-MM-dd}.log"), text);
                }
                catch (Exception)
                {
                    _failed = true;
                }
            }
        }

        private static string Label(LogLevel level) => level switch
        {
            LogLevel.Debug   => "DEBUG",
            LogLevel.Success => "OK",
            LogLevel.Warning => "WARN",
            LogLevel.Error   => "ERROR",
            _                => "INFO",
        };
    }
}
