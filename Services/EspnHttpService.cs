using ESPNScrape.Configuration;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using System.Net;
using System.Text.Json;

namespace ESPNScrape.Services
{
    public class EspnHttpService : IEspnHttpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EspnHttpService> _logger;
        private readonly IEspnRateLimitService _rateLimitService;
        private readonly ResilienceConfiguration _config;
        private readonly IAsyncPolicy _retryPolicy;
        private readonly IAsyncPolicy _circuitBreakerPolicy;
        private readonly IAsyncPolicy _combinedPolicy;
        private readonly Random _jitterRandom = new Random();

        private const string BaseUrl = "https://www.espn.com";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

        public EspnHttpService(
            HttpClient httpClient,
            ILogger<EspnHttpService> logger,
            IEspnRateLimitService rateLimitService,
            IOptions<ResilienceConfiguration> resilienceConfig)
        {
            _httpClient = httpClient;
            _logger = logger;
            _rateLimitService = rateLimitService;
            _config = resilienceConfig.Value;

            // Configure headers and timeout
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            _httpClient.Timeout = TimeSpan.FromSeconds(_config.Timeouts.DefaultRequestTimeoutSeconds);

            // Configure retry policy with jitter
            _retryPolicy = CreateRetryPolicy();

            // Configure circuit breaker policy
            _circuitBreakerPolicy = CreateCircuitBreakerPolicy();

            // Combine policies: Circuit Breaker wraps Retry
            _combinedPolicy = Policy.WrapAsync(_circuitBreakerPolicy, _retryPolicy);
        }

        public async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            var json = await GetRawJsonAsync(endpoint, cancellationToken);
            return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
        }

        public async Task<string> GetRawJsonAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var fullUrl = endpoint.StartsWith("http") ? endpoint : $"{BaseUrl}{endpoint}";

            // Apply rate limiting before making request
            await _rateLimitService.WaitForRequestAsync(cancellationToken);

            return await _combinedPolicy.ExecuteAsync(async (context) =>
            {
                try
                {
                    _logger.LogDebug("Making HTTP request to {Url}", fullUrl);

                    var response = await _httpClient.GetAsync(fullUrl, cancellationToken);

                    // Handle specific HTTP status codes
                    if (!response.IsSuccessStatusCode)
                    {
                        var statusCode = (int)response.StatusCode;
                        if (_config.RetryPolicy.RetryableStatusCodes.Contains(statusCode))
                        {
                            _logger.LogWarning("Received retryable status code {StatusCode} for {Url}", statusCode, fullUrl);
                            throw new HttpRequestException($"HTTP {statusCode}: {response.ReasonPhrase}");
                        }

                        if (_config.CircuitBreaker.FailureStatusCodes.Contains(statusCode))
                        {
                            _logger.LogError("Received failure status code {StatusCode} for {Url}", statusCode, fullUrl);
                            throw new HttpRequestException($"HTTP {statusCode}: {response.ReasonPhrase}");
                        }

                        // For other status codes, don't retry but still throw
                        response.EnsureSuccessStatusCode();
                    }

                    var content = await response.Content.ReadAsStringAsync(cancellationToken);

                    _logger.LogDebug("ESPN HTTP request successful. Response length: {Length} characters", content.Length);

                    return content;
                }
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    _logger.LogWarning("ESPN HTTP request timed out for {Url}", fullUrl);
                    throw new HttpRequestException($"Request timeout for {fullUrl}", ex);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ESPN HTTP request failed for {Url}", fullUrl);
                    throw;
                }
            }, new Context { ["endpoint"] = fullUrl });
        }

        public async Task<T> GetFromReferenceAsync<T>(string referenceUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(referenceUrl))
                throw new ArgumentException("Reference URL cannot be null or empty", nameof(referenceUrl));

            // ESPN $ref URLs are direct API endpoints
            return await GetAsync<T>(referenceUrl, cancellationToken);
        }

        private IAsyncPolicy CreateRetryPolicy()
        {
            var retryConfig = _config.RetryPolicy;

            return Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>(ex => retryConfig.RetryOnTimeout)
                .WaitAndRetryAsync(
                    retryCount: retryConfig.MaxRetryAttempts,
                    sleepDurationProvider: retryAttempt => CalculateRetryDelay(retryAttempt, retryConfig),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        var endpoint = context.GetValueOrDefault("endpoint", "unknown");
                        _logger.LogWarning("ESPN HTTP retry attempt {RetryCount}/{MaxRetries} after {Delay}ms for {Endpoint}. Reason: {Reason}",
                            retryCount, retryConfig.MaxRetryAttempts, timespan.TotalMilliseconds, endpoint, outcome.Message);
                    });
        }

        private IAsyncPolicy CreateCircuitBreakerPolicy()
        {
            var cbConfig = _config.CircuitBreaker;

            return Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.5, // 50% failure rate
                    samplingDuration: TimeSpan.FromSeconds(cbConfig.SamplingDurationSeconds),
                    minimumThroughput: cbConfig.MinimumThroughput,
                    durationOfBreak: TimeSpan.FromSeconds(cbConfig.OpenCircuitDurationSeconds),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogError("ESPN API circuit breaker opened for {Duration}s due to: {Exception}",
                            duration.TotalSeconds, exception.Message);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("ESPN API circuit breaker reset - service recovered");
                    },
                    onHalfOpen: () =>
                    {
                        _logger.LogInformation("ESPN API circuit breaker half-open - testing service");
                    });
        }

        private TimeSpan CalculateRetryDelay(int retryAttempt, RetryPolicyConfig config)
        {
            // Calculate exponential backoff
            var baseDelay = TimeSpan.FromMilliseconds(config.BaseDelayMs);
            var exponentialDelay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * Math.Pow(2, retryAttempt - 1));

            // Apply maximum delay cap
            var cappedDelay = exponentialDelay.TotalMilliseconds > config.MaxDelayMs
                ? TimeSpan.FromMilliseconds(config.MaxDelayMs)
                : exponentialDelay;

            // Add jitter if enabled
            if (config.EnableJitter)
            {
                var jitterRange = cappedDelay.TotalMilliseconds * config.JitterFactor;
                var jitter = (_jitterRandom.NextDouble() - 0.5) * 2 * jitterRange;
                cappedDelay = TimeSpan.FromMilliseconds(Math.Max(0, cappedDelay.TotalMilliseconds + jitter));
            }

            _logger.LogDebug("Calculated retry delay for attempt {Attempt}: {Delay}ms", retryAttempt, cappedDelay.TotalMilliseconds);

            return cappedDelay;
        }
    }
}