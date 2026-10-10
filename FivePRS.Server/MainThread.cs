using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace FivePRS.Server
{
    public static class MainThread
    {
        private static readonly ConcurrentQueue<TaskCompletionSource<bool>> Waiting = new();
        private static int _threadId;

        public static void Attach() => _threadId = Thread.CurrentThread.ManagedThreadId;

        public static void Detach() => _threadId = 0;

        public static bool IsCurrent => _threadId == 0 || Thread.CurrentThread.ManagedThreadId == _threadId;

        public static Task Resume()
        {
            if (IsCurrent) return Task.FromResult(true);

            var completion = new TaskCompletionSource<bool>();
            Waiting.Enqueue(completion);
            return completion.Task;
        }

        public static void Drain()
        {
            while (Waiting.TryDequeue(out var completion))
                completion.TrySetResult(true);
        }

        public static async Task Run(Func<Task> work)
        {
            try
            {
                await work();
            }
            finally
            {
                await Resume();
            }
        }

        public static async Task<T> Run<T>(Func<Task<T>> work)
        {
            try
            {
                return await work();
            }
            finally
            {
                await Resume();
            }
        }
    }
}
