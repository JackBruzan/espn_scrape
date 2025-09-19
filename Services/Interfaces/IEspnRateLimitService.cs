using ESPNScrape.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Rate limiting service interface for controlling ESPN API request frequency
    /// </summary>
    public interface IEspnRateLimitService
    {
        /// <summary>
        /// Waits if necessary to ensure rate limit compliance before allowing request
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Task that completes when request is allowed</returns>
        Task WaitForRequestAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Checks if a request can be made immediately without waiting
        /// </summary>
        /// <returns>True if request is allowed, false if rate limited</returns>
        bool CanMakeRequest();

        /// <summary>
        /// Gets current rate limit status information
        /// </summary>
        /// <returns>Rate limit status details</returns>
        RateLimitStatus GetStatus();

        /// <summary>
        /// Resets the rate limit counters (for testing purposes)
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// Current rate limit status information
    /// </summary>
    public class RateLimitStatus
    {
        public int RequestsRemaining { get; set; }
        public int TotalRequests { get; set; }
        public DateTime WindowStart { get; set; }
        public DateTime WindowEnd { get; set; }
        public TimeSpan TimeUntilReset { get; set; }
        public bool IsLimited { get; set; }
    }
}