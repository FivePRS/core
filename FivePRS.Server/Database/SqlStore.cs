using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FivePRS.Server.Database
{
    public abstract class SqlStore
    {
        protected SqlStore(IDatabaseProvider db) => Db = db;

        protected IDatabaseProvider Db { get; }

        protected bool IsMySql => Db.Dialect == SqlDialect.MySql;

        protected Task ExecuteAsync(string sql, params (string Name, object? Value)[] parameters) =>
            MainThread.Run(() => Db.ExecuteAsync(sql, parameters));

        protected Task<long> InsertAsync(string sql, params (string Name, object? Value)[] parameters) =>
            MainThread.Run(() => Db.InsertAsync(sql, parameters));

        protected Task BatchAsync(params SqlStatement[] statements) =>
            MainThread.Run(() => Db.BatchAsync(statements));

        protected async Task<List<T>> QueryAsync<T>(string sql, Func<DbRow, T> read, params (string Name, object? Value)[] parameters)
        {
            var rows = await MainThread.Run(() => Db.QueryAsync(sql, parameters));
            var result = new List<T>(rows.Count);
            foreach (var row in rows)
                result.Add(read(row));
            return result;
        }
    }
}
