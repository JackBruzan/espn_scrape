using Microsoft.Extensions.Diagnostics.HealthChecks;
using ESPNScrape.Models.DataSync;
using Microsoft.Extensions.Logging;

namespace ESPNScrape.Services.Infrastructure.Interfaces
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
        void LogStructured(Microsoft.Extensions.Logging.LogLevel level, string messageTemplate, params object[] args);

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
    /// Service for collecting and managing performance metrics
    /// </summary>
    public interface IEspnMetricsService
    {
        /// <summary>
        /// Record sync operation performance
        /// </summary>
        void RecordSyncPerformance(SyncResult syncResult);

        /// <summary>
        /// Record player matching accuracy
        /// </summary>
        void RecordPlayerMatchingAccuracy(int totalPlayers, int successfulMatches, int manualReviewRequired);

        /// <summary>
        /// Record database operation performance
        /// </summary>
        void RecordDatabaseOperation(string operationType, TimeSpan duration, int recordsAffected, bool success = true);

        /// <summary>
        /// Start timing an operation (returns IDisposable for automatic timing)
        /// </summary>
        IDisposable StartOperation(string operationName, Dictionary<string, object>? metadata = null);

        /// <summary>
        /// Record API response time
        /// </summary>
        void RecordApiResponseTime(string endpoint, TimeSpan responseTime, bool success);

        /// <summary>
        /// Record cache operation
        /// </summary>
        void RecordCacheOperation(string operation, bool hit, TimeSpan? duration = null);

        /// <summary>
        /// Record business metric
        /// </summary>
        void RecordBusinessMetric(string metricName, double value, Dictionary<string, string>? tags = null);

        /// <summary>
        /// Record bulk operation metrics
        /// </summary>
        void RecordBulkOperationMetrics(string operationType, int itemsProcessed, TimeSpan duration, int errorCount);

        /// <summary>
        /// Record memory usage
        /// </summary>
        void RecordMemoryUsage(long bytesUsed);

        /// <summary>
        /// Record health check result
        /// </summary>
        void RecordHealthCheck(string checkName, bool healthy, TimeSpan duration);

        /// <summary>
        /// Get current metrics snapshot
        /// </summary>
        MetricsSnapshot GetCurrentMetrics();

        /// <summary>
        /// Get metrics for a specific time range
        /// </summary>
        MetricsSnapshot GetMetrics(DateTime from, DateTime to);

        /// <summary>
        /// Reset all metrics
        /// </summary>
        void ResetMetrics();

        /// <summary>
        /// Check if any alert thresholds are exceeded
        /// </summary>
        List<AlertCondition> CheckAlertConditions();
    }

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
        Task CheckSyncAlertsAsync(SyncResult syncResult, CancellationToken cancellationToken = default);

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
        Task CheckHealthAlertsAsync(HealthReport healthReport, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get alert statistics
        /// </summary>
        Task<AlertStatistics> GetAlertStatisticsAsync(CancellationToken cancellationToken = default);
    }
}