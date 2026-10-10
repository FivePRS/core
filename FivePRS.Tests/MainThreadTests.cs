using System.Threading.Tasks;
using FivePRS.Server;
using Xunit;

namespace FivePRS.Tests
{
    [Collection(TestDatabase.Collection)]
    public class MainThreadTests
    {
        [Fact]
        public void Resume_NotAttached_CompletesImmediately()
        {
            Assert.True(MainThread.Resume().IsCompleted);
        }

        [Fact]
        public void Resume_OnMainThread_CompletesImmediately()
        {
            MainThread.Attach();
            try
            {
                Assert.True(MainThread.Resume().IsCompleted);
            }
            finally
            {
                MainThread.Detach();
            }
        }

        [Fact]
        public async Task Run_FromAnotherThread_FinishesOnlyAfterDrain()
        {
            MainThread.Attach();
            try
            {
                var run = Task.Run(() => MainThread.Run(() => Task.FromResult(42)));
                await Task.Delay(100);
                Assert.False(run.IsCompleted);

                MainThread.Drain();

                Assert.Equal(42, await run);
            }
            finally
            {
                MainThread.Detach();
            }
        }
    }
}
