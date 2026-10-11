using System.Threading.Tasks;
using FivePRS.Server;
using Xunit;

namespace FivePRS.Tests
{
    [Collection(TestDatabase.Collection)]
    public class MainThreadTests
    {
        [Fact]
        public void Resume_NotEnabled_CompletesImmediately()
        {
            Assert.True(MainThread.Resume().IsCompleted);
        }

        [Fact]
        public void Resume_OnTickThread_CompletesImmediately()
        {
            MainThread.Enable();
            try
            {
                MainThread.Tick();
                Assert.True(MainThread.Resume().IsCompleted);
            }
            finally
            {
                MainThread.Disable();
            }
        }

        [Fact]
        public async Task Run_BeforeFirstTick_WaitsForTick()
        {
            MainThread.Enable();
            try
            {
                var run = MainThread.Run(() => Task.FromResult(42));
                Assert.False(run.IsCompleted);

                MainThread.Tick();

                Assert.Equal(42, await run);
            }
            finally
            {
                MainThread.Disable();
            }
        }

        [Fact]
        public async Task Resume_FromAnotherThread_ContinuesOnTheTickThread()
        {
            MainThread.Enable();
            try
            {
                MainThread.Tick();
                var continuedOn = 0;
                var resumed = Task.Run(async () =>
                {
                    await MainThread.Resume();
                    continuedOn = System.Threading.Thread.CurrentThread.ManagedThreadId;
                });
                await Task.Delay(100);
                Assert.False(resumed.IsCompleted);

                var tickThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                MainThread.Tick();
                await resumed;

                Assert.Equal(tickThread, continuedOn);
            }
            finally
            {
                MainThread.Disable();
            }
        }

        [Fact]
        public async Task Run_FromAnotherThread_FinishesOnlyAfterTick()
        {
            MainThread.Enable();
            try
            {
                MainThread.Tick();
                var run = Task.Run(() => MainThread.Run(() => Task.FromResult(7)));
                await Task.Delay(100);
                Assert.False(run.IsCompleted);

                MainThread.Tick();

                Assert.Equal(7, await run);
            }
            finally
            {
                MainThread.Disable();
            }
        }
    }
}
