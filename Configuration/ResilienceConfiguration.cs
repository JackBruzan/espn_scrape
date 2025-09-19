namespace ESPNScrape.Configuration
{
    /// <summary>
    /// Configuration settings for ESPN API resilience patterns including 
    /// retry policies, circuit breakers, rate limiting, and timeouts
    /// </summary>
    public class ResilienceConfiguration
    {
        /// <summary>
        /// Retry policy configuration for ESPN API calls
        /// </summary>
        public RetryPolicyConfig RetryPolicy { get; set; } = new();

        /// <summary>
        /// Circuit breaker configuration for ESPN API health monitoring
        /// </summary>
        public CircuitBreakerConfig CircuitBreaker { get; set; } = new();

        /// <summary>
        /// Rate limiting configuration to respect ESPN's API limits
        /// </summary>
        public RateLimitConfig RateLimit { get; set; } = new();

        /// <summary>
        /// Timeout configuration for different operation types
        /// </summary>
        public TimeoutConfig Timeouts { get; set; } = new();

        /// <summary>
        /// Health check configuration for ESPN API monitoring
        /// </summary>
        public HealthCheckConfig HealthCheck { get; set; } = new();
    }

    /// <summary>
    /// Retry policy configuration with exponential backoff and jitter
    /// </summary>
    public class RetryPolicyConfig
    {
        /// <summary>
        /// Maximum number of retry attempts for transient failures
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 3;

        /// <summary>
        /// Base delay for exponential backoff in milliseconds
        /// </summary>
        public int BaseDelayMs { get; set; } = 1000;

        /// <summary>
        /// Maximum delay cap for retry attempts in milliseconds
        /// </summary>
        public int MaxDelayMs { get; set; } = 30000;

        /// <summary>
        /// Enable jitter to prevent thundering herd problem
        /// </summary>
        public bool EnableJitter { get; set; } = true;

        /// <summary>
        /// Jitter factor (0.0 to 1.0) for randomizing retry delays
        /// </summary>
        public double JitterFactor { get; set; } = 0.1;

        /// <summary>
        /// HTTP status codes that should trigger retry attempts
        /// </summary>
        public List<int> RetryableStatusCodes { get; set; } = new() { 429, 502, 503, 504 };

        /// <summary>
        /// Enable retry on timeout exceptions
        /// </summary>
        public bool RetryOnTimeout { get; set; } = true;
    }

    /// <summary>
    /// Circuit breaker configuration for ESPN API protection
    /// </summary>
    public class CircuitBreakerConfig
    {
        /// <summary>
        /// Number of consecutive failures before opening the circuit
        /// </summary>
        public int FailureThreshold { get; set; } = 5;

        /// <summary>
        /// Duration to keep circuit open before attempting to close it (in seconds)
        /// </summary>
        public int OpenCircuitDurationSeconds { get; set; } = 60;

        /// <summary>
        /// Number of successful calls required to close an open circuit
        /// </summary>
        public int SuccessThreshold { get; set; } = 2;

        /// <summary>
        /// Minimum throughput required before circuit breaker activates
        /// </summary>
        public int MinimumThroughput { get; set; } = 10;

        /// <summary>
        /// Sampling duration for failure rate calculation (in seconds)
        /// </summary>
        public int SamplingDurationSeconds { get; set; } = 30;

        /// <summary>
        /// HTTP status codes that count as failures for circuit breaker
        /// </summary>
        public List<int> FailureStatusCodes { get; set; } = new() { 500, 502, 503, 504 };
    }

    /// <summary>
    /// Rate limiting configuration to respect ESPN's API limits
    /// </summary>
    public class RateLimitConfig
    {
        /// <summary>
        /// Maximum requests per time window
        /// </summary>
        public int MaxRequests { get; set; } = 100;

        /// <summary>
        /// Time window for rate limiting in seconds
        /// </summary>
        public int TimeWindowSeconds { get; set; } = 60;

        /// <summary>
        /// Enable distributed rate limiting across multiple instances
        /// </summary>
        public bool EnableDistributedLimiting { get; set; } = false;

        /// <summary>
        /// Burst allowance for rate limiting
        /// </summary>
        public int BurstAllowance { get; set; } = 10;

        /// <summary>
        /// Queue timeout for rate-limited requests in milliseconds
        /// </summary>
        public int QueueTimeoutMs { get; set; } = 5000;
    }

    /// <summary>
    /// Timeout configuration for different operation types
    /// </summary>
    public class TimeoutConfig
    {
        /// <summary>
        /// Default HTTP request timeout in seconds
        /// </summary>
        public int DefaultRequestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Timeout for scoreboard requests in seconds
        /// </summary>
        public int ScoreboardRequestTimeoutSeconds { get; set; } = 45;

        /// <summary>
        /// Timeout for box score requests in seconds
        /// </summary>
        public int BoxScoreRequestTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Timeout for player stats requests in seconds
        /// </summary>
        public int PlayerStatsRequestTimeoutSeconds { get; set; } = 90;

        /// <summary>
        /// Timeout for health check requests in seconds
        /// </summary>
        public int HealthCheckTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Overall operation timeout for bulk operations in minutes
        /// </summary>
        public int BulkOperationTimeoutMinutes { get; set; } = 30;
    }

    /// <summary>
    /// Health check configuration for ESPN API monitoring
    /// </summary>
    public class HealthCheckConfig
    {
        /// <summary>
        /// Interval between health checks in seconds
        /// </summary>
        public int CheckIntervalSeconds { get; set; } = 30;

        /// <summary>
        /// Number of consecutive failed health checks before marking as unhealthy
        /// </summary>
        public int FailureThreshold { get; set; } = 3;

        /// <summary>
        /// Enable detailed health check reporting
        /// </summary>
        public bool EnableDetailedReporting { get; set; } = true;

        /// <summary>
        /// Test endpoints for health checks
        /// </summary>
        public List<string> TestEndpoints { get; set; } = new() { "/nfl/", "/nfl/scoreboard" };

        /// <summary>
        /// Enable background health monitoring
        /// </summary>
        public bool EnableBackgroundMonitoring { get; set; } = true;

        /// <summary>
        /// Grace period for health check startup in seconds
        /// </summary>
        public int StartupGracePeriodSeconds { get; set; } = 120;
    }
}