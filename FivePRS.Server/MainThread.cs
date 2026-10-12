using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace FivePRS.Server
{
    public static class MainThread
    {
        private static readonly ConcurrentQueue<Action> Waiting = new();
        private static bool _enabled;
        private static int _threadId;

        public static void Enable() => _enabled = true;

        public static void Disable()
        {
            _enabled = false;
            _threadId = 0;
            Drain();
        }

        public static bool IsCurrent => !_enabled || (_threadId != 0 && Thread.CurrentThread.ManagedThreadId == _threadId);

        public static Awaitable Resume() => default;

        public static void Tick()
        {
            _threadId = Thread.CurrentThread.ManagedThreadId;
            Drain();
        }

        private static void Drain()
        {
            while (Waiting.TryDequeue(out var continuation))
                continuation();
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

        public readonly struct Awaitable : INotifyCompletion
        {
            public Awaitable GetAwaiter() => this;

            public bool IsCompleted => IsCurrent;

            public void OnCompleted(Action continuation) => Waiting.Enqueue(continuation);

            public void GetResult()
            {
            }
        }
    }
}
