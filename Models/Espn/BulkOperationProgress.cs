using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    /// <summary>
    /// Represents progress information for long-running bulk operations
    /// </summary>
    public class BulkOperationProgress
    {
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; } = string.Empty;

        [JsonPropertyName("operationType")]
        public string OperationType { get; set; } = string.Empty;

        [JsonPropertyName("totalItems")]
        public int TotalItems { get; set; }

        [JsonPropertyName("completedItems")]
        public int CompletedItems { get; set; }

        [JsonPropertyName("failedItems")]
        public int FailedItems { get; set; }

        [JsonPropertyName("currentItem")]
        public string CurrentItem { get; set; } = string.Empty;

        [JsonPropertyName("startTime")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("lastUpdateTime")]
        public DateTime LastUpdateTime { get; set; }

        [JsonPropertyName("estimatedTimeRemaining")]
        public TimeSpan? EstimatedTimeRemaining { get; set; }

        [JsonPropertyName("isCompleted")]
        public bool IsCompleted { get; set; }

        [JsonPropertyName("errorMessages")]
        public List<string> ErrorMessages { get; set; } = new();

        [JsonPropertyName("performanceMetrics")]
        public BulkOperationMetrics? Metrics { get; set; }

        /// <summary>
        /// Calculates the completion percentage (0-100)
        /// </summary>
        public double PercentageComplete => TotalItems > 0 ? (double)CompletedItems / TotalItems * 100 : 0;
    }

    /// <summary>
    /// Performance metrics for bulk operations
    /// </summary>
    public class BulkOperationMetrics
    {
        [JsonPropertyName("averageItemProcessingTime")]
        public TimeSpan AverageItemProcessingTime { get; set; }

        [JsonPropertyName("itemsPerSecond")]
        public double ItemsPerSecond { get; set; }

        [JsonPropertyName("totalApiCalls")]
        public int TotalApiCalls { get; set; }

        [JsonPropertyName("cacheHitRate")]
        public double CacheHitRate { get; set; }

        [JsonPropertyName("peakMemoryUsage")]
        public long PeakMemoryUsage { get; set; }

        [JsonPropertyName("totalDataProcessed")]
        public long TotalDataProcessed { get; set; }
    }
}