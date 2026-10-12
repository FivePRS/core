using System;
using System.Collections.Generic;
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
        internal abstract Task ApplyAsync(ISqlExecutor db);
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

        internal override async Task ApplyAsync(ISqlExecutor db)
        {
            foreach (var statement in SqlStatement.Split(db.Dialect == SqlDialect.MySql ? MySql : Sqlite))
                await db.ExecuteAsync(statement);
        }
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

        internal override async Task ApplyAsync(ISqlExecutor db)
        {
            var mySql = db.Dialect == SqlDialect.MySql;
            var rows = await db.QueryAsync(
                mySql
                    ? $"SELECT COUNT(*) AS found FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{Table}' AND COLUMN_NAME = @column"
                    : $"SELECT COUNT(*) AS found FROM pragma_table_info('{Table}') WHERE name = @column",
                ("@column", Column));
            if (rows.Count > 0 && rows[0].GetInt64("found") > 0) return;

            await db.ExecuteAsync(
                mySql
                    ? $"ALTER TABLE `{Table}` ADD COLUMN `{Column}` {MySqlDefinition}"
                    : $"ALTER TABLE {Table} ADD COLUMN {Column} {SqliteDefinition}");
        }
    }
}
