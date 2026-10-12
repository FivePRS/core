using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace FivePRS.Server.Database
{
    public sealed class SQLiteProvider : IDatabaseProvider
    {
        private readonly string _connectionString;

        public SqlDialect Dialect => SqlDialect.Sqlite;

        public SQLiteProvider(string dbPath)
        {
            var dir = Path.GetDirectoryName(dbPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            _connectionString = $"Data Source={Path.GetFullPath(dbPath)};";
        }

        public Task InitializeAsync() =>
            ExecuteAsync("PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;");

        public async Task<int> ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
        {
            using var conn = await OpenAsync();
            return await new Session(conn, null).ExecuteAsync(sql, parameters);
        }

        public async Task<long> InsertAsync(string sql, params (string Name, object? Value)[] parameters)
        {
            using var conn = await OpenAsync();
            return await new Session(conn, null).InsertAsync(sql, parameters);
        }

        public async Task<List<DbRow>> QueryAsync(string sql, params (string Name, object? Value)[] parameters)
        {
            using var conn = await OpenAsync();
            return await new Session(conn, null).QueryAsync(sql, parameters);
        }

        public Task BatchAsync(IReadOnlyList<SqlStatement> statements) =>
            ApplySchemaAsync(async db =>
            {
                foreach (var statement in statements)
                    await db.ExecuteAsync(statement.Sql, ToArray(statement.Parameters));
            });

        public async Task ApplySchemaAsync(Func<ISqlExecutor, Task> work)
        {
            using var conn = await OpenAsync();
            using var transaction = conn.BeginTransaction();
            await work(new Session(conn, transaction));
            transaction.Commit();
        }

        public async Task BackupAsync(string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await ExecuteAsync("VACUUM INTO @path;", ("@path", Path.GetFullPath(path)));
        }

        private async Task<SqliteConnection> OpenAsync()
        {
            var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            return conn;
        }

        private static (string Name, object? Value)[] ToArray(IReadOnlyList<(string Name, object? Value)> parameters)
        {
            var array = new (string Name, object? Value)[parameters.Count];
            for (var i = 0; i < array.Length; i++) array[i] = parameters[i];
            return array;
        }

        private sealed class Session : ISqlExecutor
        {
            private readonly SqliteConnection _conn;
            private readonly SqliteTransaction? _transaction;

            public Session(SqliteConnection conn, SqliteTransaction? transaction)
            {
                _conn = conn;
                _transaction = transaction;
            }

            public SqlDialect Dialect => SqlDialect.Sqlite;

            public async Task<int> ExecuteAsync(string sql, params (string Name, object? Value)[] parameters)
            {
                using var cmd = Command(sql, parameters);
                return await cmd.ExecuteNonQueryAsync();
            }

            public async Task<long> InsertAsync(string sql, params (string Name, object? Value)[] parameters)
            {
                await ExecuteAsync(sql, parameters);
                using var cmd = Command("SELECT last_insert_rowid()");
                return Convert.ToInt64(await cmd.ExecuteScalarAsync());
            }

            public async Task<List<DbRow>> QueryAsync(string sql, params (string Name, object? Value)[] parameters)
            {
                using var cmd = Command(sql, parameters);
                using var reader = await cmd.ExecuteReaderAsync();

                var rows = new List<DbRow>();
                while (await reader.ReadAsync())
                {
                    var values = new Dictionary<string, object?>(reader.FieldCount);
                    for (var i = 0; i < reader.FieldCount; i++)
                        values[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    rows.Add(new DbRow(values));
                }
                return rows;
            }

            private SqliteCommand Command(string sql, params (string Name, object? Value)[] parameters)
            {
                var cmd = _conn.CreateCommand();
                cmd.Transaction = _transaction;
                cmd.CommandText = sql;
                foreach (var (name, value) in parameters)
                    cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
                return cmd;
            }
        }
    }
}
