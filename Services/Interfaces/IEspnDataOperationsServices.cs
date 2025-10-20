using ESPNScrape.Models;
using ESPNScrape.Models.Espn;
using ESPNScrape.Models.Supabase;
using ESPNScrape.Models.DataSync;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Combined interface for Data Operations services
    /// </summary>
    public interface IEspnDataOperationsService :
        IEspnBulkOperationsService,
        IEspnDataSyncService,
        IEspnPlayerStatsService,
        IEspnScrapingService,
        ISupabaseDatabaseService
    {
    }

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
    /// Service for synchronizing ESPN data with the database
    /// </summary>
    public interface IEspnDataSyncService
    {
        /// <summary>
        /// Synchronize player roster data from ESPN
        /// </summary>
        /// <param name="options">Sync configuration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Sync result with detailed metrics</returns>
        Task<SyncResult> SyncPlayersAsync(SyncOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Synchronize player statistics for a specific season and week
        /// </summary>
        /// <param name="season">NFL season year</param>
        /// <param name="week">Week number</param>
        /// <param name="options">Sync configuration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Sync result with detailed metrics</returns>
        Task<SyncResult> SyncPlayerStatsAsync(int season, int week, SyncOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Synchronize player statistics for a date range
        /// </summary>
        /// <param name="startDate">Start date for sync</param>
        /// <param name="endDate">End date for sync</param>
        /// <param name="options">Sync configuration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Sync result with detailed metrics</returns>
        Task<SyncResult> SyncPlayerStatsForDateRangeAsync(DateTime startDate, DateTime endDate, SyncOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Perform a full synchronization of all ESPN data
        /// </summary>
        /// <param name="season">NFL season year</param>
        /// <param name="options">Sync configuration options</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Sync result with detailed metrics</returns>
        Task<SyncResult> FullSyncAsync(int season, SyncOptions? options = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get the last synchronization report
        /// </summary>
        /// <param name="syncType">Type of sync to get report for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Last sync report</returns>
        Task<SyncReport?> GetLastSyncReportAsync(SyncType? syncType = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get sync history for analysis and monitoring
        /// </summary>
        /// <param name="limit">Maximum number of reports to return</param>
        /// <param name="syncType">Filter by sync type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of sync reports</returns>
        Task<List<SyncReport>> GetSyncHistoryAsync(int limit = 50, SyncType? syncType = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Check if a sync operation is currently running
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if sync is running</returns>
        Task<bool> IsSyncRunningAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancel any running sync operations
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if cancellation was successful</returns>
        Task<bool> CancelRunningSyncAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate ESPN API connectivity and data availability
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Validation result</returns>
        Task<SyncValidationResult> ValidateEspnConnectivityAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get sync statistics and performance metrics
        /// </summary>
        /// <param name="fromDate">Start date for metrics</param>
        /// <param name="toDate">End date for metrics</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Sync metrics</returns>
        Task<SyncMetrics> GetSyncMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Service responsible for extracting player statistics from ESPN game data
    /// </summary>
    public interface IEspnPlayerStatsService
    {
        /// <summary>
        /// Extracts player statistics from a specific game's box score data
        /// </summary>
        /// <param name="eventId">ESPN event identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of player statistics for the game</returns>
        Task<IEnumerable<PlayerStats>> ExtractGamePlayerStatsAsync(string eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts player statistics from raw ESPN box score JSON data
        /// </summary>
        /// <param name="boxScoreJson">Raw ESPN box score JSON response</param>
        /// <param name="gameInfo">Game metadata for context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of parsed player statistics</returns>
        Task<IEnumerable<PlayerStats>> ParsePlayerStatsFromJsonAsync(string boxScoreJson, GameEvent gameInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts ESPN player IDs from box score data for correlation
        /// </summary>
        /// <param name="boxScoreJson">Raw ESPN box score JSON response</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of ESPN player IDs found in the data</returns>
        Task<IEnumerable<string>> ExtractPlayerIdsAsync(string boxScoreJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Maps ESPN player data to PlayerStats model based on position
        /// </summary>
        /// <param name="espnPlayerData">Raw ESPN player data</param>
        /// <param name="position">Player position for stat categorization</param>
        /// <param name="gameContext">Game context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mapped PlayerStats object</returns>
        Task<PlayerStats> MapEspnPlayerDataAsync(dynamic espnPlayerData, PlayerPosition position, GameEvent gameContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Normalizes player names for consistent data matching
        /// </summary>
        /// <param name="playerName">Raw player name from ESPN</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Normalized player name</returns>
        Task<string> NormalizePlayerNameAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts team statistics from box score data
        /// </summary>
        /// <param name="boxScoreJson">Raw ESPN box score JSON response</param>
        /// <param name="teamId">Specific team ID to extract stats for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Team-level statistics</returns>
        Task<IEnumerable<PlayerStats>> ExtractTeamStatsAsync(string boxScoreJson, string teamId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles missing or incomplete statistical data gracefully
        /// </summary>
        /// <param name="playerData">Incomplete player data</param>
        /// <param name="position">Player position for default values</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>PlayerStats with appropriate defaults for missing data</returns>
        Task<PlayerStats> HandleMissingDataAsync(dynamic playerData, PlayerPosition position, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates extracted player statistics for data quality
        /// </summary>
        /// <param name="playerStats">Player statistics to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if statistics are valid, false otherwise</returns>
        Task<bool> ValidatePlayerStatsAsync(PlayerStats playerStats, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts player statistics from multiple games in parallel
        /// </summary>
        /// <param name="eventIds">Collection of ESPN event IDs</param>
        /// <param name="maxConcurrency">Maximum number of concurrent operations</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of player statistics from all games</returns>
        Task<IEnumerable<PlayerStats>> ExtractBulkGamePlayerStatsAsync(IEnumerable<string> eventIds, int maxConcurrency = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes large JSON responses using streaming for memory efficiency
        /// </summary>
        /// <param name="boxScoreJsonStream">Stream of box score JSON data</param>
        /// <param name="gameInfo">Game metadata for context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of player statistics</returns>
        IAsyncEnumerable<PlayerStats> StreamParsePlayerStatsAsync(Stream boxScoreJsonStream, GameEvent gameInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates multiple player statistics records in parallel
        /// </summary>
        /// <param name="playerStatsCollection">Collection of player statistics to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of player stats with their validation results</returns>
        Task<Dictionary<PlayerStats, bool>> ValidateBulkPlayerStatsAsync(IEnumerable<PlayerStats> playerStatsCollection, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// ESPN scraping service interface (combines scraping and image download)
    /// </summary>
    public interface IEspnScrapingService
    {
        Task ScrapeImagesAsync();
        Task<List<Player>> GetActivePlayersAsync();
        Task DownloadPlayerImageAsync(string playerId, string playerName);
    }

    /// <summary>
    /// Supabase database service interface for ESPN data persistence
    /// </summary>
    public interface ISupabaseDatabaseService
    {
        Task<long?> FindPlayerByEspnIdAsync(string espnId, CancellationToken cancellationToken = default);
        Task<(long? PlayerId, string? Name)> FindMatchingPlayerAsync(Models.Player espnPlayer, CancellationToken cancellationToken = default);
        Task<bool> AddPlayerAsync(Models.Player espnPlayer, CancellationToken cancellationToken = default);
        Task<bool> UpdatePlayerAsync(long playerId, Models.Player espnPlayer, CancellationToken cancellationToken = default);
        Task<long?> FindTeamIdByAbbreviationAsync(string abbreviation, CancellationToken cancellationToken = default);
        Task<long?> FindPositionIdByNameAsync(string positionName, CancellationToken cancellationToken = default);

        // Player stats operations
        Task<bool> SavePlayerStatsAsync(PlayerStatsRecord playerStats, CancellationToken cancellationToken = default);
        Task<bool> UpsertPlayerStatsAsync(PlayerStatsRecord playerStats, CancellationToken cancellationToken = default);
        Task<int> UpsertPlayerStatsBatchAsync(List<PlayerStatsRecord> playerStatsList, CancellationToken cancellationToken = default);
        Task<List<PlayerStatsRecord>> GetPlayerStatsAsync(long playerId, int? season = null, int? week = null, CancellationToken cancellationToken = default);

        // Sync operations
        Task<bool> SaveSyncReportAsync(SyncResult syncResult, CancellationToken cancellationToken = default);
        Task<SyncReport?> GetLastSyncReportAsync(SyncType? syncType = null, CancellationToken cancellationToken = default);
        Task<List<SyncReport>> GetSyncHistoryAsync(int limit = 50, SyncType? syncType = null, CancellationToken cancellationToken = default);
        Task<SyncMetrics> GetSyncMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default);
        Task UpdateSyncMetricsAsync(DateTime date, SyncResult syncResult, CancellationToken cancellationToken = default);

        // Schedule operations
        Task<bool> SaveScheduleAsync(ScheduleRecord schedule, CancellationToken cancellationToken = default);
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