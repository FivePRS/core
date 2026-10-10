using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CitizenFX.Core;
using FivePRS.Core.Events;

namespace FivePRS.Server.Http
{
    internal sealed class HttpResult
    {
        public int Status { get; set; }
        public string Body { get; set; } = string.Empty;
        public string Error { get; set; } = string.Empty;
        public bool Ok => Error.Length == 0 && Status >= 200 && Status < 300;
    }

    internal static class HttpBridge
    {
        private const int DefaultTimeoutMs = 60_000;

        private static readonly Dictionary<string, TaskCompletionSource<HttpResult>> Pending = new();

        public static async Task<HttpResult> GetAsync(string url, string savePath = "", string accept = "", string check = "", int timeoutMs = DefaultTimeoutMs)
        {
            var id = Guid.NewGuid().ToString("N");
            var completion = new TaskCompletionSource<HttpResult>();
            Pending[id] = completion;

            BaseScript.TriggerEvent(EventNames.LocalHttpRequest, id, url, savePath, accept, check);

            var finished = await Task.WhenAny(completion.Task, BaseScript.Delay(timeoutMs));
            Pending.Remove(id);

            return finished == completion.Task ? completion.Task.Result : new HttpResult { Error = "timed out" };
        }

        public static void OnResponse(string id, int status, string body, string error)
        {
            if (id is not null && Pending.TryGetValue(id, out var completion))
                completion.TrySetResult(new HttpResult { Status = status, Body = body ?? string.Empty, Error = error ?? string.Empty });
        }
    }
}
