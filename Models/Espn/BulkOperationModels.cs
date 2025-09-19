using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    /// <summary>
    /// Configuration options for bulk operations
    /// </summary>
    public class BulkOperationOptions
    {
        /// <summary>
        /// Maximum number of concurrent operations
        /// </summary>
        [JsonPropertyName("maxConcurrency")]
        public int MaxConcurrency { get; set; } = 5;

        /// <summary>
        /// Batch size for processing items in chunks
        /// </summary>
        [JsonPropertyName("batchSize")]
        public int BatchSize { get; set; } = 10;

        /// <summary>
        /// Whether to continue processing if individual items fail
        /// </summary>
        [JsonPropertyName("continueOnError")]
        public bool ContinueOnError { get; set; } = true;

        /// <summary>
        /// Maximum number of retries for failed items
        /// </summary>
        [JsonPropertyName("maxRetries")]
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Delay between retry attempts
        /// </summary>
        [JsonPropertyName("retryDelay")]
        public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Whether to report progress updates
        /// </summary>
        [JsonPropertyName("enableProgressReporting")]
        public bool EnableProgressReporting { get; set; } = true;

        /// <summary>
        /// Interval for progress updates
        /// </summary>
        [JsonPropertyName("progressUpdateInterval")]
        public TimeSpan ProgressUpdateInterval { get; set; } = TimeSpan.FromSeconds(5);

        /// <summary>
        /// Whether to collect performance metrics
        /// </summary>
        [JsonPropertyName("enableMetrics")]
        public bool EnableMetrics { get; set; } = true;

        /// <summary>
        /// Maximum memory usage threshold (in bytes) before pausing operations
        /// </summary>
        [JsonPropertyName("maxMemoryThreshold")]
        public long MaxMemoryThreshold { get; set; } = 1024 * 1024 * 1024; // 1GB

        /// <summary>
        /// Whether to use streaming for large JSON responses
        /// </summary>
        [JsonPropertyName("useStreamingParsing")]
        public bool UseStreamingParsing { get; set; } = true;

        /// <summary>
        /// Cancellation token for the entire operation
        /// </summary>
        [JsonIgnore]
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
    }

    /// <summary>
    /// Represents a bulk operation request for multiple weeks of data
    /// </summary>
    public class BulkWeekRequest
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("weekNumbers")]
        public List<int> WeekNumbers { get; set; } = new();

        [JsonPropertyName("seasonType")]
        public int SeasonType { get; set; } = 2;

        [JsonPropertyName("options")]
        public BulkOperationOptions Options { get; set; } = new();
    }

    /// <summary>
    /// Represents a bulk operation request for multiple seasons
    /// </summary>
    public class BulkSeasonRequest
    {
        [JsonPropertyName("years")]
        public List<int> Years { get; set; } = new();

        [JsonPropertyName("seasonTypes")]
        public List<int> SeasonTypes { get; set; } = new() { 2 }; // Default to regular season

        [JsonPropertyName("options")]
        public BulkOperationOptions Options { get; set; } = new();
    }
}