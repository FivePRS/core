using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;

namespace FivePRS.Server.Database
{
    public sealed class Migration
    {
        public Migration(int version, string name, params MigrationStep[] steps)
        {
            Version = version;
            Name = name;
            Steps = steps;
        }

        public int Version { get; }

        public string Name { get; }

        public IReadOnlyList<MigrationStep> Steps { get; }
    }

    public abstract class MigrationStep
    {
        internal abstract Task ApplyAsync(DbConnection conn, DbTransaction? transaction, SqlDialect dialect);

        internal static async Task ExecuteAsync(DbConnection conn, DbTransaction? transaction, string sql)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        internal static async Task<object?> ScalarAsync(DbConnection conn, DbTransaction? transaction, string sql, string name, object value)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            var parameter = cmd.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            cmd.Parameters.Add(parameter);
            return await cmd.ExecuteScalarAsync();
        }
    }

    public sealed class SqlStep : MigrationStep
    {
        public SqlStep(string sqlite, string mySql)
        {
            Sqlite = sqlite;
            MySql = mySql;
        }

        public string Sqlite { get; }

        public string MySql { get; }

        internal override Task ApplyAsync(DbConnection conn, DbTransaction? transaction, SqlDialect dialect) =>
            ExecuteAsync(conn, transaction, dialect == SqlDialect.MySql ? MySql : Sqlite);
    }

    public sealed class AddColumnStep : MigrationStep
    {
        public AddColumnStep(string table, string column, string sqliteDefinition, string mySqlDefinition)
        {
            Table = table;
            Column = column;
            SqliteDefinition = sqliteDefinition;
            MySqlDefinition = mySqlDefinition;
        }

        public string Table { get; }

        public string Column { get; }

        public string SqliteDefinition { get; }

        public string MySqlDefinition { get; }

        internal override async Task ApplyAsync(DbConnection conn, DbTransaction? transaction, SqlDialect dialect)
        {
            var mySql = dialect == SqlDialect.MySql;
            var exists = await ScalarAsync(conn, transaction,
                mySql
                    ? $"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{Table}' AND COLUMN_NAME = @column"
                    : $"SELECT COUNT(*) FROM pragma_table_info('{Table}') WHERE name = @column",
                "@column", Column);
            if (Convert.ToInt64(exists) > 0) return;

            await ExecuteAsync(conn, transaction,
                mySql
                    ? $"ALTER TABLE `{Table}` ADD COLUMN `{Column}` {MySqlDefinition}"
                    : $"ALTER TABLE {Table} ADD COLUMN {Column} {SqliteDefinition}");
        }
    }
}
