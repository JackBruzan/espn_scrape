namespace ESPNScrape.Configuration
{
    /// <summary>
    /// Configuration settings for comprehensive logging and monitoring
    /// </summary>
    public class LoggingConfiguration
    {
        /// <summary>
        /// Structured logging configuration
        /// </summary>
        public StructuredLoggingConfig StructuredLogging { get; set; } = new();

        /// <summary>
        /// Performance metrics configuration
        /// </summary>
        public PerformanceMetricsConfig PerformanceMetrics { get; set; } = new();

        /// <summary>
        /// Business metrics configuration
        /// </summary>
        public BusinessMetricsConfig BusinessMetrics { get; set; } = new();

        /// <summary>
        /// Request correlation configuration
        /// </summary>
        public CorrelationConfig Correlation { get; set; } = new();

        /// <summary>
        /// Health monitoring configuration
        /// </summary>
        public HealthMonitoringConfig HealthMonitoring { get; set; } = new();

        /// <summary>
        /// Alerting configuration
        /// </summary>
        public AlertingConfig Alerting { get; set; } = new();
    }

    /// <summary>
    /// Structured logging configuration
    /// </summary>
    public class StructuredLoggingConfig
    {
        /// <summary>
        /// Enable structured logging with Serilog
        /// </summary>
        public bool EnableStructuredLogging { get; set; } = true;

        /// <summary>
        /// Use JSON format for logs
        /// </summary>
        public bool UseJsonFormat { get; set; } = true;

        /// <summary>
        /// Include scopes in logs
        /// </summary>
        public bool IncludeScopes { get; set; } = true;

        /// <summary>
        /// Include detailed request/response logging
        /// </summary>
        public bool IncludeRequestResponseLogging { get; set; } = true;

        /// <summary>
        /// Include performance timing in logs
        /// </summary>
        public bool IncludePerformanceTiming { get; set; } = true;

        /// <summary>
        /// Include memory usage in logs
        /// </summary>
        public bool IncludeMemoryUsage { get; set; } = true;

        /// <summary>
        /// Log level for ESPN API operations
        /// </summary>
        public string EspnApiLogLevel { get; set; } = "Information";

        /// <summary>
        /// Log level for cache operations
        /// </summary>
        public string CacheLogLevel { get; set; } = "Debug";

        /// <summary>
        /// Log level for health checks
        /// </summary>
        public string HealthCheckLogLevel { get; set; } = "Information";

        /// <summary>
        /// Enable JSON output format
        /// </summary>
        public bool EnableJsonFormat { get; set; } = true;

        /// <summary>
        /// Enable file logging
        /// </summary>
        public bool EnableFileLogging { get; set; } = true;

        /// <summary>
        /// File path template for logs
        /// </summary>
        public string FilePathTemplate { get; set; } = "logs/espn-scrape-{Date}.json";

        /// <summary>
        /// Rolling interval for log files
        /// </summary>
        public string RollingInterval { get; set; } = "Day";
    }

    /// <summary>
    /// Performance metrics configuration
    /// </summary>
    public class PerformanceMetricsConfig
    {
        /// <summary>
        /// Enable performance metrics collection
        /// </summary>
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Enable detailed metrics
        /// </summary>
        public bool EnableDetailedMetrics { get; set; } = true;

        /// <summary>
        /// Track response times for all ESPN API calls
        /// </summary>
        public bool TrackResponseTimes { get; set; } = true;

        /// <summary>
        /// Track cache hit/miss rates
        /// </summary>
        public bool TrackCacheMetrics { get; set; } = true;

        /// <summary>
        /// Track throughput metrics
        /// </summary>
        public bool TrackThroughputMetrics { get; set; } = true;

        /// <summary>
        /// Track error rates
        /// </summary>
        public bool TrackErrorRates { get; set; } = true;

        /// <summary>
        /// Track memory usage metrics
        /// </summary>
        public bool TrackMemoryMetrics { get; set; } = true;

        /// <summary>
        /// Metrics collection interval
        /// </summary>
        public TimeSpan MetricsCollectionInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Metrics retention period
        /// </summary>
        public TimeSpan MetricsRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
    }

    /// <summary>
    /// Business metrics configuration
    /// </summary>
    public class BusinessMetricsConfig
    {
        /// <summary>
        /// Enable business metrics tracking
        /// </summary>
        public bool EnableBusinessMetrics { get; set; } = true;

        /// <summary>
        /// Track API call counts
        /// </summary>
        public bool TrackApiCallCounts { get; set; } = true;

        /// <summary>
        /// Track games processed
        /// </summary>
        public bool TrackGamesProcessed { get; set; } = true;

        /// <summary>
        /// Track players extracted
        /// </summary>
        public bool TrackPlayersExtracted { get; set; } = true;

        /// <summary>
        /// Track data completeness
        /// </summary>
        public bool TrackDataCompleteness { get; set; } = true;

        /// <summary>
        /// Track processing efficiency
        /// </summary>
        public bool TrackProcessingEfficiency { get; set; } = true;

        /// <summary>
        /// Business metrics aggregation interval
        /// </summary>
        public TimeSpan AggregationInterval { get; set; } = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Request correlation configuration
    /// </summary>
    public class CorrelationConfig
    {
        /// <summary>
        /// Enable correlation ID tracking
        /// </summary>
        public bool EnableCorrelationIds { get; set; } = true;

        /// <summary>
        /// Correlation ID header name
        /// </summary>
        public string CorrelationIdHeaderName { get; set; } = "X-Correlation-ID";

        /// <summary>
        /// Include correlation ID in all logs
        /// </summary>
        public bool IncludeInLogs { get; set; } = true;

        /// <summary>
        /// Propagate correlation ID to external calls
        /// </summary>
        public bool PropagateToExternalCalls { get; set; } = true;

        /// <summary>
        /// Generate correlation ID if not present
        /// </summary>
        public bool GenerateIfMissing { get; set; } = true;
    }

    /// <summary>
    /// Health monitoring configuration
    /// </summary>
    public class HealthMonitoringConfig
    {
        /// <summary>
        /// Enable enhanced health monitoring
        /// </summary>
        public bool EnableHealthMonitoring { get; set; } = true;

        /// <summary>
        /// Health check interval
        /// </summary>
        public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Include detailed health metrics
        /// </summary>
        public bool IncludeDetailedMetrics { get; set; } = true;

        /// <summary>
        /// Enable dependency health checks
        /// </summary>
        public bool EnableDependencyChecks { get; set; } = true;

        /// <summary>
        /// Health check timeout
        /// </summary>
        public TimeSpan HealthCheckTimeout { get; set; } = TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Alerting configuration
    /// </summary>
    public class AlertingConfig
    {
        /// <summary>
        /// Enable alerting system
        /// </summary>
        public bool EnableAlerting { get; set; } = true;

        /// <summary>
        /// Send resolution notifications
        /// </summary>
        public bool SendResolutionNotifications { get; set; } = true;

        /// <summary>
        /// Enable email notifications
        /// </summary>
        public bool EmailEnabled { get; set; } = false;

        /// <summary>
        /// Enable webhook notifications
        /// </summary>
        public bool WebhookEnabled { get; set; } = false;

        /// <summary>
        /// Webhook URL for notifications
        /// </summary>
        public string? WebhookUrl { get; set; }

        /// <summary>
        /// Enable SMS notifications
        /// </summary>
        public bool SmsEnabled { get; set; } = false;

        /// <summary>
        /// Monitoring interval
        /// </summary>
        public TimeSpan MonitoringInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Error rate threshold for alerts (percentage)
        /// </summary>
        public double ErrorRateThreshold { get; set; } = 5.0;

        /// <summary>
        /// Response time threshold for alerts (milliseconds)
        /// </summary>
        public int ResponseTimeThresholdMs { get; set; } = 5000;

        /// <summary>
        /// Memory usage threshold for alerts (percentage)
        /// </summary>
        public double MemoryUsageThreshold { get; set; } = 80.0;

        /// <summary>
        /// Cache hit rate threshold for alerts (percentage)
        /// </summary>
        public double CacheHitRateThreshold { get; set; } = 70.0;

        /// <summary>
        /// Alert evaluation interval
        /// </summary>
        public TimeSpan AlertEvaluationInterval { get; set; } = TimeSpan.FromMinutes(1);

        /// <summary>
        /// Alert cooldown period
        /// </summary>
        public TimeSpan AlertCooldownPeriod { get; set; } = TimeSpan.FromMinutes(15);
    }
}