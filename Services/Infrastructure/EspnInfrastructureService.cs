using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ESPNScrape.Configuration;
using ESPNScrape.Models.DataSync;
using ESPNScrape.Services.Interfaces;
using Serilog.Context;
using System.Collections.Concurrent;
using System.Diagnostics;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace ESPNScrape.Services.Infrastructure
{


    /// <summary>
    /// Combined infrastructure service that handles logging, metrics, and alerting
    /// </summary>
    public class EspnInfrastructureService : ESPNScrape.Services.Interfaces.IEspnLoggingService, ESPNScrape.Services.Interfaces.IEspnMetricsService, ESPNScrape.Services.Interfaces.IEspnAlertingService
    {
        private readonly ILogger<EspnInfrastructureService> _logger;
        private readonly LoggingConfiguration _loggingConfig;
        private readonly CacheConfiguration _cacheConfig;
        private readonly ResilienceConfiguration _resilienceConfig;

        // Logging fields
        private static readonly AsyncLocal<string?> _correlationId = new();
        private const string CorrelationIdProperty = "CorrelationId";

        // Metrics fields
        private readonly ConcurrentDictionary<string, MetricData> _metrics = new();
        private readonly ConcurrentDictionary<string, ResponseTimeMetrics> _responseTimeMetrics = new();
        private readonly ConcurrentDictionary<string, CacheMetrics> _cacheMetrics = new();
        private readonly object _metricsLock = new();

        // Alerting fields
        private readonly ConcurrentDictionary<string, AlertRecord> _activeAlerts = new();
        private readonly List<AlertRecord> _alertHistory = new();
        private readonly object _alertLock = new();

        public EspnInfrastructureService(
            ILogger<EspnInfrastructureService> logger,
            IOptions<LoggingConfiguration> loggingOptions,
            IOptions<CacheConfiguration> cacheOptions,
            IOptions<ResilienceConfiguration> resilienceOptions)
        {
            _logger = logger;
            _loggingConfig = loggingOptions.Value;
            _cacheConfig = cacheOptions.Value;
            _resilienceConfig = resilienceOptions.Value;
        }

        #region IEspnLoggingService Implementation

        public void LogApiOperation(string endpoint, string method, TimeSpan responseTime, int statusCode, bool success, string? errorMessage = null)
        {
            using (LogContext.PushProperty("Endpoint", endpoint))
            using (LogContext.PushProperty("Method", method))
            using (LogContext.PushProperty("ResponseTime", responseTime.TotalMilliseconds))
            using (LogContext.PushProperty("StatusCode", statusCode))
            using (LogContext.PushProperty("Success", success))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var logLevel = success ? LogLevel.Information : LogLevel.Warning;

                if (success)
                {
                    _logger.Log(logLevel, "ESPN API {Method} {Endpoint} completed successfully with status {StatusCode} in {ResponseTime}ms",
                        method, endpoint, statusCode, responseTime.TotalMilliseconds);
                }
                else
                {
                    using (LogContext.PushProperty("ErrorMessage", errorMessage))
                    {
                        _logger.Log(logLevel, "ESPN API {Method} {Endpoint} failed with status {StatusCode} after {ResponseTime}ms: {ErrorMessage}",
                            method, endpoint, statusCode, responseTime.TotalMilliseconds, errorMessage);
                    }
                }
            }

            // Also record as metric
            RecordApiResponseTime(endpoint, responseTime, success);
        }

        public void LogCacheOperation(string operation, string key, bool hit, TimeSpan? duration = null)
        {
            using (LogContext.PushProperty("CacheOperation", operation))
            using (LogContext.PushProperty("CacheKey", key))
            using (LogContext.PushProperty("CacheHit", hit))
            using (LogContext.PushProperty("Duration", duration?.TotalMilliseconds))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var hitMiss = hit ? "HIT" : "MISS";
                var durationInfo = duration.HasValue ? $" in {duration.Value.TotalMilliseconds}ms" : "";

                _logger.LogDebug("Cache {CacheOperation} - {HitMiss} for key {CacheKey}{DurationInfo}",
                    operation, hitMiss, key, durationInfo);
            }

            // Also record as metric
            RecordCacheOperation(operation, hit, duration);
        }

        public void LogBusinessMetric(string metricName, object value, Dictionary<string, object>? additionalProperties = null)
        {
            using (LogContext.PushProperty("MetricName", metricName))
            using (LogContext.PushProperty("MetricValue", value))
            using (LogContext.PushProperty("MetricType", "Business"))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var disposables = new List<IDisposable>();
                try
                {
                    if (additionalProperties != null)
                    {
                        foreach (var prop in additionalProperties)
                        {
                            disposables.Add(LogContext.PushProperty(prop.Key, prop.Value));
                        }
                    }

                    _logger.LogInformation("Business metric {MetricName}: {MetricValue}", metricName, value);
                }
                finally
                {
                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }

            // Also record as metric if it's a numeric value
            if (value is double doubleValue)
            {
                var tags = additionalProperties?.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? string.Empty) ?? new Dictionary<string, string>();
                RecordBusinessMetric(metricName, doubleValue, tags);
            }
        }

        public void LogBulkOperationProgress(string operationId, string operationType, int completed, int total, TimeSpan elapsed)
        {
            var percentComplete = total > 0 ? (double)completed / total * 100 : 0;
            var itemsPerSecond = elapsed.TotalSeconds > 0 ? completed / elapsed.TotalSeconds : 0;

            using (LogContext.PushProperty("OperationId", operationId))
            using (LogContext.PushProperty("OperationType", operationType))
            using (LogContext.PushProperty("CompletedItems", completed))
            using (LogContext.PushProperty("TotalItems", total))
            using (LogContext.PushProperty("PercentComplete", percentComplete))
            using (LogContext.PushProperty("ItemsPerSecond", itemsPerSecond))
            using (LogContext.PushProperty("ElapsedTime", elapsed.TotalSeconds))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                _logger.LogInformation("Bulk operation {OperationType} ({OperationId}): {CompletedItems}/{TotalItems} completed ({PercentComplete:F1}%) - {ItemsPerSecond:F2} items/sec",
                    operationType, operationId, completed, total, percentComplete, itemsPerSecond);
            }

            // Also record as metric
            RecordBulkOperationMetrics(operationType, completed, elapsed, 0); // Assuming no errors for progress logging
        }

        public IDisposable BeginTimedOperation(string operationName, Dictionary<string, object>? properties = null)
        {
            return new TimedOperation(_logger, operationName, properties, GetOrGenerateCorrelationId());
        }

        public IDisposable BeginCorrelationContext(string correlationId)
        {
            var previousCorrelationId = _correlationId.Value;
            _correlationId.Value = correlationId;

            return new CorrelationContext(() => _correlationId.Value = previousCorrelationId);
        }

        public string GetOrGenerateCorrelationId()
        {
            return _correlationId.Value ?? GenerateCorrelationId();
        }

        public static string GenerateCorrelationId()
        {
            var newId = Guid.NewGuid().ToString("N")[..8];
            _correlationId.Value = newId;
            return newId;
        }

        public void LogStructured(LogLevel level, string messageTemplate, params object[] args)
        {
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                _logger.Log(level, messageTemplate, args);
            }
        }

        public void LogHealthCheck(string checkName, bool healthy, TimeSpan duration, string? details = null)
        {
            using (LogContext.PushProperty("HealthCheckName", checkName))
            using (LogContext.PushProperty("Healthy", healthy))
            using (LogContext.PushProperty("Duration", duration.TotalMilliseconds))
            using (LogContext.PushProperty("Details", details))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var status = healthy ? "HEALTHY" : "UNHEALTHY";
                var detailsInfo = !string.IsNullOrEmpty(details) ? $" - {details}" : "";

                if (healthy)
                {
                    _logger.LogInformation("Health check {CheckName}: {Status} in {Duration}ms{Details}",
                        checkName, status, duration.TotalMilliseconds, detailsInfo);
                }
                else
                {
                    _logger.LogWarning("Health check {CheckName}: {Status} in {Duration}ms{Details}",
                        checkName, status, duration.TotalMilliseconds, detailsInfo);
                }
            }

            // Also record as metric
            RecordHealthCheck(checkName, healthy, duration);
        }

        public void LogPerformanceMetrics(string component, Dictionary<string, object> metrics)
        {
            using (LogContext.PushProperty("Component", component))
            using (LogContext.PushProperty("MetricType", "Performance"))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var disposables = new List<IDisposable>();
                try
                {
                    foreach (var metric in metrics)
                    {
                        disposables.Add(LogContext.PushProperty(metric.Key, metric.Value));
                    }

                    _logger.LogInformation("Performance metrics for {Component}: {Metrics}",
                        component, string.Join(", ", metrics.Select(m => $"{m.Key}={m.Value}")));
                }
                finally
                {
                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        public void LogError(Exception exception, string message, Dictionary<string, object>? context = null)
        {
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var disposables = new List<IDisposable>();
                try
                {
                    if (context != null)
                    {
                        foreach (var prop in context)
                        {
                            disposables.Add(LogContext.PushProperty(prop.Key, prop.Value));
                        }
                    }

                    _logger.LogError(exception, message);
                }
                finally
                {
                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        public void SetCorrelationContext(string correlationId)
        {
            _correlationId.Value = correlationId;
        }

        public string? GetCurrentCorrelationId()
        {
            return _correlationId.Value;
        }

        public void ClearCorrelationContext()
        {
            _correlationId.Value = null;
        }

        public void LogHealth(string componentName, bool healthy, TimeSpan duration, Dictionary<string, object>? details = null)
        {
            using (LogContext.PushProperty("Component", componentName))
            using (LogContext.PushProperty("Healthy", healthy))
            using (LogContext.PushProperty("Duration", duration.TotalMilliseconds))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var disposables = new List<IDisposable>();
                try
                {
                    if (details != null)
                    {
                        foreach (var detail in details)
                        {
                            disposables.Add(LogContext.PushProperty(detail.Key, detail.Value));
                        }
                    }

                    var logLevel = healthy ? LogLevel.Information : LogLevel.Warning;
                    var status = healthy ? "HEALTHY" : "UNHEALTHY";

                    _logger.Log(logLevel, "Health check {Component}: {Status} in {Duration}ms",
                        componentName, status, duration.TotalMilliseconds);
                }
                finally
                {
                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }

            // Also record as metric
            RecordHealthCheck(componentName, healthy, duration);
        }

        public void LogBulkOperation(string operationType, int itemsProcessed, TimeSpan duration, int errorCount, bool success)
        {
            using (LogContext.PushProperty("OperationType", operationType))
            using (LogContext.PushProperty("ItemsProcessed", itemsProcessed))
            using (LogContext.PushProperty("Duration", duration.TotalMilliseconds))
            using (LogContext.PushProperty("ErrorCount", errorCount))
            using (LogContext.PushProperty("Success", success))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var logLevel = success ? LogLevel.Information : LogLevel.Warning;
                var itemsPerSecond = duration.TotalSeconds > 0 ? itemsProcessed / duration.TotalSeconds : 0;

                _logger.Log(logLevel, "Bulk operation {OperationType}: {ItemsProcessed} items processed in {Duration}ms ({ItemsPerSecond:F2} items/sec), {ErrorCount} errors",
                    operationType, itemsProcessed, duration.TotalMilliseconds, itemsPerSecond, errorCount);
            }

            // Also record as metric
            RecordBulkOperationMetrics(operationType, itemsProcessed, duration, errorCount);
        }

        public void LogConfigurationLoad(string configSection, bool success, Dictionary<string, object>? details = null)
        {
            using (LogContext.PushProperty("ConfigSection", configSection))
            using (LogContext.PushProperty("Success", success))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var disposables = new List<IDisposable>();
                try
                {
                    if (details != null)
                    {
                        foreach (var detail in details)
                        {
                            disposables.Add(LogContext.PushProperty(detail.Key, detail.Value));
                        }
                    }

                    var logLevel = success ? LogLevel.Information : LogLevel.Warning;
                    var status = success ? "loaded successfully" : "failed to load";

                    _logger.Log(logLevel, "Configuration section {ConfigSection} {Status}",
                        configSection, status);
                }
                finally
                {
                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        public void LogPerformanceMetric(string metricName, double value, string unit, Dictionary<string, string>? tags = null)
        {
            using (LogContext.PushProperty("MetricName", metricName))
            using (LogContext.PushProperty("MetricValue", value))
            using (LogContext.PushProperty("Unit", unit))
            using (LogContext.PushProperty("MetricType", "Performance"))
            using (LogContext.PushProperty(CorrelationIdProperty, GetOrGenerateCorrelationId()))
            {
                var disposables = new List<IDisposable>();
                try
                {
                    if (tags != null)
                    {
                        foreach (var tag in tags)
                        {
                            disposables.Add(LogContext.PushProperty($"Tag_{tag.Key}", tag.Value));
                        }
                    }

                    _logger.LogDebug("Performance metric {MetricName}: {Value} {Unit}",
                        metricName, value, unit);
                }
                finally
                {
                    foreach (var disposable in disposables)
                    {
                        disposable.Dispose();
                    }
                }
            }

            // Also record as metric
            RecordBusinessMetric(metricName, value, tags);
        }

        #endregion

        #region IEspnMetricsService Implementation

        public void RecordSyncPerformance(SyncResult syncResult)
        {
            var metricKey = $"sync_{syncResult.SyncType}";

            lock (_metricsLock)
            {
                if (!_metrics.TryGetValue(metricKey, out var metric))
                {
                    metric = new MetricData { Name = metricKey };
                    _metrics[metricKey] = metric;
                }

                metric.RecordValue(syncResult.Duration.TotalMilliseconds);
                metric.IncrementCount(syncResult.IsSuccessful ? 1 : 0, syncResult.IsSuccessful ? 0 : 1);
            }

            _logger.LogInformation("Sync performance recorded for {SyncType}: {Duration}ms, Success: {Success}",
                syncResult.SyncType, syncResult.Duration.TotalMilliseconds, syncResult.IsSuccessful);
        }

        public void RecordPlayerMatchingAccuracy(int totalPlayers, int successfulMatches, int manualReviewRequired)
        {
            var accuracy = totalPlayers > 0 ? (double)successfulMatches / totalPlayers * 100 : 0;

            lock (_metricsLock)
            {
                var accuracyMetric = _metrics.GetOrAdd("player_matching_accuracy", _ => new MetricData { Name = "player_matching_accuracy" });
                var totalMetric = _metrics.GetOrAdd("player_matching_total", _ => new MetricData { Name = "player_matching_total" });
                var reviewMetric = _metrics.GetOrAdd("player_matching_manual_review", _ => new MetricData { Name = "player_matching_manual_review" });

                accuracyMetric.RecordValue(accuracy);
                totalMetric.RecordValue(totalPlayers);
                reviewMetric.RecordValue(manualReviewRequired);
            }

            _logger.LogInformation("Player matching accuracy: {Accuracy:F2}% ({SuccessfulMatches}/{TotalPlayers}), Manual review: {ManualReviewRequired}",
                accuracy, successfulMatches, totalPlayers, manualReviewRequired);
        }

        public void RecordDatabaseOperation(string operationType, TimeSpan duration, int recordsAffected, bool success = true)
        {
            var metricKey = $"database_{operationType.ToLowerInvariant()}";

            lock (_metricsLock)
            {
                if (!_metrics.TryGetValue(metricKey, out var metric))
                {
                    metric = new MetricData { Name = metricKey };
                    _metrics[metricKey] = metric;
                }

                metric.RecordValue(duration.TotalMilliseconds);
                metric.IncrementCount(success ? 1 : 0, success ? 0 : 1);
                metric.RecordValue(recordsAffected, "records_affected");
            }

            _logger.LogDebug("Database operation {OperationType}: {Duration}ms, {RecordsAffected} records, Success: {Success}",
                operationType, duration.TotalMilliseconds, recordsAffected, success);
        }

        public IDisposable StartOperation(string operationName, Dictionary<string, object>? metadata = null)
        {
            return new OperationTimer(this, operationName, metadata);
        }

        public void RecordApiResponseTime(string endpoint, TimeSpan responseTime, bool success)
        {
            var metricKey = $"api_{endpoint.Replace("/", "_").Replace("{", "").Replace("}", "")}";

            lock (_metricsLock)
            {
                if (!_responseTimeMetrics.TryGetValue(metricKey, out var metric))
                {
                    metric = new ResponseTimeMetrics { Endpoint = endpoint };
                    _responseTimeMetrics[metricKey] = metric;
                }

                metric.RecordResponseTime(responseTime, success);
            }

            _logger.LogDebug("API response time recorded for {Endpoint}: {ResponseTime}ms, Success: {Success}",
                endpoint, responseTime.TotalMilliseconds, success);
        }

        public void RecordCacheOperation(string operation, bool hit, TimeSpan? duration = null)
        {
            var metricKey = $"cache_{operation.ToLowerInvariant()}";

            lock (_metricsLock)
            {
                if (!_cacheMetrics.TryGetValue(metricKey, out var metric))
                {
                    metric = new CacheMetrics { Operation = operation };
                    _cacheMetrics[metricKey] = metric;
                }

                metric.RecordOperation(hit, duration);
            }

            _logger.LogDebug("Cache operation recorded for {Operation}: {Result}, Duration: {Duration}ms",
                operation, hit ? "HIT" : "MISS", duration?.TotalMilliseconds);
        }

        public void RecordBusinessMetric(string metricName, double value, Dictionary<string, string>? tags = null)
        {
            var metricKey = $"business_{metricName.ToLowerInvariant()}";

            lock (_metricsLock)
            {
                if (!_metrics.TryGetValue(metricKey, out var metric))
                {
                    metric = new MetricData { Name = metricName, Tags = tags ?? new Dictionary<string, string>() };
                    _metrics[metricKey] = metric;
                }

                metric.RecordValue(value);
            }

            _logger.LogDebug("Business metric recorded: {MetricName} = {Value}", metricName, value);
        }

        public void RecordBulkOperationMetrics(string operationType, int itemsProcessed, TimeSpan duration, int errorCount)
        {
            var metricKey = $"bulk_{operationType.ToLowerInvariant()}";

            lock (_metricsLock)
            {
                if (!_metrics.TryGetValue(metricKey, out var metric))
                {
                    metric = new MetricData { Name = metricKey };
                    _metrics[metricKey] = metric;
                }

                metric.RecordValue(duration.TotalMilliseconds, "duration");
                metric.RecordValue(itemsProcessed, "items_processed");
                metric.RecordValue(errorCount, "errors");

                if (duration.TotalSeconds > 0)
                {
                    var itemsPerSecond = itemsProcessed / duration.TotalSeconds;
                    metric.RecordValue(itemsPerSecond, "items_per_second");
                }
            }

            _logger.LogDebug("Bulk operation metrics recorded for {OperationType}: {ItemsProcessed} items, {Duration}ms, {ErrorCount} errors",
                operationType, itemsProcessed, duration.TotalMilliseconds, errorCount);
        }

        public void RecordMemoryUsage(long bytesUsed)
        {
            lock (_metricsLock)
            {
                var metric = _metrics.GetOrAdd("memory_usage", _ => new MetricData { Name = "memory_usage" });
                metric.RecordValue(bytesUsed);
            }

            _logger.LogDebug("Memory usage recorded: {BytesUsed} bytes", bytesUsed);
        }

        public void RecordHealthCheck(string checkName, bool healthy, TimeSpan duration)
        {
            var metricKey = $"health_{checkName.ToLowerInvariant()}";

            lock (_metricsLock)
            {
                if (!_metrics.TryGetValue(metricKey, out var metric))
                {
                    metric = new MetricData { Name = metricKey };
                    _metrics[metricKey] = metric;
                }

                metric.RecordValue(duration.TotalMilliseconds);
                metric.IncrementCount(healthy ? 1 : 0, healthy ? 0 : 1);
            }

            _logger.LogDebug("Health check recorded for {CheckName}: {Healthy}, {Duration}ms",
                checkName, healthy ? "HEALTHY" : "UNHEALTHY", duration.TotalMilliseconds);
        }

        public MetricsSnapshot GetCurrentMetrics()
        {
            lock (_metricsLock)
            {
                return new MetricsSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    Metrics = _metrics.ToDictionary(kv => kv.Key, kv => kv.Value.Copy()),
                    ResponseTimeMetrics = _responseTimeMetrics.ToDictionary(kv => kv.Key, kv => kv.Value.Copy()),
                    CacheMetrics = _cacheMetrics.ToDictionary(kv => kv.Key, kv => kv.Value.Copy())
                };
            }
        }

        public MetricsSnapshot GetMetrics(DateTime from, DateTime to)
        {
            // For this implementation, we'll return current metrics filtered by timestamp
            // In a production system, you'd want to store historical data
            lock (_metricsLock)
            {
                var filteredMetrics = _metrics
                    .Where(kv => kv.Value.LastUpdated >= from && kv.Value.LastUpdated <= to)
                    .ToDictionary(kv => kv.Key, kv => kv.Value.Copy());

                return new MetricsSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    Metrics = filteredMetrics,
                    ResponseTimeMetrics = _responseTimeMetrics.ToDictionary(kv => kv.Key, kv => kv.Value.Copy()),
                    CacheMetrics = _cacheMetrics.ToDictionary(kv => kv.Key, kv => kv.Value.Copy())
                };
            }
        }

        public void ResetMetrics()
        {
            lock (_metricsLock)
            {
                _metrics.Clear();
                _responseTimeMetrics.Clear();
                _cacheMetrics.Clear();
            }

            _logger.LogInformation("All metrics have been reset");
        }

        public List<AlertCondition> CheckAlertConditions()
        {
            var alerts = new List<AlertCondition>();

            // Default alert thresholds (could be moved to configuration)
            const double responseTimeThreshold = 5000; // 5 seconds
            const double errorRateThreshold = 10; // 10%

            lock (_metricsLock)
            {
                // Check response time alerts
                foreach (var responseMetric in _responseTimeMetrics.Values)
                {
                    if (responseMetric.AverageResponseTime > responseTimeThreshold)
                    {
                        alerts.Add(new AlertCondition
                        {
                            Type = "ResponseTime",
                            Message = $"High response time for {responseMetric.Endpoint}: {responseMetric.AverageResponseTime:F2}ms",
                            Severity = AlertSeverity.Warning,
                            Threshold = responseTimeThreshold,
                            CurrentValue = responseMetric.AverageResponseTime,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }

                // Check error rate alerts
                foreach (var metric in _metrics.Values.Where(m => m.ErrorCount > 0))
                {
                    var errorRate = metric.TotalCount > 0 ? (double)metric.ErrorCount / metric.TotalCount * 100 : 0;
                    if (errorRate > errorRateThreshold)
                    {
                        alerts.Add(new AlertCondition
                        {
                            Type = "ErrorRate",
                            Message = $"High error rate for {metric.Name}: {errorRate:F2}%",
                            Severity = AlertSeverity.Critical,
                            Threshold = errorRateThreshold,
                            CurrentValue = errorRate,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
            }

            return alerts;
        }

        #endregion

        #region IEspnAlertingService Implementation

        public async Task ProcessAlertsAsync(List<AlertCondition> alerts)
        {
            foreach (var alert in alerts)
            {
                await SendAlertAsync(alert);
            }
        }

        public async Task SendAlertAsync(AlertCondition alert)
        {
            var alertRecord = new AlertRecord
            {
                Id = Guid.NewGuid().ToString(),
                Type = alert.Type,
                Message = alert.Message,
                Severity = alert.Severity,
                Timestamp = alert.Timestamp,
                State = AlertState.Active,
                Metadata = alert.Metadata ?? new Dictionary<string, object>()
            };

            lock (_alertLock)
            {
                _activeAlerts[alertRecord.Id] = alertRecord;
                _alertHistory.Add(alertRecord);
            }

            _logger.LogWarning("Alert triggered: {AlertType} - {AlertMessage} (Severity: {Severity})",
                alert.Type, alert.Message, alert.Severity);

            // In a real implementation, you would send notifications here
            // For now, we'll just log the alert
            await Task.CompletedTask;
        }

        public List<AlertRecord> GetAlertHistory(DateTime? from = null, DateTime? to = null)
        {
            lock (_alertLock)
            {
                var query = _alertHistory.AsEnumerable();

                if (from.HasValue)
                    query = query.Where(a => a.Timestamp >= from.Value);

                if (to.HasValue)
                    query = query.Where(a => a.Timestamp <= to.Value);

                return query.OrderByDescending(a => a.Timestamp).ToList();
            }
        }

        public void ClearResolvedAlerts()
        {
            lock (_alertLock)
            {
                var resolvedAlerts = _activeAlerts.Values.Where(a => a.State == AlertState.Resolved).ToList();
                foreach (var alert in resolvedAlerts)
                {
                    _activeAlerts.TryRemove(alert.Id, out _);
                }
            }

            _logger.LogInformation("Cleared {Count} resolved alerts", _activeAlerts.Count(a => a.Value.State == AlertState.Resolved));
        }

        public List<AlertRecord> GetActiveAlerts()
        {
            lock (_alertLock)
            {
                return _activeAlerts.Values.Where(a => a.State == AlertState.Active).ToList();
            }
        }

        public async Task CheckSyncAlertsAsync(SyncResult syncResult, CancellationToken cancellationToken = default)
        {
            if (!syncResult.IsSuccessful)
            {
                var errorMessage = syncResult.Errors.Any() ? string.Join("; ", syncResult.Errors) : "Unknown error";
                var alert = new AlertCondition
                {
                    Type = "SyncFailure",
                    Message = $"Sync operation failed for {syncResult.SyncType}: {errorMessage}",
                    Severity = AlertSeverity.Critical,
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["SyncType"] = syncResult.SyncType,
                        ["Duration"] = syncResult.Duration.TotalMilliseconds,
                        ["RecordsProcessed"] = syncResult.RecordsProcessed,
                        ["ErrorMessage"] = errorMessage
                    }
                };

                await SendAlertAsync(alert);
            }
        }

        public async Task CheckDataQualityAlertsAsync(CancellationToken cancellationToken = default)
        {
            // Implement data quality checks here
            // This would typically check for data anomalies, missing required fields, etc.
            await Task.CompletedTask;
        }

        public async Task CheckPerformanceAlertsAsync(CancellationToken cancellationToken = default)
        {
            var alertConditions = CheckAlertConditions();
            if (alertConditions.Any())
            {
                await ProcessAlertsAsync(alertConditions);
            }
        }

        public async Task CheckHealthAlertsAsync(HealthReport healthReport, CancellationToken cancellationToken = default)
        {
            foreach (var entry in healthReport.Entries.Where(e => e.Value.Status != HealthStatus.Healthy))
            {
                var severity = entry.Value.Status == HealthStatus.Degraded ? AlertSeverity.Warning : AlertSeverity.Critical;

                var alert = new AlertCondition
                {
                    Type = "HealthCheck",
                    Message = $"Health check '{entry.Key}' is {entry.Value.Status}: {entry.Value.Description}",
                    Severity = severity,
                    Timestamp = DateTime.UtcNow,
                    Metadata = new Dictionary<string, object>
                    {
                        ["CheckName"] = entry.Key,
                        ["Status"] = entry.Value.Status.ToString(),
                        ["Description"] = entry.Value.Description ?? "",
                        ["Duration"] = entry.Value.Duration.TotalMilliseconds
                    }
                };

                await SendAlertAsync(alert);
            }
        }

        public async Task<AlertStatistics> GetAlertStatisticsAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;

            lock (_alertLock)
            {
                var last24Hours = DateTime.UtcNow.AddHours(-24);
                var recentAlerts = _alertHistory.Where(a => a.Timestamp >= last24Hours).ToList();

                return new AlertStatistics
                {
                    TotalAlerts = _alertHistory.Count,
                    ActiveAlerts = _activeAlerts.Count(a => a.Value.State == AlertState.Active),
                    ResolvedAlerts = _alertHistory.Count(a => a.State == AlertState.Resolved),
                    AlertsLast24Hours = recentAlerts.Count,
                    CriticalAlertsLast24Hours = recentAlerts.Count(a => a.Severity == AlertSeverity.Critical),
                    WarningAlertsLast24Hours = recentAlerts.Count(a => a.Severity == AlertSeverity.Warning),
                    InfoAlertsLast24Hours = recentAlerts.Count(a => a.Severity == AlertSeverity.Info),
                    MostCommonAlertType = _alertHistory.GroupBy(a => a.Type)
                        .OrderByDescending(g => g.Count())
                        .FirstOrDefault()?.Key ?? "None",
                    AverageResolutionTime = _alertHistory
                        .Where(a => a.State == AlertState.Resolved && a.ResolvedAt.HasValue)
                        .Select(a => a.ResolvedAt!.Value - a.Timestamp)
                        .DefaultIfEmpty()
                        .Average(ts => ts.TotalMinutes)
                };
            }
        }

        #endregion
    }

    #region Supporting Classes

    /// <summary>
    /// Disposable class for timed operations
    /// </summary>
    public class TimedOperation : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly Stopwatch _stopwatch;
        private readonly List<IDisposable> _propertyDisposables;
        private readonly string _correlationId;
        private bool _isCompleted;
        private bool? _success;

        public TimedOperation(ILogger logger, string operationName, Dictionary<string, object>? properties = null, string? correlationId = null)
        {
            _logger = logger;
            _operationName = operationName;
            _correlationId = correlationId ?? Guid.NewGuid().ToString("N")[..8];
            _stopwatch = Stopwatch.StartNew();
            _propertyDisposables = new List<IDisposable>
            {
                LogContext.PushProperty("OperationName", operationName),
                LogContext.PushProperty("CorrelationId", _correlationId)
            };

            if (properties != null)
            {
                foreach (var prop in properties)
                {
                    _propertyDisposables.Add(LogContext.PushProperty(prop.Key, prop.Value));
                }
            }

            _logger.LogDebug("Started timed operation {OperationName}", operationName);
        }

        public TimeSpan ElapsedTime => _stopwatch.Elapsed;
        public bool IsCompleted => _isCompleted;
        public bool Success => _success ?? false;

        public void Complete(bool success)
        {
            if (_isCompleted) return;

            _stopwatch.Stop();
            _isCompleted = true;
            _success = success;

            using (LogContext.PushProperty("Duration", _stopwatch.ElapsedMilliseconds))
            using (LogContext.PushProperty("Success", success))
            using (LogContext.PushProperty("CorrelationId", _correlationId))
            {
                var logLevel = success ? LogLevel.Information : LogLevel.Warning;
                _logger.Log(logLevel, "Completed timed operation {OperationName} in {Duration}ms - {Status}",
                    _operationName, _stopwatch.ElapsedMilliseconds, success ? "SUCCESS" : "FAILED");
            }
        }

        public void Dispose()
        {
            if (!_isCompleted)
            {
                Complete(true); // Default to success if not explicitly completed
            }

            foreach (var disposable in _propertyDisposables)
            {
                disposable.Dispose();
            }
        }
    }

    /// <summary>
    /// Disposable class for correlation context
    /// </summary>
    internal class CorrelationContext : IDisposable
    {
        private readonly Action _restoreAction;

        public CorrelationContext(Action restoreAction)
        {
            _restoreAction = restoreAction;
        }

        public void Dispose()
        {
            _restoreAction();
        }
    }

    /// <summary>
    /// Metric data container
    /// </summary>
    public class MetricData
    {
        public string Name { get; set; } = string.Empty;
        public double TotalValue { get; set; }
        public double MinValue { get; set; } = double.MaxValue;
        public double MaxValue { get; set; } = double.MinValue;
        public int Count { get; set; }
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Tags { get; set; } = new();
        public Dictionary<string, double> AdditionalValues { get; set; } = new();

        public double AverageValue => Count > 0 ? TotalValue / Count : 0;
        public int TotalCount => SuccessCount + ErrorCount;
        public double SuccessRate => TotalCount > 0 ? (double)SuccessCount / TotalCount * 100 : 0;

        public void RecordValue(double value, string? key = null)
        {
            if (key == null)
            {
                TotalValue += value;
                Count++;
                MinValue = Math.Min(MinValue, value);
                MaxValue = Math.Max(MaxValue, value);
            }
            else
            {
                AdditionalValues[key] = value;
            }

            LastUpdated = DateTime.UtcNow;
        }

        public void IncrementCount(int successIncrement, int errorIncrement)
        {
            SuccessCount += successIncrement;
            ErrorCount += errorIncrement;
            LastUpdated = DateTime.UtcNow;
        }

        public MetricData Copy()
        {
            return new MetricData
            {
                Name = Name,
                TotalValue = TotalValue,
                MinValue = MinValue,
                MaxValue = MaxValue,
                Count = Count,
                SuccessCount = SuccessCount,
                ErrorCount = ErrorCount,
                LastUpdated = LastUpdated,
                Tags = new Dictionary<string, string>(Tags),
                AdditionalValues = new Dictionary<string, double>(AdditionalValues)
            };
        }
    }

    /// <summary>
    /// Response time metrics
    /// </summary>
    public class ResponseTimeMetrics
    {
        public string Endpoint { get; set; } = string.Empty;
        public double TotalResponseTime { get; set; }
        public double MinResponseTime { get; set; } = double.MaxValue;
        public double MaxResponseTime { get; set; } = double.MinValue;
        public int RequestCount { get; set; }
        public int SuccessfulRequests { get; set; }
        public int FailedRequests { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public double AverageResponseTime => RequestCount > 0 ? TotalResponseTime / RequestCount : 0;
        public double SuccessRate => (SuccessfulRequests + FailedRequests) > 0 ?
            (double)SuccessfulRequests / (SuccessfulRequests + FailedRequests) * 100 : 0;

        public void RecordResponseTime(TimeSpan responseTime, bool success)
        {
            var ms = responseTime.TotalMilliseconds;
            TotalResponseTime += ms;
            RequestCount++;
            MinResponseTime = Math.Min(MinResponseTime, ms);
            MaxResponseTime = Math.Max(MaxResponseTime, ms);

            if (success)
                SuccessfulRequests++;
            else
                FailedRequests++;

            LastUpdated = DateTime.UtcNow;
        }

        public ResponseTimeMetrics Copy()
        {
            return new ResponseTimeMetrics
            {
                Endpoint = Endpoint,
                TotalResponseTime = TotalResponseTime,
                MinResponseTime = MinResponseTime,
                MaxResponseTime = MaxResponseTime,
                RequestCount = RequestCount,
                SuccessfulRequests = SuccessfulRequests,
                FailedRequests = FailedRequests,
                LastUpdated = LastUpdated
            };
        }
    }

    /// <summary>
    /// Cache operation metrics
    /// </summary>
    public class CacheMetrics
    {
        public string Operation { get; set; } = string.Empty;
        public int HitCount { get; set; }
        public int MissCount { get; set; }
        public double TotalDuration { get; set; }
        public int OperationCount { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

        public double HitRate => (HitCount + MissCount) > 0 ? (double)HitCount / (HitCount + MissCount) * 100 : 0;
        public double AverageDuration => OperationCount > 0 ? TotalDuration / OperationCount : 0;

        public void RecordOperation(bool hit, TimeSpan? duration)
        {
            if (hit)
                HitCount++;
            else
                MissCount++;

            if (duration.HasValue)
            {
                TotalDuration += duration.Value.TotalMilliseconds;
                OperationCount++;
            }

            LastUpdated = DateTime.UtcNow;
        }

        public CacheMetrics Copy()
        {
            return new CacheMetrics
            {
                Operation = Operation,
                HitCount = HitCount,
                MissCount = MissCount,
                TotalDuration = TotalDuration,
                OperationCount = OperationCount,
                LastUpdated = LastUpdated
            };
        }
    }

    /// <summary>
    /// Metrics snapshot
    /// </summary>
    public class MetricsSnapshot
    {
        public DateTime Timestamp { get; set; }
        public Dictionary<string, MetricData> Metrics { get; set; } = new();
        public Dictionary<string, ResponseTimeMetrics> ResponseTimeMetrics { get; set; } = new();
        public Dictionary<string, CacheMetrics> CacheMetrics { get; set; } = new();
    }

    /// <summary>
    /// Operation timer for metrics
    /// </summary>
    public class OperationTimer : IDisposable
    {
        private readonly EspnInfrastructureService _metricsService;
        private readonly string _operationName;
        private readonly Dictionary<string, object>? _metadata;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;

        public OperationTimer(EspnInfrastructureService metricsService, string operationName, Dictionary<string, object>? metadata = null)
        {
            _metricsService = metricsService;
            _operationName = operationName;
            _metadata = metadata;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();

                // Record the operation duration as a business metric
                _metricsService.RecordBusinessMetric($"operation_{_operationName}_duration", _stopwatch.ElapsedMilliseconds);

                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Alert condition
    /// </summary>
    public class AlertCondition
    {
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public double? Threshold { get; set; }
        public double? CurrentValue { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, object>? Metadata { get; set; }
    }

    /// <summary>
    /// Alert record
    /// </summary>
    public class AlertRecord
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public AlertSeverity Severity { get; set; }
        public AlertState State { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolvedBy { get; set; }
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Alert state enumeration
    /// </summary>
    public enum AlertState
    {
        Active,
        Acknowledged,
        Resolved,
        Suppressed
    }

    /// <summary>
    /// Alert severity enumeration
    /// </summary>
    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical,
        Emergency
    }

    /// <summary>
    /// Alert statistics
    /// </summary>
    public class AlertStatistics
    {
        public int TotalAlerts { get; set; }
        public int ActiveAlerts { get; set; }
        public int ResolvedAlerts { get; set; }
        public int AlertsLast24Hours { get; set; }
        public int CriticalAlertsLast24Hours { get; set; }
        public int WarningAlertsLast24Hours { get; set; }
        public int InfoAlertsLast24Hours { get; set; }
        public string MostCommonAlertType { get; set; } = string.Empty;
        public double AverageResolutionTime { get; set; }
    }

    #endregion
}