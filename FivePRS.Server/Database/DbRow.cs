using System;
using System.Collections.Generic;
using System.Globalization;

namespace FivePRS.Server.Database
{
    public sealed class DbRow
    {
        private readonly Dictionary<string, object?> _values;

        public DbRow(IDictionary<string, object?> values)
        {
            _values = new Dictionary<string, object?>(values, StringComparer.OrdinalIgnoreCase);
        }

        public bool IsNull(string column)
        {
            var value = Raw(column);
            return value is null || value is DBNull;
        }

        public object Value(string column)
        {
            var value = Raw(column);
            if (value is null || value is DBNull)
                throw new InvalidCastException($"Column '{column}' is null.");
            return value;
        }

        public string GetString(string column) => Convert.ToString(Value(column), CultureInfo.InvariantCulture)!;

        public int GetInt32(string column) => Value(column) is bool flag ? (flag ? 1 : 0) : Convert.ToInt32(Value(column), CultureInfo.InvariantCulture);

        public long GetInt64(string column) => Value(column) is bool flag ? (flag ? 1 : 0) : Convert.ToInt64(Value(column), CultureInfo.InvariantCulture);

        public double GetDouble(string column) => Convert.ToDouble(Value(column), CultureInfo.InvariantCulture);

        public bool GetBoolean(string column) => Value(column) switch
        {
            bool flag     => flag,
            string text   => text == "1" || text.Equals("true", StringComparison.OrdinalIgnoreCase),
            var number    => Convert.ToInt64(number, CultureInfo.InvariantCulture) != 0,
        };

        public DateTime GetDateTime(string column) => Value(column) switch
        {
            DateTime date          => date.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(date, DateTimeKind.Utc) : date.ToUniversalTime(),
            DateTimeOffset offset  => offset.UtcDateTime,
            string text            => DateTime.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
            var milliseconds       => FromLocalTimestamp(Convert.ToInt64(milliseconds, CultureInfo.InvariantCulture)),
        };

        public static DateTime FromLocalTimestamp(long milliseconds) =>
            DateTime.SpecifyKind(DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).LocalDateTime, DateTimeKind.Utc);

        private object? Raw(string column) => _values.TryGetValue(column, out var value) ? value : null;
    }
}
