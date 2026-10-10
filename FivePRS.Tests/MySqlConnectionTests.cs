using System;
using FivePRS.Server.Database;
using MySqlConnector;
using Xunit;

namespace FivePRS.Tests
{
    public class MySqlConnectionTests
    {
        private const string Base = "Server=127.0.0.1;Port=3306;Database=fiveprs;User ID=fivem;Password=secret;";

        [Fact]
        public void Prepare_NoSslMode_DisablesTlsAndAllowsKeyRetrieval()
        {
            var builder = new MySqlConnectionStringBuilder(MySqlProvider.PrepareConnectionString(Base));

            Assert.Equal(MySqlSslMode.None, builder.SslMode);
            Assert.True(builder.AllowPublicKeyRetrieval);
            Assert.Equal("fiveprs", builder.Database);
        }

        [Theory]
        [InlineData("SslMode=None;")]
        [InlineData("Ssl Mode=none;")]
        [InlineData("sslmode=disabled;")]
        public void Prepare_SslModeNone_IsKept(string ssl)
        {
            var builder = new MySqlConnectionStringBuilder(MySqlProvider.PrepareConnectionString(Base + ssl));

            Assert.Equal(MySqlSslMode.None, builder.SslMode);
        }

        [Theory]
        [InlineData("SslMode=Required;")]
        [InlineData("SslMode=Preferred;")]
        [InlineData("Ssl Mode=VerifyFull;")]
        public void Prepare_TlsRequested_ExplainsItIsUnsupported(string ssl)
        {
            var error = Assert.Throws<NotSupportedException>(() => MySqlProvider.PrepareConnectionString(Base + ssl));

            Assert.Contains("SslMode=None", error.Message);
        }

        [Fact]
        public void Prepare_ExplicitKeyRetrieval_IsKept()
        {
            var builder = new MySqlConnectionStringBuilder(MySqlProvider.PrepareConnectionString(Base + "AllowPublicKeyRetrieval=false;"));

            Assert.False(builder.AllowPublicKeyRetrieval);
        }
    }
}
