using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FivePRS.Server.Database
{
    public interface IOxMySql
    {
        Task ConnectAsync();

        Task<string> SendAsync(string kind, string sql, string parameters);
    }

    public sealed class OxMySqlProvider : IDatabaseProvider
    {
        private static readonly JsonSerializerSettings ReadSettings = new() { DateParseHandling = DateParseHandling.None };

        private readonly IOxMySql _oxmysql;

        public OxMySqlProvider(IOxMySql oxmysql) => _oxmysql = oxmysql;

        public SqlDialect Dialect => SqlDialect.MySql;

        public Task InitializeAsync() => _oxmysql.ConnectAsync();

        public async Task<int> ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
        {
            var result = await SendAsync("update", new SqlStatement(sql, parameters));
            return result.Type == JTokenType.Integer ? result.Value<int>() : 0;
        }

        public async Task<long> InsertAsync(string sql, params (string Name, object? Value)[] parameters)
        {
            var result = await SendAsync("insert", new SqlStatement(sql, parameters));
            return result.Type == JTokenType.Integer ? result.Value<long>() : 0;
        }

        public async Task<List<DbRow>> QueryAsync(string sql, params (string Name, object? Value)[] parameters) =>
            ReadRows(await SendAsync("query", new SqlStatement(sql, parameters)));

        public async Task BatchAsync(IReadOnlyList<SqlStatement> statements)
        {
            var queries = statements.Select(statement =>
            {
                var (sql, values) = statement.ToPositional();
                return new { query = sql, values = ToJsonValues(values) };
            });

            await _oxmysql.SendAsync("batch", JsonConvert.SerializeObject(queries), "[]");
        }

        public Task ApplySchemaAsync(Func<ISqlExecutor, Task> work) => work(this);

        private async Task<JToken> SendAsync(string kind, SqlStatement statement)
        {
            var (sql, values) = statement.ToPositional();
            var json = await _oxmysql.SendAsync(kind, sql, JsonConvert.SerializeObject(ToJsonValues(values)));
            return string.IsNullOrEmpty(json) ? JValue.CreateNull() : JsonConvert.DeserializeObject<JToken>(json, ReadSettings) ?? JValue.CreateNull();
        }

        public static List<DbRow> ReadRows(JToken result)
        {
            var rows = new List<DbRow>();
            if (result is not JArray array) return rows;

            foreach (var item in array.OfType<JObject>())
            {
                var values = new Dictionary<string, object?>();
                foreach (var property in item.Properties())
                    values[property.Name] = property.Value is JValue value ? value.Value : property.Value.ToString(Formatting.None);
                rows.Add(new DbRow(values));
            }
            return rows;
        }

        private static object?[] ToJsonValues(object?[] values) =>
            values.Select(value => value switch
            {
                DateTime date => (object)date.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
                DateTimeOffset offset => offset.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture),
                Enum e => Convert.ToInt64(e, CultureInfo.InvariantCulture),
                _ => value,
            }).ToArray();
    }
}
