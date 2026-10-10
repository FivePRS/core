using System;
using MySqlConnector;

namespace FivePRS.Server.Database
{
    public static class DatabaseErrors
    {
        public static string? Describe(Exception ex, string? connectionString)
        {
            var mySql = ex as MySqlException ?? ex.InnerException as MySqlException;
            return mySql is null ? null : Describe(mySql.Number, connectionString);
        }

        public static string? Describe(int errorNumber, string? connectionString)
        {
            var settings = Settings(connectionString);
            var database = string.IsNullOrEmpty(settings?.Database) ? "the database" : $"'{settings!.Database}'";
            var user     = string.IsNullOrEmpty(settings?.UserID) ? "the user" : $"'{settings!.UserID}'";
            var server   = settings is null ? "the database server" : $"{settings.Server}:{settings.Port}";

            return (MySqlErrorCode)errorNumber switch
            {
                MySqlErrorCode.UnknownDatabase =>
                    $"The database {database} doesn't exist. Create it (CREATE DATABASE {Name(settings?.Database)} CHARACTER SET utf8mb4;), or fix Database= in fiveprs_db_connection.",
                MySqlErrorCode.AccessDenied =>
                    $"The database server rejected the login for {user}. Check User ID and Password in fiveprs_db_connection.",
                MySqlErrorCode.DatabaseAccessDenied =>
                    $"The user {user} isn't allowed to use {database}. Grant access (GRANT ALL PRIVILEGES ON {Name(settings?.Database)}.* TO '{settings?.UserID ?? "your_user"}'@'localhost';) or use another user.",
                MySqlErrorCode.UnableToConnectToHost =>
                    $"Couldn't reach the database server at {server}. Check that MySQL or MariaDB is running and that Server and Port in fiveprs_db_connection are right.",
                _ => null,
            };
        }

        private static string Name(string? value) => string.IsNullOrEmpty(value) ? "fiveprs" : value!;

        private static MySqlConnectionStringBuilder? Settings(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return null;

            try
            {
                return new MySqlConnectionStringBuilder(connectionString!);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
