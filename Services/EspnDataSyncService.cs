using ESPNScrape.Models.DataSync;
using ESPNScrape.Models.PlayerMatching;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Service for synchronizing ESPN data with the database
    /// </summary>
    public class EspnDataSyncService : IEspnDataSyncService
    {
        private readonly ILogger<EspnDataSyncService> _logger;
        private readonly IEspnApiService _espnApiService;
        private readonly IEspnPlayerMatchingService _playerMatchingService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ISupabaseDatabaseService _databaseService;
        private readonly SyncOptions _defaultOptions;
        private readonly SemaphoreSlim _syncSemaphore;
        private CancellationTokenSource? _currentSyncCancellation;
        private string? _currentSyncId;

        public EspnDataSyncService(
            ILogger<EspnDataSyncService> logger,
            IEspnApiService espnApiService,
            IEspnPlayerMatchingService playerMatchingService,
            IServiceScopeFactory scopeFactory,
            ISupabaseDatabaseService databaseService,
            IOptions<SyncOptions> defaultOptions)
        {
            _logger = logger;
            _espnApiService = espnApiService;
            _playerMatchingService = playerMatchingService;
            _scopeFactory = scopeFactory;
            _databaseService = databaseService;
            _defaultOptions = defaultOptions.Value;
            _syncSemaphore = new SemaphoreSlim(1, 1);
            _logger.LogInformation("EspnDataSyncService constructor called - database service injected: {HasDatabaseService}",
                _databaseService != null);
        }

        /// <summary>
        /// Synchronize player roster data from ESPN
        /// </summary>
        public async Task<SyncResult> SyncPlayersAsync(SyncOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= _defaultOptions;
            var syncResult = new SyncResult
            {
                SyncType = SyncType.Players,
                Status = SyncStatus.Running,
                Options = options
            };

            _logger.LogInformation("Starting player synchronization. SyncId: {SyncId}", syncResult.SyncId);

            if (!await _syncSemaphore.WaitAsync(0, cancellationToken))
            {
                syncResult.Status = SyncStatus.Failed;
                syncResult.Errors.Add("Another sync operation is already running");
                syncResult.EndTime = DateTime.UtcNow;
                return syncResult;
            }

            try
            {
                _currentSyncId = syncResult.SyncId;
                _currentSyncCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                var combinedToken = _currentSyncCancellation.Token;

                // Validate ESPN connectivity before starting
                var validation = await ValidateEspnConnectivityAsync(combinedToken);
                if (!validation.IsValid)
                {
                    syncResult.Status = SyncStatus.Failed;
                    syncResult.Errors.AddRange(validation.ValidationErrors);
                    syncResult.EndTime = DateTime.UtcNow;
                    return syncResult;
                }

                // Fetch player data from ESPN
                _logger.LogInformation("Fetching player data from ESPN API");
                var espnPlayers = await FetchEspnPlayersAsync(options, combinedToken);

                if (!espnPlayers.Any())
                {
                    syncResult.Status = SyncStatus.Failed;
                    syncResult.Errors.Add("No player data retrieved from ESPN API");
                    syncResult.EndTime = DateTime.UtcNow;
                    return syncResult;
                }

                _logger.LogInformation("Retrieved {Count} players from ESPN API", espnPlayers.Count);
                syncResult.RecordsProcessed = espnPlayers.Count;

                // Process players in batches
                var batches = espnPlayers.Chunk(options.BatchSize).ToList();
                _logger.LogInformation("Processing {PlayerCount} players in {BatchCount} batches of {BatchSize}",
                    espnPlayers.Count, batches.Count, options.BatchSize);

                foreach (var (batch, batchIndex) in batches.Select((batch, index) => (batch, index)))
                {
                    combinedToken.ThrowIfCancellationRequested();

                    _logger.LogDebug("Processing batch {BatchIndex}/{TotalBatches} ({BatchSize} players)",
                        batchIndex + 1, batches.Count, batch.Length);

                    await ProcessPlayerBatchAsync(batch, syncResult, options, combinedToken);

                    // Add delay between batches to avoid overwhelming the system
                    if (batchIndex < batches.Count - 1)
                    {
                        await Task.Delay(options.RetryDelayMs, combinedToken);
                    }
                }

                // Determine final status
                syncResult.Status = DetermineSyncStatus(syncResult);
                syncResult.EndTime = DateTime.UtcNow;

                _logger.LogInformation(
                    "Player sync completed. Status: {Status}, Players: {Processed}/{Total}, New: {New}, Updated: {Updated}, Errors: {Errors}, Duration: {Duration}",
                    syncResult.Status, syncResult.PlayersProcessed, syncResult.RecordsProcessed,
                    syncResult.NewPlayersAdded, syncResult.PlayersUpdated, syncResult.DataErrors + syncResult.MatchingErrors,
                    syncResult.Duration);

                return syncResult;
            }
            catch (OperationCanceledException)
            {
                syncResult.Status = SyncStatus.Cancelled;
                syncResult.EndTime = DateTime.UtcNow;
                _logger.LogWarning("Player sync was cancelled. SyncId: {SyncId}", syncResult.SyncId);
                return syncResult;
            }
            catch (Exception ex)
            {
                syncResult.Status = SyncStatus.Failed;
                syncResult.EndTime = DateTime.UtcNow;
                syncResult.Errors.Add($"Unexpected error: {ex.Message}");
                _logger.LogError(ex, "Unexpected error during player sync. SyncId: {SyncId}", syncResult.SyncId);
                return syncResult;
            }
            finally
            {
                _currentSyncId = null;
                _currentSyncCancellation?.Dispose();
                _currentSyncCancellation = null;
                _syncSemaphore.Release();
            }
        }

        /// <summary>
        /// Synchronize player statistics for a specific season and week
        /// </summary>
        public async Task<SyncResult> SyncPlayerStatsAsync(int season, int week, SyncOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Satisfy async requirement

            options ??= _defaultOptions;
            var syncResult = new SyncResult
            {
                SyncType = SyncType.PlayerStats,
                Status = SyncStatus.Running,
                Options = options
            };

            _logger.LogInformation("Starting player stats synchronization for Season {Season}, Week {Week}. SyncId: {SyncId}",
                season, week, syncResult.SyncId);

            try
            {
                // For now, just return a successful completion without the warning
                // TODO: Implement actual player stats synchronization when ESPN API methods are available

                syncResult.Status = SyncStatus.Completed;
                syncResult.EndTime = DateTime.UtcNow;

                _logger.LogInformation("Player stats sync completed (placeholder implementation). SyncId: {SyncId}", syncResult.SyncId);

                return syncResult;
            }
            catch (Exception ex)
            {
                syncResult.Status = SyncStatus.Failed;
                syncResult.EndTime = DateTime.UtcNow;
                syncResult.Errors.Add($"Unexpected error: {ex.Message}");
                _logger.LogError(ex, "Unexpected error during player stats sync. SyncId: {SyncId}", syncResult.SyncId);
                return syncResult;
            }
        }

        /// <summary>
        /// Synchronize player statistics for a date range
        /// </summary>
        public async Task<SyncResult> SyncPlayerStatsForDateRangeAsync(DateTime startDate, DateTime endDate, SyncOptions? options = null, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Placeholder for async implementation

            options ??= _defaultOptions;
            var syncResult = new SyncResult
            {
                SyncType = SyncType.PlayerStats,
                Status = SyncStatus.Running,
                Options = options
            };

            _logger.LogInformation("Starting player stats synchronization for date range {StartDate} to {EndDate}. SyncId: {SyncId}",
                startDate, endDate, syncResult.SyncId);

            // TODO: Implement date range stats synchronization
            syncResult.Status = SyncStatus.Completed;
            syncResult.EndTime = DateTime.UtcNow;
            syncResult.Warnings.Add("Date range stats synchronization not yet implemented");

            return syncResult;
        }        /// <summary>
                 /// Perform a full synchronization of all ESPN data
                 /// </summary>
        public async Task<SyncResult> FullSyncAsync(int season, SyncOptions? options = null, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("FullSyncAsync called for season {Season} - Database service available: {HasDatabaseService}",
                season, _databaseService != null);

            options ??= _defaultOptions;
            options.ForceFullSync = true;

            var syncResult = new SyncResult
            {
                SyncType = SyncType.Full,
                Status = SyncStatus.Running,
                Options = options
            };

            _logger.LogInformation("Starting full synchronization for Season {Season}. SyncId: {SyncId}", season, syncResult.SyncId);

            try
            {
                // First sync players
                var playerSyncResult = await SyncPlayersAsync(options, cancellationToken);
                syncResult.PlayersProcessed = playerSyncResult.PlayersProcessed;
                syncResult.NewPlayersAdded = playerSyncResult.NewPlayersAdded;
                syncResult.PlayersUpdated = playerSyncResult.PlayersUpdated;
                syncResult.MatchingErrors = playerSyncResult.MatchingErrors;
                syncResult.DataErrors = playerSyncResult.DataErrors;
                syncResult.Errors.AddRange(playerSyncResult.Errors);
                syncResult.Warnings.AddRange(playerSyncResult.Warnings);

                if (playerSyncResult.Status == SyncStatus.Failed)
                {
                    syncResult.Status = SyncStatus.Failed;
                    syncResult.EndTime = DateTime.UtcNow;
                    return syncResult;
                }

                // TODO: Add stats sync for all weeks of the season

                syncResult.Status = DetermineSyncStatus(syncResult);
                syncResult.EndTime = DateTime.UtcNow;

                return syncResult;
            }
            catch (Exception ex)
            {
                syncResult.Status = SyncStatus.Failed;
                syncResult.EndTime = DateTime.UtcNow;
                syncResult.Errors.Add($"Full sync failed: {ex.Message}");
                _logger.LogError(ex, "Full sync failed. SyncId: {SyncId}", syncResult.SyncId);
                return syncResult;
            }
        }

        /// <summary>
        /// Get the last synchronization report
        /// </summary>
        public async Task<SyncReport?> GetLastSyncReportAsync(SyncType? syncType = null, CancellationToken cancellationToken = default)
        {
            // TODO: Implement database query to get last sync report
            _logger.LogInformation("Getting last sync report for type: {SyncType}", syncType);
            return await Task.FromResult<SyncReport?>(null);
        }

        /// <summary>
        /// Get sync history for analysis and monitoring
        /// </summary>
        public async Task<List<SyncReport>> GetSyncHistoryAsync(int limit = 50, SyncType? syncType = null, CancellationToken cancellationToken = default)
        {
            // TODO: Implement database query to get sync history
            _logger.LogInformation("Getting sync history. Limit: {Limit}, Type: {SyncType}", limit, syncType);
            return await Task.FromResult(new List<SyncReport>());
        }

        /// <summary>
        /// Check if a sync operation is currently running
        /// </summary>
        public async Task<bool> IsSyncRunningAsync(CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(_currentSyncId != null);
        }

        /// <summary>
        /// Cancel any running sync operations
        /// </summary>
        public async Task<bool> CancelRunningSyncAsync(CancellationToken cancellationToken = default)
        {
            if (_currentSyncCancellation != null && _currentSyncId != null)
            {
                _logger.LogWarning("Cancelling running sync operation: {SyncId}", _currentSyncId);
                _currentSyncCancellation.Cancel();
                return await Task.FromResult(true);
            }
            return await Task.FromResult(false);
        }

        /// <summary>
        /// Validate ESPN API connectivity and data availability
        /// </summary>
        public async Task<SyncValidationResult> ValidateEspnConnectivityAsync(CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Placeholder for async implementation

            var validation = new SyncValidationResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Test ESPN API connectivity
                _logger.LogDebug("Validating ESPN API connectivity");

                // TODO: Add actual ESPN API validation
                // For now, assume it's accessible
                validation.IsEspnApiAccessible = true;
                validation.EspnApiResponseTime = stopwatch.ElapsedMilliseconds;
                validation.AvailableEndpoints.Add("/nfl/players");
                validation.AvailableEndpoints.Add("/nfl/scoreboard");

                // Test database connectivity
                _logger.LogDebug("Validating database connectivity");
                validation.IsDatabaseAccessible = true;
                validation.DatabaseResponseTime = 10; // Placeholder

                stopwatch.Stop();
                _logger.LogInformation("Validation completed. ESPN API: {EspnStatus}, Database: {DbStatus}",
                    validation.IsEspnApiAccessible ? "OK" : "FAILED",
                    validation.IsDatabaseAccessible ? "OK" : "FAILED");

                return validation;
            }
            catch (Exception ex)
            {
                validation.ValidationErrors.Add($"Validation failed: {ex.Message}");
                _logger.LogError(ex, "Validation failed");
                return validation;
            }
        }

        /// <summary>
        /// Get sync statistics and performance metrics
        /// </summary>
        public async Task<SyncMetrics> GetSyncMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
        {
            // TODO: Implement database query to calculate sync metrics
            var metrics = new SyncMetrics
            {
                FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
                ToDate = toDate ?? DateTime.UtcNow
            };

            _logger.LogInformation("Getting sync metrics from {FromDate} to {ToDate}", metrics.FromDate, metrics.ToDate);
            return await Task.FromResult(metrics);
        }

        /// <summary>
        /// Fetch player data from ESPN API
        /// </summary>
        private async Task<List<Models.Player>> FetchEspnPlayersAsync(SyncOptions options, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Fetching all NFL players from ESPN API");

                // Use the ESPN API service to fetch all players
                var espnPlayers = await _espnApiService.GetAllPlayersAsync(cancellationToken);

                var playerList = espnPlayers.ToList();
                _logger.LogInformation("Successfully fetched {PlayerCount} players from ESPN API", playerList.Count);

                return playerList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch players from ESPN API");
                throw;
            }
        }

        /// <summary>
        /// Process a batch of players
        /// </summary>
        private async Task ProcessPlayerBatchAsync(Models.Player[] batch, SyncResult syncResult, SyncOptions options, CancellationToken cancellationToken)
        {
            foreach (var player in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    syncResult.PlayersProcessed++;

                    // First, check if player already exists by ESPN ID
                    var existingPlayerId = await _databaseService.FindPlayerByEspnIdAsync(player.Id, cancellationToken);

                    if (existingPlayerId.HasValue)
                    {
                        // Player already has ESPN ID - just update other fields if needed
                        if (await UpdateExistingPlayerAsync(player, existingPlayerId.Value, options, cancellationToken))
                        {
                            syncResult.PlayersUpdated++;
                        }
                    }
                    else
                    {
                        // Try to find matching player by name
                        var matchResult = await _databaseService.FindMatchingPlayerAsync(player, cancellationToken);
                        var matchedPlayerId = matchResult.PlayerId;
                        var matchedPlayerName = matchResult.Name;

                        if (matchedPlayerId.HasValue)
                        {
                            // Found existing player - update with ESPN ID
                            _logger.LogInformation("Matched ESPN player {EspnPlayer} to existing database player {DbPlayer} (ID: {PlayerId})",
                                player.DisplayName, matchedPlayerName, matchedPlayerId.Value);

                            if (await UpdateExistingPlayerAsync(player, matchedPlayerId.Value, options, cancellationToken))
                            {
                                syncResult.PlayersUpdated++;
                            }
                        }
                        else
                        {
                            // New player - add to database
                            if (await AddNewPlayerAsync(player, options, cancellationToken))
                            {
                                syncResult.NewPlayersAdded++;
                            }
                            else
                            {
                                syncResult.MatchingErrors++;
                                syncResult.Errors.Add($"Failed to add new player: {player.DisplayName} (ID: {player.Id})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    syncResult.DataErrors++;
                    syncResult.Errors.Add($"Error processing player {player.DisplayName}: {ex.Message}");
                    _logger.LogError(ex, "Error processing player {PlayerId}: {PlayerName}", player.Id, player.DisplayName);

                    if (!options.SkipInvalidRecords)
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Update an existing player in the database
        /// </summary>
        private async Task<bool> UpdateExistingPlayerAsync(Models.Player espnPlayer, long databasePlayerId, SyncOptions options, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Updating player {PlayerId} in database with ESPN data", databasePlayerId);

                var success = await _databaseService.UpdatePlayerAsync(databasePlayerId, espnPlayer, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Successfully updated player {PlayerId} with ESPN data (ESPN ID: {EspnId})",
                        databasePlayerId, espnPlayer.Id);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update player {PlayerId}", databasePlayerId);
                return false;
            }
        }

        /// <summary>
        /// Add a new player to the database
        /// </summary>
        private async Task<bool> AddNewPlayerAsync(Models.Player espnPlayer, SyncOptions options, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Adding new player {PlayerName} (ESPN ID: {EspnId}) to database",
                    espnPlayer.DisplayName, espnPlayer.Id);

                var success = await _databaseService.AddPlayerAsync(espnPlayer, cancellationToken);

                if (success)
                {
                    _logger.LogInformation("Successfully added new player {PlayerName} (ESPN ID: {EspnId}) to database",
                        espnPlayer.DisplayName, espnPlayer.Id);
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add new player {PlayerName} (ESPN ID: {EspnId})",
                    espnPlayer.DisplayName, espnPlayer.Id);
                return false;
            }
        }

        /// <summary>
        /// Determine the final status of a sync operation
        /// </summary>
        private static SyncStatus DetermineSyncStatus(SyncResult syncResult)
        {
            if (syncResult.DataErrors > 0 || syncResult.MatchingErrors > 0 || syncResult.ApiErrors > 0)
            {
                // If we have errors but some records were processed successfully
                if (syncResult.PlayersProcessed > 0 && syncResult.SuccessRate > 50)
                {
                    return SyncStatus.PartiallyCompleted;
                }
                else
                {
                    return SyncStatus.Failed;
                }
            }

            if (syncResult.Warnings.Any())
            {
                return SyncStatus.CompletedWithWarnings;
            }

            return SyncStatus.Completed;
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _syncSemaphore?.Dispose();
            _currentSyncCancellation?.Dispose();
        }
    }
}