using System;

namespace FivePRS.Server.Database
{
    public sealed class DatabaseSetupException : Exception
    {
        public DatabaseSetupException(string message) : base(message)
        {
        }
    }
}
