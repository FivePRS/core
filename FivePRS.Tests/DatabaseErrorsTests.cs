using FivePRS.Server.Database;
using MySqlConnector;
using Xunit;

namespace FivePRS.Tests
{
    public class DatabaseErrorsTests
    {
        private const string Connection = "Server=127.0.0.1;Port=3306;Database=fiveprs;User ID=fivem;Password=secret;";

        [Fact]
        public void UnknownDatabase_NamesTheDatabaseAndTheFix()
        {
            var message = DatabaseErrors.Describe((int)MySqlErrorCode.UnknownDatabase, Connection)!;

            Assert.Contains("'fiveprs' doesn't exist", message);
            Assert.Contains("CREATE DATABASE fiveprs", message);
        }

        [Fact]
        public void AccessDenied_NamesTheUser()
        {
            Assert.Contains("'fivem'", DatabaseErrors.Describe((int)MySqlErrorCode.AccessDenied, Connection));
        }

        [Fact]
        public void DatabaseAccessDenied_SuggestsAGrant()
        {
            Assert.Contains("GRANT ALL PRIVILEGES ON fiveprs.* TO 'fivem'@'localhost'", DatabaseErrors.Describe((int)MySqlErrorCode.DatabaseAccessDenied, Connection));
        }

        [Fact]
        public void UnableToConnect_NamesTheServer()
        {
            Assert.Contains("127.0.0.1:3306", DatabaseErrors.Describe((int)MySqlErrorCode.UnableToConnectToHost, Connection));
        }

        [Fact]
        public void OtherErrorsAndBadConnectionStrings_FallBack()
        {
            Assert.Null(DatabaseErrors.Describe((int)MySqlErrorCode.LockDeadlock, Connection));
            Assert.Contains("the database", DatabaseErrors.Describe((int)MySqlErrorCode.UnknownDatabase, "not a connection string")!);
        }
    }
}
