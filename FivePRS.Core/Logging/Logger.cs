using System;
using System.Collections.Generic;

namespace FivePRS.Core.Logging
{
    public sealed class Logger
    {
        private static readonly object Gate = new();
        private static ILogSink[] _sinks = new ILogSink[0];

        private readonly string _source;

        private Logger(string source)
        {
            _source = source;
        }

        public static bool DebugEnabled { get; set; }

        public static Logger For(string source) => new(source);

        public static void AddSink(ILogSink sink)
        {
            lock (Gate)
            {
                var sinks = new List<ILogSink>(_sinks) { sink };
                _sinks = sinks.ToArray();
            }
        }

        public static void ClearSinks()
        {
            lock (Gate) _sinks = new ILogSink[0];
        }

        public void Debug(string message) => Write(LogLevel.Debug, message, null);

        public void Info(string message) => Write(LogLevel.Info, message, null);

        public void Success(string message) => Write(LogLevel.Success, message, null);

        public void Warn(string message, Exception? exception = null) => Write(LogLevel.Warning, message, exception);

        public void Error(string message, Exception? exception = null) => Write(LogLevel.Error, message, exception);

        private void Write(LogLevel level, string message, Exception? exception)
        {
            var entry = new LogEntry(DateTime.Now, level, _source, message, exception);
            foreach (var sink in _sinks)
            {
                try
                {
                    sink.Write(entry);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
