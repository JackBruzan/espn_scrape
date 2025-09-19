using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.Configuration;

namespace ESPNScrape.HealthChecks
{
    public class EspnApiHealthCheck : IHealthCheck
    {
        private readonly IEspnHttpService _espnHttpService;
        private readonly IEspnRateLimitService _rateLimitService;
        private readonly ILogger<EspnApiHealthCheck> _logger;
        private readonly ResilienceConfiguration _config;

        public EspnApiHealthCheck(
            IEspnHttpService espnHttpService,
            IEspnRateLimitService rateLimitService,
            ILogger<EspnApiHealthCheck> logger,
            IOptions<ResilienceConfiguration> resilienceConfig)
        {
            _espnHttpService = espnHttpService;
            _rateLimitService = rateLimitService;
            _logger = logger;
            _config = resilienceConfig.Value;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var data = new Dictionary<string, object>();
            var healthStatus = HealthStatus.Healthy;
            var issues = new List<string>();

            try
            {
                _logger.LogDebug("Performing comprehensive ESPN API health check");

                // Check rate limiting status
                var rateLimitStatus = _rateLimitService.GetStatus();
                data["RateLimit"] = new
                {
                    RequestsRemaining = rateLimitStatus.RequestsRemaining,
                    TotalRequests = rateLimitStatus.TotalRequests,
                    IsLimited = rateLimitStatus.IsLimited,
                    TimeUntilReset = rateLimitStatus.TimeUntilReset.ToString()
                };

                if (rateLimitStatus.IsLimited)
                {
                    issues.Add("Rate limit exceeded");
                    healthStatus = HealthStatus.Degraded;
                }

                // Test multiple endpoints for comprehensive health check
                var endpointResults = new List<object>();

                foreach (var endpoint in _config.HealthCheck.TestEndpoints)
                {
                    var endpointResult = await TestEndpoint(endpoint, cancellationToken);
                    endpointResults.Add(endpointResult);

                    if (!endpointResult.Success)
                    {
                        issues.Add($"Endpoint {endpoint} failed: {endpointResult.Error}");
                        healthStatus = HealthStatus.Unhealthy;
                    }
                }

                data["EndpointTests"] = endpointResults;
                data["TotalIssues"] = issues.Count;

                if (healthStatus == HealthStatus.Healthy)
                {
                    _logger.LogDebug("ESPN API health check successful");
                    return HealthCheckResult.Healthy("ESPN API is responding successfully", data);
                }
                else if (healthStatus == HealthStatus.Degraded)
                {
                    _logger.LogWarning("ESPN API health check degraded: {Issues}", string.Join(", ", issues));
                    return HealthCheckResult.Degraded($"ESPN API degraded: {string.Join(", ", issues)}", null, data);
                }
                else
                {
                    _logger.LogError("ESPN API health check failed: {Issues}", string.Join(", ", issues));
                    return HealthCheckResult.Unhealthy($"ESPN API unhealthy: {string.Join(", ", issues)}", null, data);
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "ESPN API health check failed with HTTP error");
                data["Error"] = httpEx.Message;
                return HealthCheckResult.Unhealthy($"ESPN API HTTP error: {httpEx.Message}", httpEx, data);
            }
            catch (TaskCanceledException timeoutEx)
            {
                _logger.LogError(timeoutEx, "ESPN API health check timed out");
                data["Error"] = "Request timeout";
                return HealthCheckResult.Unhealthy("ESPN API request timed out", timeoutEx, data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ESPN API health check failed with unexpected error");
                data["Error"] = ex.Message;
                return HealthCheckResult.Unhealthy($"ESPN API unexpected error: {ex.Message}", ex, data);
            }
        }

        private async Task<EndpointTestResult> TestEndpoint(string endpoint, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Use a shorter timeout for health checks
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.Timeouts.HealthCheckTimeoutSeconds));
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                var response = await _espnHttpService.GetRawJsonAsync(endpoint, combinedCts.Token);
                stopwatch.Stop();

                var success = !string.IsNullOrEmpty(response);

                return new EndpointTestResult
                {
                    Endpoint = endpoint,
                    Success = success,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    ResponseLength = response?.Length ?? 0,
                    Error = success ? null : "Empty response"
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new EndpointTestResult
                {
                    Endpoint = endpoint,
                    Success = false,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    ResponseLength = 0,
                    Error = ex.Message
                };
            }
        }

        private class EndpointTestResult
        {
            public string Endpoint { get; set; } = string.Empty;
            public bool Success { get; set; }
            public long ResponseTimeMs { get; set; }
            public int ResponseLength { get; set; }
            public string? Error { get; set; }
        }
    }
}