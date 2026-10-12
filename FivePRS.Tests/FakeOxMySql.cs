using System;
using System.Threading.Tasks;
using FivePRS.Server.Database;
using MySqlConnector;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FivePRS.Tests
{
    public sealed class FakeOxMySql : IOxMySql
    {
        private readonly string _connectionString;

        public FakeOxMySql(string connectionString) => _connectionString = connectionString;

        public async Task ConnectAsync()
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();
        }

        public async Task<string> SendAsync(string kind, string sql, string parameters)
        {
            using var conn = new MySqlConnection(_connectionString);
            await conn.OpenAsync();

            if (kind == "batch")
            {
                using var transaction = await conn.BeginTransactionAsync();
                foreach (var query in JArray.Parse(sql))
                {
                    using var cmd = Command(conn, query.Value<string>("query")!, (JArray)query["values"]!);
                    cmd.Transaction = transaction;
                    await cmd.ExecuteNonQueryAsync();
                }
                await transaction.CommitAsync();
                return "true";
            }

            using (var cmd = Command(conn, sql, JArray.Parse(parameters)))
            {
                switch (kind)
                {
                    case "insert":
                        await cmd.ExecuteNonQueryAsync();
                        return cmd.LastInsertedId.ToString();
                    case "update":
                        return (await cmd.ExecuteNonQueryAsync()).ToString();
                    case "query":
                        return await ReadAsync(cmd);
                    default:
                        throw new ArgumentException($"Unknown request {kind}.");
                }
            }
        }

        private static async Task<string> ReadAsync(MySqlCommand cmd)
        {
            using var reader = await cmd.ExecuteReaderAsync();
            var rows = new JArray();
            while (await reader.ReadAsync())
            {
                var row = new JObject();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    row[reader.GetName(i)] = value switch
                    {
                        null => JValue.CreateNull(),
                        DateTime date => new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Local)).ToUnixTimeMilliseconds(),
                        _ => JToken.FromObject(value),
                    };
                }
                rows.Add(row);
            }
            return rows.ToString(Formatting.None);
        }

        private static MySqlCommand Command(MySqlConnection conn, string sql, JArray values)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var value in values)
                cmd.Parameters.Add(new MySqlParameter { Value = ((JValue)value).Value ?? DBNull.Value });
            return cmd;
        }
    }
}
