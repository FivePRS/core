using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FivePRS.Server.Database
{
    public interface ISqlExecutor
    {
        SqlDialect Dialect { get; }

        Task<int> ExecuteAsync(string sql, params (string Name, object? Value)[] parameters);

        Task<long> InsertAsync(string sql, params (string Name, object? Value)[] parameters);

        Task<List<DbRow>> QueryAsync(string sql, params (string Name, object? Value)[] parameters);
    }

    public interface IDatabaseProvider : ISqlExecutor
    {
        Task InitializeAsync();

        Task BatchAsync(IReadOnlyList<SqlStatement> statements);

        Task ApplySchemaAsync(Func<ISqlExecutor, Task> work);
    }
}
