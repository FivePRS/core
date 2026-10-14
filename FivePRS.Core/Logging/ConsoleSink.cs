using System;

namespace FivePRS.Core.Logging
{
    public sealed class ConsoleSink : ILogSink
    {
        private const string Prefix = "^5[FivePRS]^7 ";

        private readonly Action<string> _write;
        private readonly string? _detailsHint;
        private bool _hinted;

        public ConsoleSink(Action<string> write, string? detailsHint = null)
        {
            _write       = write;
            _detailsHint = detailsHint;
        }

        public void Write(LogEntry entry)
        {
            if (entry.Level == LogLevel.Debug && !Logger.DebugEnabled) return;

            _write($"{Prefix}{Color(entry.Level)}{entry.Text}^7");
            if (entry.Exception is null) return;

            if (Logger.DebugEnabled)
            {
                _write(entry.Exception.ToString());
            }
            else if (!_hinted && _detailsHint is not null)
            {
                _hinted = true;
                _write($"{Prefix}{_detailsHint}");
            }
        }

        public static string Color(LogLevel level) => level switch
        {
            LogLevel.Debug   => "^6",
            LogLevel.Success => "^2",
            LogLevel.Warning => "^3",
            LogLevel.Error   => "^1",
            _                => "^7",
        };
    }
}
