using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Service responsible for bulk operations and performance-optimized data collection
    /// </summary>
    public interface IEspnBulkOperationsService
    {
        /// <summary>
        /// Collects player statistics for multiple weeks in parallel with progress reporting
        /// </summary>
        /// <param name="request">Bulk week request with options</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <returns>Collection of all player statistics from specified weeks</returns>
        Task<IEnumerable<PlayerStats>> GetBulkWeekPlayerStatsAsync(
            BulkWeekRequest request,
            IProgress<BulkOperationProgress>? progressCallback = null);

        /// <summary>
        /// Collects player statistics for multiple seasons in parallel with progress reporting
        /// </summary>
        /// <param name="request">Bulk season request with options</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <returns>Collection of all player statistics from specified seasons</returns>
        Task<IEnumerable<PlayerStats>> GetBulkSeasonPlayerStatsAsync(
            BulkSeasonRequest request,
            IProgress<BulkOperationProgress>? progressCallback = null);

        /// <summary>
        /// Processes multiple game events in parallel with optimized memory usage
        /// </summary>
        /// <param name="eventIds">Collection of ESPN event IDs</param>
        /// <param name="options">Bulk operation options</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <returns>Collection of player statistics from all games</returns>
        Task<IEnumerable<PlayerStats>> GetBulkGamePlayerStatsAsync(
            IEnumerable<string> eventIds,
            BulkOperationOptions options,
            IProgress<BulkOperationProgress>? progressCallback = null);

        /// <summary>
        /// Streams large JSON responses for memory-efficient processing
        /// </summary>
        /// <param name="jsonContent">Large JSON content to parse</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of parsed objects</returns>
        IAsyncEnumerable<T> StreamParseJsonAsync<T>(
            string jsonContent,
            CancellationToken cancellationToken = default) where T : class;

        /// <summary>
        /// Processes items in batches with memory monitoring
        /// </summary>
        /// <param name="items">Items to process</param>
        /// <param name="processor">Function to process each batch</param>
        /// <param name="options">Bulk operation options</param>
        /// <param name="progressCallback">Optional callback for progress updates</param>
        /// <returns>Collection of processed results</returns>
        Task<IEnumerable<TResult>> ProcessInBatchesAsync<TItem, TResult>(
            IEnumerable<TItem> items,
            Func<IEnumerable<TItem>, CancellationToken, Task<IEnumerable<TResult>>> processor,
            BulkOperationOptions options,
            IProgress<BulkOperationProgress>? progressCallback = null);

        /// <summary>
        /// Gets current memory usage for monitoring
        /// </summary>
        /// <returns>Current memory usage in bytes</returns>
        long GetCurrentMemoryUsage();

        /// <summary>
        /// Forces garbage collection to free memory during bulk operations
        /// </summary>
        void OptimizeMemoryUsage();

        /// <summary>
        /// Validates bulk operation parameters before execution
        /// </summary>
        /// <param name="options">Options to validate</param>
        /// <returns>Validation result with any error messages</returns>
        ValidationResult ValidateBulkOperationOptions(BulkOperationOptions options);

        /// <summary>
        /// Creates an optimized concurrent semaphore for bulk operations
        /// </summary>
        /// <param name="maxConcurrency">Maximum concurrent operations</param>
        /// <returns>Configured semaphore</returns>
        SemaphoreSlim CreateOptimizedSemaphore(int maxConcurrency);

        /// <summary>
        /// Estimates completion time for bulk operations
        /// </summary>
        /// <param name="totalItems">Total number of items to process</param>
        /// <param name="completedItems">Number of completed items</param>
        /// <param name="elapsedTime">Time elapsed so far</param>
        /// <returns>Estimated time remaining</returns>
        TimeSpan EstimateTimeRemaining(int totalItems, int completedItems, TimeSpan elapsedTime);
    }

    /// <summary>
    /// Validation result for bulk operation parameters
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
        public List<string> WarningMessages { get; set; } = new();
    }
}