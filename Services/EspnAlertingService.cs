using ESPNScrape.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Service for handling alerting and monitoring
    /// </summary>
    public interface IEspnAlertingService
    {
        /// <summary>
        /// Process and handle alert conditions
        /// </summary>
        Task ProcessAlertsAsync(List<AlertCondition> alerts);

        /// <summary>
        /// Send immediate alert
        /// </summary>
        Task SendAlertAsync(AlertCondition alert);

        /// <summary>
        /// Get alert history
        /// </summary>
        List<AlertRecord> GetAlertHistory(DateTime? from = null, DateTime? to = null);

        /// <summary>
        /// Clear resolved alerts
        /// </summary>
        void ClearResolvedAlerts();

        /// <summary>
        /// Get current active alerts
        /// </summary>
        List<AlertRecord> GetActiveAlerts();

        /// <summary>
        /// Check for sync failure alerts
        /// </summary>
        Task CheckSyncAlertsAsync(Models.DataSync.SyncResult syncResult, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check for data quality alerts
        /// </summary>
        Task CheckDataQualityAlertsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Check for performance degradation alerts
        /// </summary>
        Task CheckPerformanceAlertsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Check for health status alerts
        /// </summary>
        Task CheckHealthAlertsAsync(Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport healthReport, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get alert statistics
        /// </summary>
        Task<AlertStatistics> GetAlertStatisticsAsync(CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Implementation of ESPN alerting service
    /// </summary>
    public class EspnAlertingService : IEspnAlertingService
    {
        private readonly ILogger<EspnAlertingService> _logger;
        private readonly LoggingConfiguration _config;
        private readonly ConcurrentDictionary<string, AlertRecord> _activeAlerts = new();
        private readonly ConcurrentQueue<AlertRecord> _alertHistory = new();

        public EspnAlertingService(ILogger<EspnAlertingService> logger, IOptions<LoggingConfiguration> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public async Task ProcessAlertsAsync(List<AlertCondition> alerts)
        {
            if (!_config.Alerting.EnableAlerting)
            {
                return;
            }

            foreach (var alert in alerts)
            {
                var alertKey = GetAlertKey(alert);
                var existingAlert = _activeAlerts.GetValueOrDefault(alertKey);

                if (existingAlert == null)
                {
                    // New alert
                    var alertRecord = new AlertRecord
                    {
                        Id = Guid.NewGuid(),
                        AlertCondition = alert,
                        FirstOccurrence = DateTime.UtcNow,
                        LastOccurrence = DateTime.UtcNow,
                        OccurrenceCount = 1,
                        State = AlertState.Active,
                        Severity = DetermineAlertSeverity(alert)
                    };

                    _activeAlerts[alertKey] = alertRecord;
                    _alertHistory.Enqueue(alertRecord);

                    await SendAlertAsync(alert);

                    _logger.LogWarning("New alert triggered: {AlertType} for {Component} - {Message}",
                        alert.Type, alert.Component, alert.Message);
                }
                else
                {
                    // Update existing alert
                    existingAlert.LastOccurrence = DateTime.UtcNow;
                    existingAlert.OccurrenceCount++;
                    existingAlert.AlertCondition = alert; // Update with latest values

                    // Send alert again if enough time has passed (to avoid spam)
                    var timeSinceLastAlert = DateTime.UtcNow - existingAlert.LastAlertSent;
                    if (timeSinceLastAlert >= _config.Alerting.AlertCooldownPeriod)
                    {
                        await SendAlertAsync(alert);
                        existingAlert.LastAlertSent = DateTime.UtcNow;

                        _logger.LogWarning("Alert re-triggered: {AlertType} for {Component} (occurrence #{Count})",
                            alert.Type, alert.Component, existingAlert.OccurrenceCount);
                    }
                }
            }

            // Check for resolved alerts
            await CheckForResolvedAlertsAsync(alerts);
        }

        public async Task SendAlertAsync(AlertCondition alert)
        {
            if (!_config.Alerting.EnableAlerting)
            {
                return;
            }

            try
            {
                var severity = DetermineAlertSeverity(alert);
                var alertMessage = FormatAlertMessage(alert, severity);

                // Log the alert
                _logger.LogError("ALERT [{Severity}]: {Message}", severity, alertMessage);

                // In a real implementation, you would send to external systems here:
                // - Email notifications
                // - Slack/Teams webhooks
                // - PagerDuty/OpsGenie
                // - SMS alerts
                // - etc.

                await SimulateExternalAlertingAsync(alert, alertMessage, severity);

                _logger.LogInformation("Alert sent successfully for {AlertType} on {Component}",
                    alert.Type, alert.Component);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send alert for {AlertType} on {Component}",
                    alert.Type, alert.Component);
            }
        }

        public List<AlertRecord> GetAlertHistory(DateTime? from = null, DateTime? to = null)
        {
            var history = _alertHistory.ToList();

            if (from.HasValue)
            {
                history = history.Where(a => a.FirstOccurrence >= from.Value).ToList();
            }

            if (to.HasValue)
            {
                history = history.Where(a => a.FirstOccurrence <= to.Value).ToList();
            }

            return history.OrderByDescending(a => a.FirstOccurrence).ToList();
        }

        public void ClearResolvedAlerts()
        {
            var resolvedAlerts = _activeAlerts.Where(kvp => kvp.Value.State == AlertState.Resolved).ToList();

            foreach (var resolved in resolvedAlerts)
            {
                _activeAlerts.TryRemove(resolved.Key, out _);
                _logger.LogInformation("Cleared resolved alert: {AlertType} for {Component}",
                    resolved.Value.AlertCondition.Type, resolved.Value.AlertCondition.Component);
            }
        }

        public List<AlertRecord> GetActiveAlerts()
        {
            return _activeAlerts.Values
                .Where(a => a.State == AlertState.Active)
                .OrderByDescending(a => a.LastOccurrence)
                .ToList();
        }

        public async Task CheckSyncAlertsAsync(Models.DataSync.SyncResult syncResult, CancellationToken cancellationToken = default)
        {
            var alerts = new List<AlertCondition>();
            var totalErrors = syncResult.MatchingErrors + syncResult.DataErrors + syncResult.ApiErrors;

            // Check for sync failures
            if (syncResult.Status == Models.DataSync.SyncStatus.Failed)
            {
                alerts.Add(new AlertCondition
                {
                    Type = "SyncFailure",
                    Component = "SyncService",
                    CurrentValue = 0, // Failed
                    Threshold = 1, // Expected success
                    Message = $"Sync operation {syncResult.SyncId} failed with {totalErrors} errors. Duration: {syncResult.Duration}",
                    Timestamp = DateTime.UtcNow
                });
            }

            // Check for excessive errors
            var errorRate = syncResult.RecordsProcessed > 0 ? (double)totalErrors / syncResult.RecordsProcessed : 0;
            if (errorRate > 0.05) // 5% error rate threshold
            {
                alerts.Add(new AlertCondition
                {
                    Type = "DataQuality",
                    Component = "SyncService",
                    CurrentValue = errorRate * 100,
                    Threshold = 5.0,
                    Message = $"Sync {syncResult.SyncType} has error rate of {errorRate:P2}",
                    Timestamp = DateTime.UtcNow
                });
            }

            await ProcessAlertsAsync(alerts);
        }

        public async Task CheckDataQualityAlertsAsync(CancellationToken cancellationToken = default)
        {
            // This method would check for data quality issues
            // For now, we'll implement a basic structure
            var alerts = new List<AlertCondition>();

            // Add logic to check data freshness, completeness, etc.
            // This would typically involve querying the database or sync service

            await ProcessAlertsAsync(alerts);
        }

        public async Task CheckPerformanceAlertsAsync(CancellationToken cancellationToken = default)
        {
            // This method would check for performance degradations
            var alerts = new List<AlertCondition>();

            // Add logic to check API response times, sync performance, etc.

            await ProcessAlertsAsync(alerts);
        }

        public async Task CheckHealthAlertsAsync(Microsoft.Extensions.Diagnostics.HealthChecks.HealthReport healthReport, CancellationToken cancellationToken = default)
        {
            var alerts = new List<AlertCondition>();

            if (healthReport.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy)
            {
                alerts.Add(new AlertCondition
                {
                    Type = "SystemHealth",
                    Component = "HealthCheck",
                    CurrentValue = 0, // Unhealthy
                    Threshold = 1, // Expected healthy
                    Message = "One or more health checks failed",
                    Timestamp = DateTime.UtcNow
                });
            }
            else if (healthReport.Status == Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded)
            {
                alerts.Add(new AlertCondition
                {
                    Type = "SystemHealth",
                    Component = "HealthCheck",
                    CurrentValue = 0.5, // Degraded
                    Threshold = 1, // Expected healthy
                    Message = "One or more health checks are degraded",
                    Timestamp = DateTime.UtcNow
                });
            }

            await ProcessAlertsAsync(alerts);
        }

        public async Task<AlertStatistics> GetAlertStatisticsAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Make method async for consistency

            var last24Hours = DateTime.UtcNow.AddHours(-24);
            var recentAlerts = _alertHistory
                .Where(a => a.FirstOccurrence >= last24Hours)
                .ToList();

            return new AlertStatistics
            {
                TotalAlerts24h = recentAlerts.Count,
                HighSeverityAlerts24h = recentAlerts.Count(a => a.Severity == AlertSeverity.High),
                MediumSeverityAlerts24h = recentAlerts.Count(a => a.Severity == AlertSeverity.Medium),
                LowSeverityAlerts24h = recentAlerts.Count(a => a.Severity == AlertSeverity.Low),
                AlertsByType = recentAlerts.GroupBy(a => a.AlertCondition.Type)
                    .ToDictionary(g => g.Key, g => g.Count()),
                AlertsByComponent = recentAlerts.GroupBy(a => a.AlertCondition.Component)
                    .ToDictionary(g => g.Key, g => g.Count()),
                GeneratedAt = DateTime.UtcNow
            };
        }

        private async Task CheckForResolvedAlertsAsync(List<AlertCondition> currentAlerts)
        {
            var currentAlertKeys = currentAlerts.Select(GetAlertKey).ToHashSet();
            var activeAlertKeys = _activeAlerts.Keys.ToList();

            foreach (var activeKey in activeAlertKeys)
            {
                if (!currentAlertKeys.Contains(activeKey))
                {
                    // Alert is no longer active, mark as resolved
                    if (_activeAlerts.TryGetValue(activeKey, out var alertRecord))
                    {
                        alertRecord.State = AlertState.Resolved;
                        alertRecord.ResolvedAt = DateTime.UtcNow;

                        var resolveMessage = $"Alert resolved: {alertRecord.AlertCondition.Type} for {alertRecord.AlertCondition.Component}";
                        _logger.LogInformation(resolveMessage);

                        // Optionally send resolution notification
                        if (_config.Alerting.SendResolutionNotifications)
                        {
                            await SendResolutionNotificationAsync(alertRecord);
                        }
                    }
                }
            }
        }

        private async Task SendResolutionNotificationAsync(AlertRecord alertRecord)
        {
            try
            {
                var message = $"RESOLVED: {alertRecord.AlertCondition.Type} alert for {alertRecord.AlertCondition.Component} " +
                             $"has been resolved after {alertRecord.OccurrenceCount} occurrence(s)";

                _logger.LogInformation(message);

                // Simulate sending resolution notification
                await Task.Delay(100); // Simulate external call

                _logger.LogDebug("Resolution notification sent for {AlertType} on {Component}",
                    alertRecord.AlertCondition.Type, alertRecord.AlertCondition.Component);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send resolution notification for {AlertType} on {Component}",
                    alertRecord.AlertCondition.Type, alertRecord.AlertCondition.Component);
            }
        }

        private async Task SimulateExternalAlertingAsync(AlertCondition alert, string message, AlertSeverity severity)
        {
            // Simulate external alerting system delays
            await Task.Delay(Random.Shared.Next(50, 200));

            // In a real implementation, you would integrate with:

            // Email
            if (_config.Alerting.EmailEnabled)
            {
                _logger.LogDebug("Would send email alert: {Message}", message);
            }

            // Webhook
            if (_config.Alerting.WebhookEnabled && !string.IsNullOrEmpty(_config.Alerting.WebhookUrl))
            {
                _logger.LogDebug("Would send webhook to {Url}: {Message}", _config.Alerting.WebhookUrl, message);
            }

            // SMS (for critical alerts)
            if (severity == AlertSeverity.Critical && _config.Alerting.SmsEnabled)
            {
                _logger.LogDebug("Would send SMS alert: {Message}", message);
            }
        }

        private static string GetAlertKey(AlertCondition alert)
        {
            return $"{alert.Type}:{alert.Component}";
        }

        private static AlertSeverity DetermineAlertSeverity(AlertCondition alert)
        {
            return alert.Type switch
            {
                "ErrorRate" when alert.CurrentValue > 10 => AlertSeverity.Critical,
                "ResponseTime" when alert.CurrentValue > 5000 => AlertSeverity.Critical,
                "CacheHitRate" when alert.CurrentValue < 50 => AlertSeverity.Critical,
                "ErrorRate" when alert.CurrentValue > 5 => AlertSeverity.High,
                "ResponseTime" when alert.CurrentValue > 2000 => AlertSeverity.High,
                "CacheHitRate" when alert.CurrentValue < 70 => AlertSeverity.High,
                _ => AlertSeverity.Medium
            };
        }

        private static string FormatAlertMessage(AlertCondition alert, AlertSeverity severity)
        {
            return $"[{severity}] {alert.Type} Alert: {alert.Message} " +
                   $"(Current: {alert.CurrentValue:F2}, Threshold: {alert.Threshold:F2})";
        }
    }

    /// <summary>
    /// Background service for continuous alert monitoring
    /// </summary>
    public class AlertMonitoringService : BackgroundService
    {
        private readonly IEspnMetricsService _metricsService;
        private readonly IEspnAlertingService _alertingService;
        private readonly IEspnLoggingService _loggingService;
        private readonly LoggingConfiguration _config;
        private readonly ILogger<AlertMonitoringService> _logger;

        public AlertMonitoringService(
            IEspnMetricsService metricsService,
            IEspnAlertingService alertingService,
            IEspnLoggingService loggingService,
            IOptions<LoggingConfiguration> config,
            ILogger<AlertMonitoringService> logger)
        {
            _metricsService = metricsService;
            _alertingService = alertingService;
            _loggingService = loggingService;
            _config = config.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_config.Alerting.EnableAlerting)
            {
                _logger.LogInformation("Alert monitoring is disabled");
                return;
            }

            _logger.LogInformation("Alert monitoring service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var operation = _loggingService.BeginTimedOperation("AlertMonitoring");

                    // Check for alert conditions
                    var alerts = _metricsService.CheckAlertConditions();

                    if (alerts.Any())
                    {
                        await _alertingService.ProcessAlertsAsync(alerts);
                        _loggingService.LogBusinessMetric("alerts_processed", alerts.Count);
                    }

                    // Clean up old resolved alerts periodically
                    if (DateTime.UtcNow.Minute % 15 == 0) // Every 15 minutes
                    {
                        _alertingService.ClearResolvedAlerts();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in alert monitoring cycle");
                }

                await Task.Delay(_config.Alerting.MonitoringInterval, stoppingToken);
            }

            _logger.LogInformation("Alert monitoring service stopped");
        }
    }

    /// <summary>
    /// Represents an alert record with history
    /// </summary>
    public class AlertRecord
    {
        public Guid Id { get; set; }
        public AlertCondition AlertCondition { get; set; } = new();
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public DateTime? LastAlertSent { get; set; }
        public int OccurrenceCount { get; set; }
        public AlertState State { get; set; }
        public AlertSeverity Severity { get; set; }
    }

    /// <summary>
    /// Alert state enumeration
    /// </summary>
    public enum AlertState
    {
        Active,
        Resolved,
        Suppressed
    }

    /// <summary>
    /// Alert severity levels
    /// </summary>
    public enum AlertSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Alert statistics
    /// </summary>
    public class AlertStatistics
    {
        public int TotalAlerts24h { get; set; }
        public int HighSeverityAlerts24h { get; set; }
        public int MediumSeverityAlerts24h { get; set; }
        public int LowSeverityAlerts24h { get; set; }
        public Dictionary<string, int> AlertsByType { get; set; } = new();
        public Dictionary<string, int> AlertsByComponent { get; set; } = new();
        public DateTime GeneratedAt { get; set; }
    }
}