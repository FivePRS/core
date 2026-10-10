using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace FivePRS.Server.Database
{
    public abstract class SqlStore
    {
        protected SqlStore(IDatabaseProvider db) => Db = db;

        protected IDatabaseProvider Db { get; }

        protected bool IsMySql => Db.Dialect == SqlDialect.MySql;

        protected string LastInsertId => IsMySql ? "SELECT LAST_INSERT_ID()" : "SELECT last_insert_rowid()";

        protected async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
        {
            using var conn = Db.CreateConnection();
            await conn.OpenAsync();
            using var cmd = CreateCommand(conn, sql, parameters);
            await cmd.ExecuteNonQueryAsync();
        }

        protected async Task<object?> ScalarAsync(string sql, params (string Name, object Value)[] parameters)
        {
            using var conn = Db.CreateConnection();
            await conn.OpenAsync();
            using var cmd = CreateCommand(conn, sql, parameters);
            return await cmd.ExecuteScalarAsync();
        }

        protected async Task<List<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> read, params (string Name, object Value)[] parameters)
        {
            using var conn = Db.CreateConnection();
            await conn.OpenAsync();
            using var cmd = CreateCommand(conn, sql, parameters);
            using var reader = await cmd.ExecuteReaderAsync();

            var rows = new List<T>();
            while (await reader.ReadAsync())
                rows.Add(read(reader));
            return rows;
        }

        private static DbCommand CreateCommand(DbConnection conn, string sql, (string Name, object Value)[] parameters)
        {
            var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (name, value) in parameters)
            {
                var parameter = cmd.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                cmd.Parameters.Add(parameter);
            }
            return cmd;
        }
    }
}
