using ESPNScrape.Services.Infrastructure.Interfaces;
using ESPNScrape.Services.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Diagnostics;
using System.Text.Json;

namespace ESPNScrape.Controllers
{
    /// <summary>
    /// Controller for diagnostic and monitoring endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly EspnInfrastructureService _infrastructureService;
        private readonly HealthCheckService _healthCheckService;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(
            EspnInfrastructureService infrastructureService,
            HealthCheckService healthCheckService,
            ILogger<DiagnosticsController> logger)
        {
            _infrastructureService = infrastructureService;
            _healthCheckService = healthCheckService;
            _logger = logger;
        }

        /// <summary>
        /// Get comprehensive health status
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> GetHealth()
        {
            using var operation = _infrastructureService.BeginTimedOperation("GetHealth");

            try
            {
                var healthReport = await _healthCheckService.CheckHealthAsync();

                var response = new
                {
                    status = healthReport.Status.ToString(),
                    totalDuration = healthReport.TotalDuration.TotalMilliseconds,
                    checks = healthReport.Entries.Select(entry => new
                    {
                        name = entry.Key,
                        status = entry.Value.Status.ToString(),
                        duration = entry.Value.Duration.TotalMilliseconds,
                        description = entry.Value.Description,
                        data = entry.Value.Data,
                        exception = entry.Value.Exception?.Message,
                        tags = entry.Value.Tags
                    })
                };

                var statusCode = healthReport.Status switch
                {
                    HealthStatus.Healthy => 200,
                    HealthStatus.Degraded => 200,
                    HealthStatus.Unhealthy => 503,
                    _ => 500
                };

                return StatusCode(statusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health status");
                return StatusCode(500, new { error = "Failed to retrieve health status" });
            }
        }

        /// <summary>
        /// Get current performance metrics
        /// </summary>
        [HttpGet("metrics")]
        public IActionResult GetMetrics([FromQuery] DateTime? from = null, [FromQuery] DateTime? to = null)
        {
            using var operation = _infrastructureService.BeginTimedOperation("GetMetrics");

            try
            {
                var metrics = _infrastructureService.GetCurrentMetrics();

                var response = new
                {
                    timestamp = metrics.Timestamp,
                    responseTimeMetrics = metrics.ResponseTimeMetrics.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            averageResponseTime = kvp.Value.AverageResponseTime,
                            maxResponseTime = kvp.Value.MaxResponseTime,
                            requestCount = kvp.Value.RequestCount
                        }),
                    cacheMetrics = metrics.CacheMetrics.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new
                        {
                            hitRate = kvp.Value.HitRate,
                            averageDuration = kvp.Value.AverageDuration
                        }),
                    summary = new
                    {
                        totalMetricTypes = metrics.Metrics.Count,
                        totalDataPoints = metrics.Metrics.Values.Sum(m => m.Count)
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metrics");
                return StatusCode(500, new { error = "Failed to retrieve metrics" });
            }
        }

        /// <summary>
        /// Get system information and diagnostics
        /// </summary>
        [HttpGet("system")]
        public IActionResult GetSystemInfo()
        {
            using var operation = _infrastructureService.BeginTimedOperation("GetSystemInfo");

            try
            {
                var process = Process.GetCurrentProcess();
                var gcInfo = GC.GetTotalMemory(false);

                var response = new
                {
                    timestamp = DateTime.UtcNow,
                    application = new
                    {
                        name = "ESPN Scrape Service",
                        version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
                        startTime = process.StartTime,
                        uptime = DateTime.Now - process.StartTime
                    },
                    system = new
                    {
                        machineName = Environment.MachineName,
                        osVersion = Environment.OSVersion.ToString(),
                        processorCount = Environment.ProcessorCount,
                        clrVersion = Environment.Version.ToString()
                    },
                    memory = new
                    {
                        workingSet = process.WorkingSet64,
                        privateMemorySize = process.PrivateMemorySize64,
                        gcTotalMemory = gcInfo,
                        gen0Collections = GC.CollectionCount(0),
                        gen1Collections = GC.CollectionCount(1),
                        gen2Collections = GC.CollectionCount(2)
                    },
                    performance = new
                    {
                        totalProcessorTime = process.TotalProcessorTime.TotalMilliseconds,
                        userProcessorTime = process.UserProcessorTime.TotalMilliseconds,
                        privilegedProcessorTime = process.PrivilegedProcessorTime.TotalMilliseconds
                    }
                };

                // Record memory usage metric
                _infrastructureService.RecordMemoryUsage(process.WorkingSet64);

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving system information");
                return StatusCode(500, new { error = "Failed to retrieve system information" });
            }
        }

        /// <summary>
        /// Get current alert conditions
        /// </summary>
        [HttpGet("alerts")]
        public IActionResult GetAlerts()
        {
            using var operation = _infrastructureService.BeginTimedOperation("GetAlerts");

            try
            {
                var alerts = _infrastructureService.CheckAlertConditions();

                var response = new
                {
                    timestamp = DateTime.UtcNow,
                    alertCount = alerts.Count,
                    hasActiveAlerts = alerts.Any(),
                    alerts = alerts.Select(alert => new
                    {
                        type = alert.Type,
                        message = alert.Message,
                        currentValue = alert.CurrentValue,
                        threshold = alert.Threshold,
                        timestamp = alert.Timestamp,
                        severity = GetAlertSeverity(alert)
                    })
                };

                if (alerts.Any())
                {
                    _infrastructureService.LogBusinessMetric("active_alerts", alerts.Count);
                    _logger.LogWarning("Found {AlertCount} active alerts", alerts.Count);
                }

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alerts");
                return StatusCode(500, new { error = "Failed to retrieve alerts" });
            }
        }

        /// <summary>
        /// Reset all metrics (useful for testing)
        /// </summary>
        [HttpPost("metrics/reset")]
        public IActionResult ResetMetrics()
        {
            using var operation = _infrastructureService.BeginTimedOperation("ResetMetrics");

            try
            {
                _infrastructureService.ResetMetrics();
                _logger.LogInformation("Metrics have been reset via API call");

                return Ok(new { message = "Metrics reset successfully", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting metrics");
                return StatusCode(500, new { error = "Failed to reset metrics" });
            }
        }

        /// <summary>
        /// Get detailed configuration information
        /// </summary>
        [HttpGet("config")]
        public IActionResult GetConfiguration()
        {
            using var operation = _infrastructureService.BeginTimedOperation("GetConfiguration");

            try
            {
                // Note: Be careful not to expose sensitive configuration values
                var response = new
                {
                    timestamp = DateTime.UtcNow,
                    environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    logging = new
                    {
                        logLevel = "Information", // This would come from actual config
                        enableStructuredLogging = true,
                        enableCorrelationIds = true
                    },
                    features = new
                    {
                        healthChecksEnabled = true,
                        metricsEnabled = true,
                        cachingEnabled = true,
                        rateLimitingEnabled = true
                    }
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving configuration");
                return StatusCode(500, new { error = "Failed to retrieve configuration" });
            }
        }

        /// <summary>
        /// Perform a comprehensive diagnostic check
        /// </summary>
        [HttpGet("full-diagnostic")]
        public async Task<IActionResult> GetFullDiagnostic()
        {
            using var operation = _infrastructureService.BeginTimedOperation("GetFullDiagnostic");

            try
            {
                var stopwatch = Stopwatch.StartNew();

                // Get all diagnostic information
                var healthTask = _healthCheckService.CheckHealthAsync();
                var alerts = _infrastructureService.CheckAlertConditions();
                var metrics = _infrastructureService.GetCurrentMetrics();

                var healthReport = await healthTask;
                var process = Process.GetCurrentProcess();

                stopwatch.Stop();

                var response = new
                {
                    timestamp = DateTime.UtcNow,
                    diagnosticDuration = stopwatch.Elapsed.TotalMilliseconds,
                    overallStatus = DetermineOverallStatus(healthReport, alerts),
                    health = new
                    {
                        status = healthReport.Status.ToString(),
                        totalDuration = healthReport.TotalDuration.TotalMilliseconds,
                        unhealthyChecks = healthReport.Entries.Count(e => e.Value.Status == HealthStatus.Unhealthy)
                    },
                    alerts = new
                    {
                        count = alerts.Count,
                        hasActiveAlerts = alerts.Any(),
                        criticalAlerts = alerts.Count(a => GetAlertSeverity(a) == "Critical")
                    },
                    performance = new
                    {
                        averageApiResponseTime = metrics.ResponseTimeMetrics.Values.Any()
                            ? metrics.ResponseTimeMetrics.Values.Average(m => m.AverageResponseTime)
                            : 0,
                        overallCacheHitRate = metrics.CacheMetrics.Values.Any()
                            ? metrics.CacheMetrics.Values.Average(m => m.HitRate)
                            : 0,
                        memoryUsage = process.WorkingSet64
                    },
                    recommendations = GenerateRecommendations(healthReport, alerts, metrics)
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing full diagnostic");
                return StatusCode(500, new { error = "Failed to perform full diagnostic" });
            }
        }

        private static string GetAlertSeverity(AlertCondition alert)
        {
            return alert.Type switch
            {
                "ErrorRate" when alert.CurrentValue > 10 => "Critical",
                "ResponseTime" when alert.CurrentValue > 5000 => "Critical",
                "CacheHitRate" when alert.CurrentValue < 50 => "Critical",
                _ => "Warning"
            };
        }

        private static string DetermineOverallStatus(HealthReport healthReport, List<AlertCondition> alerts)
        {
            if (healthReport.Status == HealthStatus.Unhealthy)
                return "Unhealthy";

            if (alerts.Any(a => GetAlertSeverity(a) == "Critical"))
                return "Critical";

            if (healthReport.Status == HealthStatus.Degraded || alerts.Any())
                return "Degraded";

            return "Healthy";
        }

        private static List<string> GenerateRecommendations(HealthReport healthReport, List<AlertCondition> alerts, MetricsSnapshot metrics)
        {
            var recommendations = new List<string>();

            if (healthReport.Status == HealthStatus.Unhealthy)
            {
                recommendations.Add("Check unhealthy services and resolve underlying issues");
            }

            foreach (var alert in alerts)
            {
                switch (alert.Type)
                {
                    case "ErrorRate":
                        recommendations.Add($"High error rate detected for {alert.Type}. Check logs and service health");
                        break;
                    case "ResponseTime":
                        recommendations.Add($"Slow response times for {alert.Type}. Consider performance optimization");
                        break;
                    case "CacheHitRate":
                        recommendations.Add($"Low cache hit rate for {alert.Type}. Review caching strategy");
                        break;
                }
            }

            if (metrics.CacheMetrics.Values.Any(c => c.HitRate < 80))
            {
                recommendations.Add("Consider optimizing cache strategy to improve hit rates");
            }

            if (!recommendations.Any())
            {
                recommendations.Add("System is operating normally");
            }

            return recommendations;
        }
    }
}