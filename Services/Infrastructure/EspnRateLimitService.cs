using ESPNScrape.Configuration;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Rate limiting service implementation using sliding window algorithm
    /// </summary>
    public class EspnRateLimitService : IEspnRateLimitService
    {
        private readonly RateLimitConfig _config;
        private readonly ILogger<EspnRateLimitService> _logger;
        private readonly ConcurrentQueue<DateTime> _requestTimestamps;
        private readonly SemaphoreSlim _semaphore;
        private readonly object _lockObject = new object();

        public EspnRateLimitService(IOptions<ResilienceConfiguration> resilienceConfig, ILogger<EspnRateLimitService> logger)
        {
            _config = resilienceConfig.Value.RateLimit;
            _logger = logger;
            _requestTimestamps = new ConcurrentQueue<DateTime>();
            _semaphore = new SemaphoreSlim(_config.BurstAllowance, _config.BurstAllowance);
        }

        public async Task WaitForRequestAsync(CancellationToken cancellationToken = default)
        {
            bool semaphoreAcquired = false;
            try
            {
                // Wait for semaphore with timeout
                var acquired = await _semaphore.WaitAsync(_config.QueueTimeoutMs, cancellationToken);
                semaphoreAcquired = acquired;

                if (!acquired)
                {
                    _logger.LogWarning("Rate limit queue timeout exceeded after {TimeoutMs}ms", _config.QueueTimeoutMs);
                    throw new TimeoutException($"Rate limit queue timeout exceeded after {_config.QueueTimeoutMs}ms");
                }

                // Clean old timestamps and check rate limit
                CleanOldTimestamps();

                // Check if we need to wait
                TimeSpan waitTime = TimeSpan.Zero;
                bool needsWait = false;

                lock (_lockObject)
                {
                    if (_requestTimestamps.Count >= _config.MaxRequests)
                    {
                        var oldestRequest = _requestTimestamps.TryPeek(out var timestamp) ? timestamp : DateTime.UtcNow;
                        waitTime = TimeSpan.FromSeconds(_config.TimeWindowSeconds) - (DateTime.UtcNow - oldestRequest);
                        needsWait = waitTime > TimeSpan.Zero;
                    }
                }

                if (needsWait)
                {
                    _logger.LogDebug("Rate limit exceeded, waiting {WaitTime}ms", waitTime.TotalMilliseconds);

                    // Release semaphore and wait outside of lock
                    _semaphore.Release();
                    semaphoreAcquired = false; // Mark as released
                    await Task.Delay(waitTime, cancellationToken);

                    // Re-acquire semaphore
                    acquired = await _semaphore.WaitAsync(_config.QueueTimeoutMs, cancellationToken);
                    semaphoreAcquired = acquired;
                    if (!acquired)
                    {
                        throw new TimeoutException("Rate limit queue timeout exceeded during retry");
                    }

                    // Clean again after waiting
                    CleanOldTimestamps();
                }

                // Record this request
                lock (_lockObject)
                {
                    _requestTimestamps.Enqueue(DateTime.UtcNow);
                    _logger.LogDebug("Request allowed, {Count} requests in current window", _requestTimestamps.Count);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogDebug("Rate limit wait was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in rate limiting logic");
                throw;
            }
            finally
            {
                // Only release semaphore if we still have it acquired
                if (semaphoreAcquired)
                {
                    try
                    {
                        _semaphore.Release();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Service is being disposed, ignore
                    }
                    catch (SemaphoreFullException ex)
                    {
                        _logger.LogWarning(ex, "Attempted to release semaphore but it was already at maximum capacity");
                    }
                }
            }
        }

        public bool CanMakeRequest()
        {
            CleanOldTimestamps();

            lock (_lockObject)
            {
                var canMake = _requestTimestamps.Count < _config.MaxRequests;
                _logger.LogDebug("Rate limit check: {CurrentCount}/{MaxRequests}, can make request: {CanMake}",
                    _requestTimestamps.Count, _config.MaxRequests, canMake);
                return canMake;
            }
        }

        public RateLimitStatus GetStatus()
        {
            CleanOldTimestamps();

            lock (_lockObject)
            {
                var currentRequests = _requestTimestamps.Count;
                var windowStart = DateTime.UtcNow.AddSeconds(-_config.TimeWindowSeconds);
                var windowEnd = DateTime.UtcNow;
                var timeUntilReset = TimeSpan.Zero;

                if (_requestTimestamps.TryPeek(out var oldestRequest))
                {
                    timeUntilReset = TimeSpan.FromSeconds(_config.TimeWindowSeconds) - (DateTime.UtcNow - oldestRequest);
                    if (timeUntilReset < TimeSpan.Zero)
                        timeUntilReset = TimeSpan.Zero;
                }

                return new RateLimitStatus
                {
                    RequestsRemaining = Math.Max(0, _config.MaxRequests - currentRequests),
                    TotalRequests = currentRequests,
                    WindowStart = windowStart,
                    WindowEnd = windowEnd,
                    TimeUntilReset = timeUntilReset,
                    IsLimited = currentRequests >= _config.MaxRequests
                };
            }
        }

        public void Reset()
        {
            lock (_lockObject)
            {
                while (_requestTimestamps.TryDequeue(out _)) { }
                _logger.LogDebug("Rate limit counters reset");
            }
        }

        private void CleanOldTimestamps()
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-_config.TimeWindowSeconds);

            while (_requestTimestamps.TryPeek(out var timestamp) && timestamp < cutoff)
            {
                _requestTimestamps.TryDequeue(out _);
            }
        }

        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
}