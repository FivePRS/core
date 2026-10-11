using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FivePRS.Core.Models;
using FivePRS.Server.Database;
using Newtonsoft.Json.Linq;
using Xunit;

namespace FivePRS.Tests
{
    public class SqlStatementTests
    {
        [Fact]
        public void ToPositional_ReplacesNamesInOrderAndRepeats()
        {
            var (sql, values) = new SqlStatement(
                "INSERT INTO t (a, b, c) VALUES (@now, @name, @now)",
                ("@name", "Ann"), ("@now", 5)).ToPositional();

            Assert.Equal("INSERT INTO t (a, b, c) VALUES (?, ?, ?)", sql);
            Assert.Equal(new object?[] { 5, "Ann", 5 }, values);
        }

        [Fact]
        public void ToPositional_LeavesQuotedTextAlone()
        {
            var (sql, values) = new SqlStatement("SELECT '@kept', `@col`, \"@x\" FROM t WHERE a = @a", ("@a", 1)).ToPositional();

            Assert.Equal("SELECT '@kept', `@col`, \"@x\" FROM t WHERE a = ?", sql);
            Assert.Single(values);
        }

        [Fact]
        public void ToPositional_MissingValue_Throws()
        {
            Assert.Throws<ArgumentException>(() => new SqlStatement("SELECT @missing").ToPositional());
        }

        [Fact]
        public void Split_SeparatesStatementsButNotQuotedSemicolons()
        {
            var parts = SqlStatement.Split("CREATE TABLE a (x TEXT DEFAULT ';');\n CREATE INDEX i ON a(x);\n");

            Assert.Equal(new[] { "CREATE TABLE a (x TEXT DEFAULT ';')", "CREATE INDEX i ON a(x)" }, parts);
        }
    }

    public class DbRowTests
    {
        private static DbRow Row(string column, object? value) => new(new Dictionary<string, object?> { [column] = value });

        [Fact]
        public void GetBoolean_AcceptsBooleansNumbersAndText()
        {
            Assert.True(Row("a", true).GetBoolean("a"));
            Assert.True(Row("a", 1L).GetBoolean("a"));
            Assert.False(Row("a", "0").GetBoolean("a"));
        }

        [Fact]
        public void GetInt32_AcceptsBooleans()
        {
            Assert.Equal(1, Row("a", true).GetInt32("a"));
        }

        [Fact]
        public void GetDateTime_TextWithoutZone_IsUtc()
        {
            var date = Row("a", "2026-10-11 12:30:00").GetDateTime("a");

            Assert.Equal(new DateTime(2026, 10, 11, 12, 30, 0, DateTimeKind.Utc), date);
            Assert.Equal(DateTimeKind.Utc, date.Kind);
        }

        [Fact]
        public void GetDateTime_OxMySqlTimestamp_RestoresStoredWallClock()
        {
            var stored = new DateTime(2026, 10, 11, 12, 30, 0);
            var timestamp = new DateTimeOffset(DateTime.SpecifyKind(stored, DateTimeKind.Local)).ToUnixTimeMilliseconds();

            Assert.Equal(DateTime.SpecifyKind(stored, DateTimeKind.Utc), Row("a", timestamp).GetDateTime("a"));
        }

        [Fact]
        public void ColumnNames_AreCaseInsensitive_AndMissingColumnsAreNull()
        {
            var row = Row("Name", "x");

            Assert.Equal("x", row.GetString("name"));
            Assert.True(row.IsNull("resolution"));
            Assert.Throws<InvalidCastException>(() => row.GetString("resolution"));
        }
    }

    public class OxMySqlProviderTests
    {
        private sealed class Recorder : IOxMySql
        {
            public readonly List<(string Kind, string Sql, string Parameters)> Requests = new();
            public string Response = "null";

            public Task ConnectAsync() => Task.CompletedTask;

            public Task<string> SendAsync(string kind, string sql, string parameters)
            {
                Requests.Add((kind, sql, parameters));
                return Task.FromResult(Response);
            }
        }

        [Fact]
        public async Task Query_SendsPositionalParametersAndUtcDates()
        {
            var ox = new Recorder { Response = "[]" };
            var provider = new OxMySqlProvider(ox);

            await provider.QueryAsync("SELECT * FROM t WHERE a = @a AND b = @b AND c = @c",
                ("@a", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)), ("@b", Department.Police), ("@c", null));

            var (kind, sql, parameters) = Assert.Single(ox.Requests);
            Assert.Equal("query", kind);
            Assert.Equal("SELECT * FROM t WHERE a = ? AND b = ? AND c = ?", sql);
            Assert.Equal(new JArray("2026-01-02 03:04:05.000000", (long)Department.Police, JValue.CreateNull()).ToString(), JArray.Parse(parameters).ToString());
        }

        [Fact]
        public async Task Query_ReadsOxMySqlRows()
        {
            var ox = new Recorder { Response = "[{\"license\":\"license:a\",\"is_on_duty\":true,\"xp\":40,\"last_seen\":\"2026-10-11T12:00:00Z\",\"resolution\":null}]" };

            var row = Assert.Single(await new OxMySqlProvider(ox).QueryAsync("SELECT 1"));

            Assert.Equal("license:a", row.GetString("license"));
            Assert.True(row.GetBoolean("is_on_duty"));
            Assert.Equal(40, row.GetInt32("xp"));
            Assert.Equal(new DateTime(2026, 10, 11, 12, 0, 0, DateTimeKind.Utc), row.GetDateTime("last_seen"));
            Assert.True(row.IsNull("resolution"));
        }

        [Fact]
        public async Task Insert_ReturnsInsertId()
        {
            var ox = new Recorder { Response = "42" };

            Assert.Equal(42, await new OxMySqlProvider(ox).InsertAsync("INSERT INTO t (a) VALUES (@a)", ("@a", 1)));
            Assert.Equal("insert", ox.Requests.Single().Kind);
        }

        [Fact]
        public async Task Batch_SendsEveryStatementAsOneTransaction()
        {
            var ox = new Recorder { Response = "true" };

            await new OxMySqlProvider(ox).BatchAsync(new[]
            {
                new SqlStatement("DELETE FROM a WHERE id = @id", ("@id", 7)),
                new SqlStatement("DELETE FROM b WHERE id = @id", ("@id", 7)),
            });

            var (kind, sql, _) = Assert.Single(ox.Requests);
            Assert.Equal("batch", kind);
            var queries = JArray.Parse(sql);
            Assert.Equal(2, queries.Count);
            Assert.Equal("DELETE FROM b WHERE id = ?", queries[1]["query"]!.Value<string>());
            Assert.Equal(7, queries[1]["values"]![0]!.Value<int>());
        }
    }
}
