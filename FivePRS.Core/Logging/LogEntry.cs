using System;
using System.Reflection;

namespace FivePRS.Core.Logging
{
    public sealed class LogEntry
    {
        public LogEntry(DateTime time, LogLevel level, string source, string message, Exception? exception)
        {
            Time      = time;
            Level     = level;
            Source    = source;
            Message   = message;
            Exception = exception;
        }

        public DateTime Time { get; }

        public LogLevel Level { get; }

        public string Source { get; }

        public string Message { get; }

        public Exception? Exception { get; }

        public string Text => Exception is null ? Message : $"{Message}: {Cause(Exception).Message}";

        public static Exception Cause(Exception ex)
        {
            while (ex is AggregateException { InnerException: not null } || ex is TargetInvocationException { InnerException: not null } ||
                   ex is TypeInitializationException { InnerException: not null })
            {
                ex = ex.InnerException!;
            }

            return ex;
        }
    }
}
