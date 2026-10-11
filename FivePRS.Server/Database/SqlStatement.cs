using System;
using System.Collections.Generic;
using System.Text;

namespace FivePRS.Server.Database
{
    public sealed class SqlStatement
    {
        public SqlStatement(string sql, params (string Name, object? Value)[] parameters)
        {
            Sql = sql;
            Parameters = parameters;
        }

        public string Sql { get; }

        public IReadOnlyList<(string Name, object? Value)> Parameters { get; }

        public (string Sql, object?[] Values) ToPositional()
        {
            var lookup = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (name, value) in Parameters)
                lookup[name.TrimStart('@')] = value;

            var sql = new StringBuilder(Sql.Length);
            var values = new List<object?>();
            char? quote = null;

            for (var i = 0; i < Sql.Length; i++)
            {
                var c = Sql[i];

                if (quote is not null)
                {
                    sql.Append(c);
                    if (c == quote) quote = null;
                    continue;
                }

                if (c == '\'' || c == '"' || c == '`')
                {
                    quote = c;
                    sql.Append(c);
                    continue;
                }

                if (c != '@' || i + 1 >= Sql.Length || !IsNameChar(Sql[i + 1]))
                {
                    sql.Append(c);
                    continue;
                }

                var start = i + 1;
                var end = start;
                while (end < Sql.Length && IsNameChar(Sql[end])) end++;

                var name = Sql.Substring(start, end - start);
                if (!lookup.TryGetValue(name, out var parameter))
                    throw new ArgumentException($"The query uses @{name}, but no value was given for it.");

                sql.Append('?');
                values.Add(parameter);
                i = end - 1;
            }

            return (sql.ToString(), values.ToArray());
        }

        public static IReadOnlyList<string> Split(string script)
        {
            var statements = new List<string>();
            var current = new StringBuilder();
            char? quote = null;

            foreach (var c in script)
            {
                if (quote is not null)
                {
                    if (c == quote) quote = null;
                }
                else if (c == '\'' || c == '"' || c == '`')
                {
                    quote = c;
                }
                else if (c == ';')
                {
                    Add(statements, current);
                    continue;
                }

                current.Append(c);
            }

            Add(statements, current);
            return statements;
        }

        private static void Add(List<string> statements, StringBuilder current)
        {
            var statement = current.ToString().Trim();
            if (statement.Length > 0) statements.Add(statement);
            current.Clear();
        }

        private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
