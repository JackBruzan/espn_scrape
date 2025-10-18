using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.Services.Infrastructure;
using ESPNScrape.Models.DataSync;
using System.Text.Json;

namespace ESPNScrape.Controllers
{
    /// <summary>
    /// Controller for monitoring and alerting endpoints
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MonitoringController : ControllerBase
    {
        private readonly ILogger<MonitoringController> _logger;
        private readonly EspnInfrastructureService _infrastructureService;
        private readonly IEspnDataSyncService _dataSyncService;
        private readonly HealthCheckService _healthCheckService;

        public MonitoringController(
            ILogger<MonitoringController> logger,
            EspnInfrastructureService infrastructureService,
            IEspnDataSyncService dataSyncService,
            HealthCheckService healthCheckService)
        {
            _logger = logger;
            _infrastructureService = infrastructureService;
            _dataSyncService = dataSyncService;
            _healthCheckService = healthCheckService;
        }

        /// <summary>
        /// Get comprehensive health status
        /// </summary>
        [HttpGet("health")]
        public async Task<ActionResult<HealthStatusResponse>> GetHealthStatus()
        {
            try
            {
                var healthReport = await _healthCheckService.CheckHealthAsync();

                var response = new HealthStatusResponse
                {
                    Status = healthReport.Status.ToString(),
                    TotalDuration = healthReport.TotalDuration,
                    Timestamp = DateTime.UtcNow,
                    Checks = healthReport.Entries.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new HealthCheckDetail
                        {
                            Status = kvp.Value.Status.ToString(),
                            Duration = kvp.Value.Duration,
                            Description = kvp.Value.Description,
                            Exception = kvp.Value.Exception?.Message,
                            Data = kvp.Value.Data?.ToDictionary(d => d.Key, d => d.Value?.ToString())
                        }
                    )
                };

                var statusCode = healthReport.Status switch
                {
                    HealthStatus.Healthy => 200,
                    HealthStatus.Degraded => 200, // Still operational
                    HealthStatus.Unhealthy => 503,
                    _ => 500
                };

                return StatusCode(statusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving health status");
                return StatusCode(500, new { error = "Health check failed", message = ex.Message });
            }
        }

        /// <summary>
        /// Get current performance metrics
        /// </summary>
        [HttpGet("metrics")]
        public ActionResult<MetricsSnapshot> GetCurrentMetrics()
        {
            try
            {
                var metrics = _infrastructureService.GetCurrentMetrics();
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving current metrics");
                return StatusCode(500, new { error = "Failed to retrieve metrics", message = ex.Message });
            }
        }

        /// <summary>
        /// Get metrics for a specific time range
        /// </summary>
        [HttpGet("metrics/range")]
        public ActionResult<MetricsSnapshot> GetMetricsForTimeRange([FromQuery] DateTime from, [FromQuery] DateTime to)
        {
            try
            {
                if (from >= to)
                {
                    return BadRequest(new { error = "Invalid time range", message = "'from' must be earlier than 'to'" });
                }

                if ((to - from).TotalDays > 7)
                {
                    return BadRequest(new { error = "Time range too large", message = "Maximum time range is 7 days" });
                }

                var metrics = _infrastructureService.GetMetrics(from, to);
                return Ok(metrics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving metrics for time range {From} - {To}", from, to);
                return StatusCode(500, new { error = "Failed to retrieve metrics", message = ex.Message });
            }
        }

        /// <summary>
        /// Reset all metrics
        /// </summary>
        [HttpPost("metrics/reset")]
        public ActionResult ResetMetrics()
        {
            try
            {
                _infrastructureService.ResetMetrics();
                _logger.LogInformation("Metrics reset by user request");
                return Ok(new { message = "Metrics reset successfully", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting metrics");
                return StatusCode(500, new { error = "Failed to reset metrics", message = ex.Message });
            }
        }

        /// <summary>
        /// Get recent alerts
        /// </summary>
        [HttpGet("alerts")]
        public ActionResult<List<AlertRecord>> GetRecentAlerts([FromQuery] int hours = 24)
        {
            try
            {
                if (hours < 1 || hours > 168) // 1 hour to 1 week
                {
                    return BadRequest(new { error = "Invalid time range", message = "Hours must be between 1 and 168" });
                }

                // Use GetAlertHistory since GetRecentAlertsAsync doesn't exist in the interface
                var fromTime = DateTime.UtcNow.AddHours(-hours);
                var alerts = _infrastructureService.GetAlertHistory(fromTime);
                return Ok(alerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving recent alerts");
                return StatusCode(500, new { error = "Failed to retrieve alerts", message = ex.Message });
            }
        }

        /// <summary>
        /// Get active alerts
        /// </summary>
        [HttpGet("alerts/active")]
        public ActionResult<List<AlertRecord>> GetActiveAlerts()
        {
            try
            {
                var activeAlerts = _infrastructureService.GetActiveAlerts();
                return Ok(activeAlerts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving active alerts");
                return StatusCode(500, new { error = "Failed to retrieve active alerts", message = ex.Message });
            }
        }

        /// <summary>
        /// Get alert statistics
        /// </summary>
        [HttpGet("alerts/statistics")]
        public async Task<ActionResult<AlertStatistics>> GetAlertStatistics()
        {
            try
            {
                var statistics = await _infrastructureService.GetAlertStatisticsAsync();
                return Ok(statistics);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alert statistics");
                return StatusCode(500, new { error = "Failed to retrieve alert statistics", message = ex.Message });
            }
        }

        /// <summary>
        /// Send a custom alert
        /// </summary>
        [HttpPost("alerts/send")]
        public async Task<ActionResult> SendCustomAlert([FromBody] CustomAlertRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Type) || string.IsNullOrWhiteSpace(request.Message))
                {
                    return BadRequest(new { error = "Invalid alert", message = "Type and Message are required" });
                }

                var alertCondition = new AlertCondition
                {
                    Type = request.Type,
                    CurrentValue = request.CurrentValue ?? 0,
                    Threshold = request.Threshold ?? 1,
                    Message = request.Message,
                    Timestamp = DateTime.UtcNow
                };

                await _infrastructureService.SendAlertAsync(alertCondition);

                _logger.LogInformation("Custom alert sent: {Type} - {Message}", request.Type, request.Message);
                return Ok(new { message = "Alert sent successfully", timestamp = DateTime.UtcNow });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending custom alert");
                return StatusCode(500, new { error = "Failed to send alert", message = ex.Message });
            }
        }

        /// <summary>
        /// Get sync status and history
        /// </summary>
        [HttpGet("sync/status")]
        public async Task<ActionResult<MonitoringSyncStatus>> GetSyncStatus()
        {
            try
            {
                var playerSyncReport = await _dataSyncService.GetLastSyncReportAsync(SyncType.Players);
                var statsSyncReport = await _dataSyncService.GetLastSyncReportAsync(SyncType.PlayerStats);

                var response = new MonitoringSyncStatus
                {
                    LastPlayerSync = playerSyncReport != null ? new SyncInfo
                    {
                        SyncType = SyncType.Players.ToString(),
                        Status = playerSyncReport.Result.Status.ToString(),
                        StartTime = playerSyncReport.Result.StartTime,
                        EndTime = playerSyncReport.Result.EndTime,
                        Duration = playerSyncReport.Result.Duration,
                        RecordsProcessed = playerSyncReport.Result.RecordsProcessed,
                        PlayersProcessed = playerSyncReport.Result.PlayersProcessed,
                        Errors = playerSyncReport.Result.MatchingErrors + playerSyncReport.Result.DataErrors + playerSyncReport.Result.ApiErrors
                    } : null,
                    LastStatsSync = statsSyncReport != null ? new SyncInfo
                    {
                        SyncType = SyncType.PlayerStats.ToString(),
                        Status = statsSyncReport.Result.Status.ToString(),
                        StartTime = statsSyncReport.Result.StartTime,
                        EndTime = statsSyncReport.Result.EndTime,
                        Duration = statsSyncReport.Result.Duration,
                        RecordsProcessed = statsSyncReport.Result.RecordsProcessed,
                        StatsProcessed = statsSyncReport.Result.StatsRecordsProcessed,
                        Errors = statsSyncReport.Result.MatchingErrors + statsSyncReport.Result.DataErrors + statsSyncReport.Result.ApiErrors
                    } : null,
                    Timestamp = DateTime.UtcNow
                };

                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sync status");
                return StatusCode(500, new { error = "Failed to retrieve sync status", message = ex.Message });
            }
        }

        /// <summary>
        /// Trigger manual performance and data quality checks
        /// </summary>
        [HttpPost("checks/run")]
        public async Task<ActionResult> RunManualChecks()
        {
            try
            {
                // Run all monitoring checks
                await _infrastructureService.CheckDataQualityAlertsAsync();
                await _infrastructureService.CheckPerformanceAlertsAsync();

                // Run health checks
                var healthReport = await _healthCheckService.CheckHealthAsync();
                await _infrastructureService.CheckHealthAlertsAsync(healthReport);

                _logger.LogInformation("Manual monitoring checks completed");
                return Ok(new
                {
                    message = "Manual checks completed successfully",
                    timestamp = DateTime.UtcNow,
                    healthStatus = healthReport.Status.ToString()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running manual checks");
                return StatusCode(500, new { error = "Failed to run manual checks", message = ex.Message });
            }
        }

        /// <summary>
        /// Get monitoring dashboard data
        /// </summary>
        [HttpGet("dashboard")]
        public async Task<ActionResult<MonitoringDashboard>> GetDashboardData()
        {
            try
            {
                var healthReport = await _healthCheckService.CheckHealthAsync();
                var metrics = _infrastructureService.GetCurrentMetrics();
                var activeAlerts = _infrastructureService.GetActiveAlerts();
                // Calculate alert statistics from active alerts
                var alertHistory = _infrastructureService.GetAlertHistory(DateTime.UtcNow.AddHours(-24));
                var syncStatus = await GetSyncStatus();

                var dashboard = new MonitoringDashboard
                {
                    HealthStatus = healthReport.Status.ToString(),
                    ActiveAlertsCount = activeAlerts.Count,
                    HighSeverityAlertsCount = activeAlerts.Count(a => a.Severity == AlertSeverity.Critical || a.Severity == AlertSeverity.Emergency),
                    TotalAlerts24h = alertHistory.Count,

                    // Extract key metrics
                    ApiHealthy = healthReport.Entries.ContainsKey("espn_api") &&
                                healthReport.Entries["espn_api"].Status == HealthStatus.Healthy,
                    DatabaseHealthy = healthReport.Entries.ContainsKey("database") &&
                                     healthReport.Entries["database"].Status == HealthStatus.Healthy,

                    SyncStatus = syncStatus.Value as MonitoringSyncStatus,
                    MetricsSummary = new MetricsSummary
                    {
                        TotalApiCalls = metrics.ResponseTimeMetrics.Values.Sum(m => m.RequestCount),
                        AverageApiResponseTime = metrics.ResponseTimeMetrics.Values.Any() ?
                            TimeSpan.FromMilliseconds(metrics.ResponseTimeMetrics.Values.Average(m => m.AverageResponseTime)) :
                            TimeSpan.Zero,
                        CacheHitRate = metrics.CacheMetrics.Values.Any() ?
                            metrics.CacheMetrics.Values.Average(m => m.HitRate) : 0
                    },

                    Timestamp = DateTime.UtcNow
                };

                return Ok(dashboard);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving dashboard data");
                return StatusCode(500, new { error = "Failed to retrieve dashboard data", message = ex.Message });
            }
        }
    }

    // Response models for API endpoints

    public class HealthStatusResponse
    {
        public string Status { get; set; } = string.Empty;
        public TimeSpan TotalDuration { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, HealthCheckDetail> Checks { get; set; } = new();
    }

    public class HealthCheckDetail
    {
        public string Status { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string? Description { get; set; }
        public string? Exception { get; set; }
        public Dictionary<string, string?>? Data { get; set; }
    }

    public class CustomAlertRequest
    {
        public string Type { get; set; } = string.Empty;
        public string? Component { get; set; }
        public double? CurrentValue { get; set; }
        public double? Threshold { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public class MonitoringSyncStatus
    {
        public SyncInfo? LastPlayerSync { get; set; }
        public SyncInfo? LastStatsSync { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SyncInfo
    {
        public string SyncType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int RecordsProcessed { get; set; }
        public int PlayersProcessed { get; set; }
        public int StatsProcessed { get; set; }
        public int Errors { get; set; }
    }

    public class MonitoringDashboard
    {
        public string HealthStatus { get; set; } = string.Empty;
        public int ActiveAlertsCount { get; set; }
        public int HighSeverityAlertsCount { get; set; }
        public int TotalAlerts24h { get; set; }
        public bool ApiHealthy { get; set; }
        public bool DatabaseHealthy { get; set; }
        public MonitoringSyncStatus? SyncStatus { get; set; }
        public MetricsSummary MetricsSummary { get; set; } = new();
        public DateTime Timestamp { get; set; }
    }

    public class MetricsSummary
    {
        public long TotalApiCalls { get; set; }
        public TimeSpan AverageApiResponseTime { get; set; }
        public double CacheHitRate { get; set; }
    }
}