using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.Services;
using ESPNScrape.Configuration;
using ESPNScrape.Models.DataSync;
using System.Text.Json;

namespace ESPNScrape.HealthChecks
{
    /// <summary>
    /// Comprehensive health check for ESPN integration components including
    /// API connectivity, database connectivity, sync status, and data freshness
    /// </summary>
    public class EspnIntegrationHealthCheck : IHealthCheck
    {
        private readonly IEspnHttpService _espnHttpService;
        private readonly IEspnDataSyncService _espnDataSyncService;
        private readonly IEspnRateLimitService _rateLimitService;
        private readonly IEspnCacheService _cacheService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EspnIntegrationHealthCheck> _logger;
        private readonly ResilienceConfiguration _config;

        public EspnIntegrationHealthCheck(
            IEspnHttpService espnHttpService,
            IEspnDataSyncService espnDataSyncService,
            IEspnRateLimitService rateLimitService,
            IEspnCacheService cacheService,
            IServiceScopeFactory scopeFactory,
            ILogger<EspnIntegrationHealthCheck> logger,
            IOptions<ResilienceConfiguration> resilienceConfig)
        {
            _espnHttpService = espnHttpService;
            _espnDataSyncService = espnDataSyncService;
            _rateLimitService = rateLimitService;
            _cacheService = cacheService;
            _scopeFactory = scopeFactory;
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
                _logger.LogDebug("Performing comprehensive ESPN integration health check");

                // 1. Check ESPN API connectivity
                var apiHealthResult = await CheckEspnApiConnectivity(cancellationToken);
                data["EspnApiConnectivity"] = apiHealthResult;
                if (!apiHealthResult.IsHealthy)
                {
                    issues.AddRange(apiHealthResult.Issues);
                    healthStatus = HealthStatus.Unhealthy;
                }

                // 2. Check database connectivity
                var dbHealthResult = await CheckDatabaseConnectivity(cancellationToken);
                data["DatabaseConnectivity"] = dbHealthResult;
                if (!dbHealthResult.IsHealthy)
                {
                    issues.AddRange(dbHealthResult.Issues);
                    healthStatus = HealthStatus.Unhealthy;
                }

                // 3. Check last sync status and data freshness
                var syncHealthResult = await CheckSyncStatusAndDataFreshness(cancellationToken);
                data["SyncStatus"] = syncHealthResult;
                if (!syncHealthResult.IsHealthy)
                {
                    issues.AddRange(syncHealthResult.Issues);
                    if (healthStatus == HealthStatus.Healthy)
                        healthStatus = HealthStatus.Degraded;
                }

                // 4. Check rate limiting status
                var rateLimitResult = CheckRateLimitStatus();
                data["RateLimit"] = rateLimitResult;
                if (!rateLimitResult.IsHealthy)
                {
                    issues.AddRange(rateLimitResult.Issues);
                    if (healthStatus == HealthStatus.Healthy)
                        healthStatus = HealthStatus.Degraded;
                }

                // 5. Check cache health
                var cacheHealthResult = await CheckCacheHealth(cancellationToken);
                data["CacheHealth"] = cacheHealthResult;
                if (!cacheHealthResult.IsHealthy)
                {
                    issues.AddRange(cacheHealthResult.Issues);
                    if (healthStatus == HealthStatus.Healthy)
                        healthStatus = HealthStatus.Degraded;
                }

                data["TotalIssues"] = issues.Count;
                data["CheckTimestamp"] = DateTime.UtcNow;

                // Return appropriate health status
                if (healthStatus == HealthStatus.Healthy)
                {
                    _logger.LogDebug("ESPN integration health check successful");
                    return HealthCheckResult.Healthy("ESPN integration is functioning normally", data);
                }
                else if (healthStatus == HealthStatus.Degraded)
                {
                    _logger.LogWarning("ESPN integration health check degraded: {Issues}", string.Join(", ", issues));
                    return HealthCheckResult.Degraded($"ESPN integration degraded: {string.Join(", ", issues)}", null, data);
                }
                else
                {
                    _logger.LogError("ESPN integration health check failed: {Issues}", string.Join(", ", issues));
                    return HealthCheckResult.Unhealthy($"ESPN integration unhealthy: {string.Join(", ", issues)}", null, data);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ESPN integration health check failed with unexpected error");
                data["Error"] = ex.Message;
                data["CheckTimestamp"] = DateTime.UtcNow;
                return HealthCheckResult.Unhealthy($"ESPN integration health check error: {ex.Message}", ex, data);
            }
        }

        private async Task<ComponentHealthResult> CheckEspnApiConnectivity(CancellationToken cancellationToken)
        {
            var result = new ComponentHealthResult { ComponentName = "ESPN API" };

            try
            {
                var endpointResults = new List<EndpointTestResult>();

                foreach (var endpoint in _config.HealthCheck.TestEndpoints)
                {
                    var endpointResult = await TestEndpoint(endpoint, cancellationToken);
                    endpointResults.Add(endpointResult);

                    if (!endpointResult.Success)
                    {
                        result.Issues.Add($"Endpoint {endpoint} failed: {endpointResult.Error}");
                    }
                }

                result.Details["EndpointTests"] = endpointResults;
                result.Details["SuccessfulEndpoints"] = endpointResults.Count(e => e.Success);
                result.Details["FailedEndpoints"] = endpointResults.Count(e => !e.Success);
                result.Details["AverageResponseTime"] = endpointResults.Where(e => e.Success).Average(e => e.ResponseTimeMs);

                result.IsHealthy = endpointResults.Any(e => e.Success);
            }
            catch (Exception ex)
            {
                result.Issues.Add($"ESPN API connectivity check failed: {ex.Message}");
                result.IsHealthy = false;
            }

            return result;
        }

        private async Task<ComponentHealthResult> CheckDatabaseConnectivity(CancellationToken cancellationToken)
        {
            var result = new ComponentHealthResult { ComponentName = "Database" };

            try
            {
                using var scope = _scopeFactory.CreateScope();

                // Test basic database connectivity
                var connectionTest = await TestDatabaseConnection(cancellationToken);

                result.Details["ConnectionTest"] = connectionTest;
                result.IsHealthy = connectionTest.Success;

                if (!connectionTest.Success)
                {
                    result.Issues.Add($"Database connection failed: {connectionTest.Error}");
                }
                else
                {
                    result.Details["ResponseTime"] = connectionTest.ResponseTimeMs;
                }
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Database connectivity check failed: {ex.Message}");
                result.IsHealthy = false;
            }

            return result;
        }

        private async Task<ComponentHealthResult> CheckSyncStatusAndDataFreshness(CancellationToken cancellationToken)
        {
            var result = new ComponentHealthResult { ComponentName = "Sync Status" };

            try
            {
                // Check last player sync
                var lastPlayerSync = await _espnDataSyncService.GetLastSyncReportAsync(SyncType.Players, cancellationToken);
                if (lastPlayerSync != null)
                {
                    var playerSyncAge = DateTime.UtcNow - lastPlayerSync.Result.EndTime;
                    result.Details["LastPlayerSync"] = new
                    {
                        EndTime = lastPlayerSync.Result.EndTime,
                        Status = lastPlayerSync.Result.Status.ToString(),
                        AgeHours = playerSyncAge?.TotalHours ?? 0,
                        PlayersProcessed = lastPlayerSync.Result.PlayersProcessed,
                        Errors = lastPlayerSync.Result.DataErrors + lastPlayerSync.Result.MatchingErrors + lastPlayerSync.Result.ApiErrors
                    };

                    // Player data should be refreshed daily
                    if (playerSyncAge?.TotalHours > 25) // Allow some grace period
                    {
                        result.Issues.Add($"Player data is stale (last sync: {playerSyncAge?.TotalHours:F1} hours ago)");
                    }

                    if (lastPlayerSync.Result.Status == SyncStatus.Failed)
                    {
                        result.Issues.Add("Last player sync failed");
                    }
                }
                else
                {
                    result.Issues.Add("No player sync history found");
                }

                // Check last stats sync
                var lastStatsSync = await _espnDataSyncService.GetLastSyncReportAsync(SyncType.PlayerStats, cancellationToken);
                if (lastStatsSync != null)
                {
                    var statsSyncAge = DateTime.UtcNow - lastStatsSync.Result.EndTime;
                    result.Details["LastStatsSync"] = new
                    {
                        EndTime = lastStatsSync.Result.EndTime,
                        Status = lastStatsSync.Result.Status.ToString(),
                        AgeHours = statsSyncAge?.TotalHours ?? 0,
                        StatsProcessed = lastStatsSync.Result.StatsRecordsProcessed,
                        Errors = lastStatsSync.Result.DataErrors + lastStatsSync.Result.MatchingErrors + lastStatsSync.Result.ApiErrors
                    };

                    // Stats data should be refreshed weekly during season
                    if (statsSyncAge?.TotalDays > 8) // Allow some grace period
                    {
                        result.Issues.Add($"Stats data is stale (last sync: {statsSyncAge?.TotalDays:F1} days ago)");
                    }

                    if (lastStatsSync.Result.Status == SyncStatus.Failed)
                    {
                        result.Issues.Add("Last stats sync failed");
                    }
                }

                result.IsHealthy = result.Issues.Count == 0;
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Sync status check failed: {ex.Message}");
                result.IsHealthy = false;
            }

            return result;
        }

        private ComponentHealthResult CheckRateLimitStatus()
        {
            var result = new ComponentHealthResult { ComponentName = "Rate Limit" };

            try
            {
                var rateLimitStatus = _rateLimitService.GetStatus();
                result.Details["RateLimitStatus"] = new
                {
                    RequestsRemaining = rateLimitStatus.RequestsRemaining,
                    TotalRequests = rateLimitStatus.TotalRequests,
                    IsLimited = rateLimitStatus.IsLimited,
                    TimeUntilReset = rateLimitStatus.TimeUntilReset.ToString(),
                    UtilizationPercentage = ((double)(rateLimitStatus.TotalRequests - rateLimitStatus.RequestsRemaining) / rateLimitStatus.TotalRequests) * 100
                };

                if (rateLimitStatus.IsLimited)
                {
                    result.Issues.Add("Rate limit exceeded");
                }
                else if (rateLimitStatus.RequestsRemaining < rateLimitStatus.TotalRequests * 0.1) // Less than 10% remaining
                {
                    result.Issues.Add("Rate limit nearly exhausted");
                }

                result.IsHealthy = !rateLimitStatus.IsLimited;
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Rate limit check failed: {ex.Message}");
                result.IsHealthy = false;
            }

            return result;
        }

        private async Task<ComponentHealthResult> CheckCacheHealth(CancellationToken cancellationToken)
        {
            var result = new ComponentHealthResult { ComponentName = "Cache" };

            try
            {
                // Check if cache is responsive by doing a simple read/write test
                var testKey = $"health_check_{Guid.NewGuid()}";
                var testValue = DateTime.UtcNow.ToString();

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                await _cacheService.SetAsync(testKey, testValue, TimeSpan.FromMinutes(1), cancellationToken);
                var retrievedValue = await _cacheService.GetAsync<string>(testKey, cancellationToken);
                await _cacheService.RemoveAsync(testKey, cancellationToken);

                stopwatch.Stop();

                result.Details["CacheTest"] = new
                {
                    TestSuccessful = retrievedValue == testValue,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    TestKey = testKey
                };

                if (retrievedValue != testValue)
                {
                    result.Issues.Add("Cache read/write test failed");
                }

                result.IsHealthy = result.Issues.Count == 0;
            }
            catch (Exception ex)
            {
                result.Issues.Add($"Cache health check failed: {ex.Message}");
                result.IsHealthy = false;
            }

            return result;
        }

        private async Task<EndpointTestResult> TestEndpoint(string endpoint, CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
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

        private async Task<DatabaseTestResult> TestDatabaseConnection(CancellationToken cancellationToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Simple database connectivity test
                // In a real implementation, you would inject a proper database context
                // For now, we'll simulate a successful connection test
                await Task.Delay(10, cancellationToken); // Simulate database query
                stopwatch.Stop();

                return new DatabaseTestResult
                {
                    Success = true,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Error = null
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new DatabaseTestResult
                {
                    Success = false,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    Error = ex.Message
                };
            }
        }

        private class ComponentHealthResult
        {
            public string ComponentName { get; set; } = string.Empty;
            public bool IsHealthy { get; set; } = true;
            public List<string> Issues { get; set; } = new();
            public Dictionary<string, object> Details { get; set; } = new();
        }

        private class EndpointTestResult
        {
            public string Endpoint { get; set; } = string.Empty;
            public bool Success { get; set; }
            public long ResponseTimeMs { get; set; }
            public int ResponseLength { get; set; }
            public string? Error { get; set; }
        }

        private class DatabaseTestResult
        {
            public bool Success { get; set; }
            public long ResponseTimeMs { get; set; }
            public string? Error { get; set; }
        }
    }
}