using Microsoft.Extensions.Diagnostics.HealthChecks;
using ESPNScrape.Services;

namespace ESPNScrape.HealthChecks
{
    public class EspnApiHealthCheck : IHealthCheck
    {
        private readonly IEspnHttpService _espnHttpService;
        private readonly ILogger<EspnApiHealthCheck> _logger;

        public EspnApiHealthCheck(IEspnHttpService espnHttpService, ILogger<EspnApiHealthCheck> logger)
        {
            _espnHttpService = espnHttpService;
            _logger = logger;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Performing ESPN API health check");

                // Test ESPN API connectivity by making a simple request to their main NFL page
                // This endpoint is lightweight and doesn't require specific parameters
                var testEndpoint = "/nfl/";
                var response = await _espnHttpService.GetRawJsonAsync(testEndpoint, cancellationToken);

                if (!string.IsNullOrEmpty(response))
                {
                    _logger.LogDebug("ESPN API health check successful");
                    return HealthCheckResult.Healthy("ESPN API is responding successfully");
                }

                _logger.LogWarning("ESPN API returned empty response");
                return HealthCheckResult.Degraded("ESPN API returned empty response");
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "ESPN API health check failed with HTTP error");
                return HealthCheckResult.Unhealthy($"ESPN API HTTP error: {httpEx.Message}", httpEx);
            }
            catch (TaskCanceledException timeoutEx)
            {
                _logger.LogError(timeoutEx, "ESPN API health check timed out");
                return HealthCheckResult.Unhealthy("ESPN API request timed out", timeoutEx);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ESPN API health check failed with unexpected error");
                return HealthCheckResult.Unhealthy($"ESPN API unexpected error: {ex.Message}", ex);
            }
        }
    }
}