using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ESPNScrape.Configuration;
using Serilog.Context;
using System.Diagnostics;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Enhanced logging service with structured logging, correlation IDs, and performance tracking
    /// </summary>
    public interface IEspnLoggingService
    {
        /// <summary>
        /// Log ESPN API operation with performance metrics
        /// </summary>
        void LogApiOperation(string endpoint, string method, TimeSpan responseTime, int statusCode, bool success, string? errorMessage = null);

        /// <summary>
        /// Log cache operation with hit/miss metrics
        /// </summary>
        void LogCacheOperation(string operation, string key, bool hit, TimeSpan? duration = null);

        /// <summary>
        /// Log business metric
        /// </summary>
        void LogBusinessMetric(string metricName, object value, Dictionary<string, object>? additionalProperties = null);

        /// <summary>
        /// Log bulk operation progress
        /// </summary>
        void LogBulkOperationProgress(string operationId, string operationType, int completed, int total, TimeSpan elapsed);

        /// <summary>
        /// Start a timed operation
        /// </summary>
        IDisposable BeginTimedOperation(string operationName, Dictionary<string, object>? properties = null);

        /// <summary>
        /// Create a correlation context for the current operation
        /// </summary>
        IDisposable BeginCorrelationContext(string correlationId);

        /// <summary>
        /// Get or generate correlation ID for current context
        /// </summary>
        string GetOrGenerateCorrelationId();

        /// <summary>
        /// Log structured data with correlation context
        /// </summary>
        void LogStructured(LogLevel level, string messageTemplate, params object[] args);

        /// <summary>
        /// Log health check result
        /// </summary>
        void LogHealthCheck(string checkName, bool healthy, TimeSpan duration, string? details = null);

        /// <summary>
        /// Log performance metrics
        /// </summary>
        void LogPerformanceMetrics(string component, Dictionary<string, object> metrics);

        /// <summary>
        /// Log error with exception
        /// </summary>
        void LogError(Exception exception, string message, Dictionary<string, object>? context = null);

        /// <summary>
        /// Set correlation context
        /// </summary>
        void SetCorrelationContext(string correlationId);

        /// <summary>
        /// Get current correlation ID
        /// </summary>
        string? GetCurrentCorrelationId();

        /// <summary>
        /// Clear correlation context
        /// </summary>
        void ClearCorrelationContext();

        /// <summary>
        /// Log health status
        /// </summary>
        void LogHealth(string componentName, bool healthy, TimeSpan duration, Dictionary<string, object>? details = null);

        /// <summary>
        /// Log bulk operation
        /// </summary>
        void LogBulkOperation(string operationType, int itemsProcessed, TimeSpan duration, int errorCount, bool success);

        /// <summary>
        /// Log configuration load
        /// </summary>
        void LogConfigurationLoad(string configSection, bool success, Dictionary<string, object>? details = null);

        /// <summary>
        /// Log performance metric
        /// </summary>
        void LogPerformanceMetric(string metricName, double value, string unit, Dictionary<string, string>? tags = null);
    }

    /// <summary>
    /// Implementation of enhanced logging service
    /// </summary>
    public class EspnLoggingService : IEspnLoggingService
    {
        private readonly ILogger<EspnLoggingService> _logger;
        private readonly LoggingConfiguration _config;
        private static readonly AsyncLocal<string?> _correlationId = new();
        private const string CorrelationIdProperty = "CorrelationId";

        public EspnLoggingService(ILogger<EspnLoggingService> logger, IOptions<LoggingConfiguration> options)
        {
            _logger = logger;
            _config = options.Value;
        }

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
            var newId = Guid.NewGuid().ToString("N")[..8]; // Use first 8 characters for brevity
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
        }
    }

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
}