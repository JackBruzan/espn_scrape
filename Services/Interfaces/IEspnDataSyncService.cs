using ESPNScrape.Models.DataSync;

namespace ESPNScrape.Services.Interfaces
{
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
}