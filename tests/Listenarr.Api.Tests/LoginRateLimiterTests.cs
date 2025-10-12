using System.Threading;
using Listenarr.Api.Services;
using Xunit;

namespace Listenarr.Api.Tests
{
    public class LoginRateLimiterTests
    {
        [Fact]
        public void RecordsFailuresAndBlocks()
        {
            var limiter = new LoginRateLimiter();
            var key = "1.2.3.4:alice";

            // Default configured max is 5; record 5 failures
            for (int i = 0; i < 5; i++) limiter.RecordFailure(key);

            Assert.True(limiter.IsBlocked(key));
            var secs = limiter.GetSecondsUntilUnblock(key);
            Assert.True(secs > 0, "Expected remaining block seconds to be > 0");

            // Record success should clear the block
            limiter.RecordSuccess(key);
            Assert.False(limiter.IsBlocked(key));
            Assert.Equal(0, limiter.GetSecondsUntilUnblock(key));
        }
    }
}
