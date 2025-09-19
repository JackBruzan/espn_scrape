namespace ESPNScrape.Configuration
{
    /// <summary>
    /// Configuration settings for bulk operations and performance optimization
    /// </summary>
    public class BulkOperationsConfiguration
    {
        /// <summary>
        /// Default maximum concurrency for bulk operations
        /// </summary>
        public int DefaultMaxConcurrency { get; set; } = 5;

        /// <summary>
        /// Default batch size for processing items in chunks
        /// </summary>
        public int DefaultBatchSize { get; set; } = 10;

        /// <summary>
        /// Default maximum number of retries for failed operations
        /// </summary>
        public int DefaultMaxRetries { get; set; } = 3;

        /// <summary>
        /// Default delay between retry attempts
        /// </summary>
        public TimeSpan DefaultRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Default memory threshold (in bytes) before triggering optimization
        /// </summary>
        public long DefaultMaxMemoryThreshold { get; set; } = 1024 * 1024 * 1024; // 1GB

        /// <summary>
        /// Default interval for progress updates
        /// </summary>
        public TimeSpan DefaultProgressUpdateInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Whether to enable performance metrics collection by default
        /// </summary>
        public bool EnableMetricsByDefault { get; set; } = true;

        /// <summary>
        /// Whether to enable progress reporting by default
        /// </summary>
        public bool EnableProgressReportingByDefault { get; set; } = true;

        /// <summary>
        /// Whether to use streaming JSON parsing by default
        /// </summary>
        public bool UseStreamingParsingByDefault { get; set; } = true;

        /// <summary>
        /// Whether to continue processing when individual items fail
        /// </summary>
        public bool ContinueOnErrorByDefault { get; set; } = true;

        /// <summary>
        /// Performance optimization settings
        /// </summary>
        public PerformanceOptimizationConfig PerformanceOptimization { get; set; } = new();

        /// <summary>
        /// Memory management settings
        /// </summary>
        public MemoryManagementConfig MemoryManagement { get; set; } = new();

        /// <summary>
        /// Progress reporting settings
        /// </summary>
        public ProgressReportingConfig ProgressReporting { get; set; } = new();
    }

    /// <summary>
    /// Performance optimization configuration
    /// </summary>
    public class PerformanceOptimizationConfig
    {
        /// <summary>
        /// Enable automatic garbage collection optimization
        /// </summary>
        public bool EnableAutoGarbageCollection { get; set; } = true;

        /// <summary>
        /// Interval for automatic memory optimization checks
        /// </summary>
        public TimeSpan MemoryOptimizationInterval { get; set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Enable parallel processing for compatible operations
        /// </summary>
        public bool EnableParallelProcessing { get; set; } = true;

        /// <summary>
        /// Maximum degree of parallelism for CPU-bound operations
        /// </summary>
        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Enable streaming for large JSON responses
        /// </summary>
        public bool EnableStreamingParsing { get; set; } = true;

        /// <summary>
        /// Minimum response size (in bytes) to trigger streaming parsing
        /// </summary>
        public long StreamingParsingThreshold { get; set; } = 1024 * 1024; // 1MB
    }

    /// <summary>
    /// Memory management configuration
    /// </summary>
    public class MemoryManagementConfig
    {
        /// <summary>
        /// Enable memory monitoring during bulk operations
        /// </summary>
        public bool EnableMemoryMonitoring { get; set; } = true;

        /// <summary>
        /// Memory usage warning threshold (percentage of max threshold)
        /// </summary>
        public double WarningThresholdPercentage { get; set; } = 0.8; // 80%

        /// <summary>
        /// Force garbage collection when memory threshold is exceeded
        /// </summary>
        public bool ForceGCOnThresholdExceeded { get; set; } = true;

        /// <summary>
        /// Delay operations when memory usage is critical
        /// </summary>
        public bool DelayOnCriticalMemory { get; set; } = true;

        /// <summary>
        /// Delay duration when memory usage is critical
        /// </summary>
        public TimeSpan CriticalMemoryDelay { get; set; } = TimeSpan.FromSeconds(5);
    }

    /// <summary>
    /// Progress reporting configuration
    /// </summary>
    public class ProgressReportingConfig
    {
        /// <summary>
        /// Minimum interval between progress updates
        /// </summary>
        public TimeSpan MinUpdateInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Maximum interval between progress updates
        /// </summary>
        public TimeSpan MaxUpdateInterval { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Include detailed performance metrics in progress reports
        /// </summary>
        public bool IncludePerformanceMetrics { get; set; } = true;

        /// <summary>
        /// Include memory usage information in progress reports
        /// </summary>
        public bool IncludeMemoryUsage { get; set; } = true;

        /// <summary>
        /// Include estimated time remaining in progress reports
        /// </summary>
        public bool IncludeTimeEstimates { get; set; } = true;

        /// <summary>
        /// Log progress updates to the logger
        /// </summary>
        public bool LogProgressUpdates { get; set; } = true;

        /// <summary>
        /// Log level for progress updates
        /// </summary>
        public string ProgressLogLevel { get; set; } = "Information";
    }
}