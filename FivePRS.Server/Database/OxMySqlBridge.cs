using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using FivePRS.Core.Events;

namespace FivePRS.Server.Database
{
    internal sealed class OxMySqlBridge : IOxMySql
    {
        private const string Resource = "oxmysql";
        private const int QueryTimeoutMs = 30_000;
        private const int ConnectTimeoutMs = 75_000;
        private const int StartWaitMs = 15_000;

        private static readonly ConcurrentDictionary<string, TaskCompletionSource<string>> Pending = new();

        public async Task ConnectAsync()
        {
            await MainThread.Resume();
            if (string.IsNullOrWhiteSpace(API.GetConvar("mysql_connection_string", "")))
                throw new DatabaseSetupException(
                    "fiveprs_db_type is mysql, but mysql_connection_string isn't set. FivePRS uses oxmysql for MySQL and MariaDB, and oxmysql reads its connection from that convar. " +
                    "Add set mysql_connection_string \"mysql://user:password@localhost/fiveprs\" to server.cfg, above ensure oxmysql.");

            var waited = 0;
            while (API.GetResourceState(Resource) != "started")
            {
                var state = API.GetResourceState(Resource);
                if (state == "missing")
                    throw new DatabaseSetupException("fiveprs_db_type is mysql, but oxmysql isn't installed. Download it from https://github.com/overextended/oxmysql/releases and add ensure oxmysql above ensure fiveprs in server.cfg.");
                if (waited >= StartWaitMs)
                    throw new DatabaseSetupException($"fiveprs_db_type is mysql, but oxmysql is {state}, not started. Add ensure oxmysql above ensure fiveprs in server.cfg.");

                await BaseScript.Delay(500);
                await MainThread.Resume();
                waited += 500;
            }

            try
            {
                await RequestAsync("connect", string.Empty, "[]", ConnectTimeoutMs);
            }
            catch (TimeoutException)
            {
                throw new DatabaseSetupException("oxmysql couldn't connect to the database. Check mysql_connection_string in server.cfg; oxmysql's own messages above say what went wrong.");
            }
        }

        public Task<string> SendAsync(string kind, string sql, string parameters) =>
            RequestAsync(kind, sql, parameters, QueryTimeoutMs);

        private static async Task<string> RequestAsync(string kind, string sql, string parameters, int timeoutMs)
        {
            await MainThread.Resume();

            var id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<string>();
            Pending[id] = completion;

            try
            {
                BaseScript.TriggerEvent(EventNames.LocalDbRequest, id, kind, sql, parameters);

                var finished = await Task.WhenAny(completion.Task, BaseScript.Delay(timeoutMs));
                await MainThread.Resume();
                if (finished != completion.Task)
                    throw new TimeoutException($"oxmysql didn't answer within {timeoutMs / 1000} seconds.");

                return await completion.Task;
            }
            finally
            {
                Pending.TryRemove(id, out _);
            }
        }

        public static void OnResponse(string id, string result, string error)
        {
            if (id is null || !Pending.TryGetValue(id, out var completion)) return;

            if (string.IsNullOrEmpty(error))
                completion.TrySetResult(result ?? string.Empty);
            else
                completion.TrySetException(new DatabaseQueryException(error));
        }
    }

    public sealed class DatabaseQueryException : Exception
    {
        public DatabaseQueryException(string message) : base(message)
        {
        }
    }
}
