using ESPNScrape.Models.DataSync;
using ESPNScrape.Models.PlayerMatching;
using ESPNScrape.Models.Supabase;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.Json;

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
        private readonly IEspnStatsTransformationService _statsTransformationService;
        private readonly IEspnPlayerStatsService _playerStatsService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ISupabaseDatabaseService _databaseService = null!;
        private readonly SyncOptions _defaultOptions;
        private readonly SemaphoreSlim _syncSemaphore;
        private CancellationTokenSource? _currentSyncCancellation;
        private string? _currentSyncId;

        public EspnDataSyncService(
            ILogger<EspnDataSyncService> logger,
            IEspnApiService espnApiService,
            IEspnPlayerMatchingService playerMatchingService,
            IEspnStatsTransformationService statsTransformationService,
            IEspnPlayerStatsService playerStatsService,
            IServiceScopeFactory scopeFactory,
            ISupabaseDatabaseService databaseService,
            IOptions<SyncOptions> defaultOptions)
        {
            _logger = logger;
            _espnApiService = espnApiService;
            _playerMatchingService = playerMatchingService;
            _statsTransformationService = statsTransformationService;
            _playerStatsService = playerStatsService;
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
                // Get all games for the week
                var games = await _espnApiService.GetGamesAsync(season, week, 2, cancellationToken);
                var gamesList = games.ToList();

                _logger.LogInformation("Found {GameCount} games for Season {Season}, Week {Week}", gamesList.Count, season, week);

                var allPlayerStats = new List<Models.Espn.PlayerStats>();

                // Process each game
                foreach (var game in gamesList)
                {
                    try
                    {
                        _logger.LogDebug("Processing game {GameId} for player stats", game.Id);

                        var gamePlayerStats = await _espnApiService.GetGamePlayerStatsAsync(game.Id, cancellationToken);
                        var gameStatsList = gamePlayerStats.ToList();

                        _logger.LogDebug("Retrieved {StatsCount} player stats from game {GameId}", gameStatsList.Count, game.Id);
                        allPlayerStats.AddRange(gameStatsList);

                        syncResult.PlayersProcessed += gameStatsList.Count;
                    }
                    catch (Exception gameEx)
                    {
                        _logger.LogWarning(gameEx, "Failed to process game {GameId} for player stats", game.Id);
                        syncResult.DataErrors++;
                        syncResult.Errors.Add($"Failed to process game {game.Id}: {gameEx.Message}");
                    }
                }

                _logger.LogInformation("Retrieved total of {TotalStats} player stats for Season {Season}, Week {Week}",
                    allPlayerStats.Count, season, week);

                // Transform and save stats to database
                _logger.LogInformation("Processing {StatsCount} player stats for database storage", allPlayerStats.Count);

                var statsToProcess = allPlayerStats.Chunk(50).ToList(); // Process in batches of 50

                foreach (var statsBatch in statsToProcess)
                {
                    try
                    {
                        // Transform ESPN stats to database format
                        var transformedStats = await _statsTransformationService.TransformPlayerStatsBatchAsync(
                            statsBatch.ToList(), cancellationToken);

                        _logger.LogDebug("Transformed {Count} stats to database format", transformedStats.Count);

                        // Process each transformed stat for database storage
                        foreach (var dbStat in transformedStats)
                        {
                            try
                            {
                                // Find the player in database by ESPN ID
                                if (string.IsNullOrEmpty(dbStat.EspnPlayerId))
                                {
                                    _logger.LogWarning("Skipping player stat with null or empty ESPN Player ID");
                                    continue;
                                }

                                var playerId = await _databaseService.FindPlayerByEspnIdAsync(dbStat.EspnPlayerId, cancellationToken);

                                // If player doesn't exist, try to create them automatically
                                if (!playerId.HasValue && !string.IsNullOrEmpty(dbStat.EspnPlayerId))
                                {
                                    _logger.LogInformation("Player not found for ESPN ID {EspnPlayerId}, attempting to create player record", dbStat.EspnPlayerId);

                                    // Create a basic player record from the stats data
                                    var newPlayer = new Models.Player
                                    {
                                        Id = dbStat.EspnPlayerId,
                                        FirstName = dbStat.Name?.Split(' ').FirstOrDefault() ?? "Unknown",
                                        LastName = dbStat.Name?.Split(' ').Skip(1).FirstOrDefault() ?? "Player",
                                        DisplayName = dbStat.Name ?? "Unknown Player",
                                        Active = true,
                                        Team = new Models.Team
                                        {
                                            // Convert full team name to abbreviation for database compatibility
                                            Abbreviation = ConvertTeamNameToAbbreviation(dbStat.Team)
                                        }
                                    };

                                    var playerAdded = await _databaseService.AddPlayerAsync(newPlayer, cancellationToken);
                                    if (playerAdded)
                                    {
                                        // Try to find the player again after adding
                                        playerId = await _databaseService.FindPlayerByEspnIdAsync(dbStat.EspnPlayerId, cancellationToken);
                                        _logger.LogInformation("Successfully created player {PlayerName} (ESPN ID: {EspnPlayerId})",
                                            dbStat.Name, dbStat.EspnPlayerId);
                                    }
                                    else
                                    {
                                        _logger.LogWarning("Failed to create player {PlayerName} (ESPN ID: {EspnPlayerId})",
                                            dbStat.Name, dbStat.EspnPlayerId);
                                    }
                                }

                                if (playerId.HasValue)
                                {
                                    // Convert DatabasePlayerStats to PlayerStatsRecord and save
                                    var statsRecord = ConvertToPlayerStatsRecord(dbStat, playerId.Value);
                                    var saved = await _databaseService.SavePlayerStatsAsync(statsRecord, cancellationToken);

                                    if (saved)
                                    {
                                        _logger.LogDebug("Saved stats for player {PlayerId} (ESPN: {EspnId})",
                                            playerId.Value, dbStat.EspnPlayerId);
                                        syncResult.StatsRecordsProcessed++;
                                    }
                                    else
                                    {
                                        syncResult.DataErrors++;
                                        syncResult.Errors.Add($"Failed to save stats for player {playerId.Value} (ESPN: {dbStat.EspnPlayerId})");
                                    }
                                }
                                else
                                {
                                    syncResult.DataErrors++;
                                    syncResult.Errors.Add($"Player not found for ESPN ID: {dbStat.EspnPlayerId}");
                                }
                            }
                            catch (Exception statEx)
                            {
                                syncResult.DataErrors++;
                                syncResult.Errors.Add($"Failed to save stats for player {dbStat.EspnPlayerId}: {statEx.Message}");
                                _logger.LogWarning(statEx, "Failed to save stats for player {EspnPlayerId}", dbStat.EspnPlayerId);
                            }
                        }
                    }
                    catch (Exception batchEx)
                    {
                        syncResult.DataErrors++;
                        syncResult.Errors.Add($"Failed to process stats batch: {batchEx.Message}");
                        _logger.LogError(batchEx, "Failed to process stats batch");
                    }
                }

                syncResult.Status = SyncStatus.Completed;
                syncResult.EndTime = DateTime.UtcNow;

                _logger.LogInformation("Player stats sync completed for Season {Season}, Week {Week}. SyncId: {SyncId}",
                    season, week, syncResult.SyncId);

                // Save sync report to database
                await _databaseService.SaveSyncReportAsync(syncResult, cancellationToken);

                return syncResult;
            }
            catch (Exception ex)
            {
                syncResult.Status = SyncStatus.Failed;
                syncResult.EndTime = DateTime.UtcNow;
                syncResult.Errors.Add($"Unexpected error: {ex.Message}");
                _logger.LogError(ex, "Unexpected error during player stats sync. SyncId: {SyncId}", syncResult.SyncId);

                // Save failed sync report to database
                await _databaseService.SaveSyncReportAsync(syncResult, cancellationToken);

                return syncResult;
            }
            finally
            {
                // TODO: Fix semaphore - no matching WaitAsync call for this Release
                // _syncSemaphore.Release();
                _currentSyncId = null;
                _currentSyncCancellation?.Dispose();
                _currentSyncCancellation = null;
            }
        }

        /// <summary>
        /// Synchronize player statistics for a date range
        /// </summary>
        public async Task<SyncResult> SyncPlayerStatsForDateRangeAsync(DateTime startDate, DateTime endDate, SyncOptions? options = null, CancellationToken cancellationToken = default)
        {
            options ??= _defaultOptions;
            var syncResult = new SyncResult
            {
                SyncType = SyncType.PlayerStats,
                Status = SyncStatus.Running,
                Options = options
            };

            _logger.LogInformation("Starting player stats synchronization for date range {StartDate} to {EndDate}. SyncId: {SyncId}",
                startDate, endDate, syncResult.SyncId);

            try
            {
                var totalDays = (endDate - startDate).Days + 1;
                _logger.LogInformation("Processing {TotalDays} days of games for date range sync", totalDays);

                var allPlayerStats = new List<Models.Espn.PlayerStats>();

                // Process each day in the date range
                for (var currentDate = startDate.Date; currentDate <= endDate.Date; currentDate = currentDate.AddDays(1))
                {
                    try
                    {
                        _logger.LogDebug("Processing games for date {Date}", currentDate);

                        var gamesForDate = await _espnApiService.GetGamesForDateAsync(currentDate, cancellationToken);
                        var gamesList = gamesForDate.ToList();

                        _logger.LogDebug("Found {GameCount} games for date {Date}", gamesList.Count, currentDate);

                        // Process each game for the date
                        foreach (var game in gamesList)
                        {
                            try
                            {
                                var gamePlayerStats = await _espnApiService.GetGamePlayerStatsAsync(game.Id, cancellationToken);
                                var gameStatsList = gamePlayerStats.ToList();

                                allPlayerStats.AddRange(gameStatsList);
                                syncResult.PlayersProcessed += gameStatsList.Count;

                                _logger.LogDebug("Processed {StatsCount} player stats from game {GameId} on {Date}",
                                    gameStatsList.Count, game.Id, currentDate);
                            }
                            catch (Exception gameEx)
                            {
                                _logger.LogWarning(gameEx, "Failed to process game {GameId} on {Date}", game.Id, currentDate);
                                syncResult.DataErrors++;
                                syncResult.Errors.Add($"Failed to process game {game.Id} on {currentDate:yyyy-MM-dd}: {gameEx.Message}");
                            }
                        }
                    }
                    catch (Exception dateEx)
                    {
                        _logger.LogWarning(dateEx, "Failed to process games for date {Date}", currentDate);
                        syncResult.DataErrors++;
                        syncResult.Errors.Add($"Failed to process date {currentDate:yyyy-MM-dd}: {dateEx.Message}");
                    }
                }

                _logger.LogInformation("Retrieved total of {TotalStats} player stats for date range {StartDate} to {EndDate}",
                    allPlayerStats.Count, startDate, endDate);

                // Transform and save stats to database (placeholder for now)
                // TODO: Implement transformation service and database storage
                foreach (var stat in allPlayerStats)
                {
                    try
                    {
                        // This would involve:
                        // 1. Transform ESPN stats to database format
                        // 2. Find or create player records
                        // 3. Insert/update player stats
                        _logger.LogDebug("Would process stats for player {PlayerId}", stat.PlayerId);
                        syncResult.StatsRecordsProcessed++;
                    }
                    catch (Exception statEx)
                    {
                        _logger.LogWarning(statEx, "Failed to process stats for player {PlayerId}", stat.PlayerId);
                        syncResult.DataErrors++;
                    }
                }

                syncResult.Status = SyncStatus.Completed;
                syncResult.EndTime = DateTime.UtcNow;

                _logger.LogInformation("Date range stats sync completed for {StartDate} to {EndDate}. SyncId: {SyncId}",
                    startDate, endDate, syncResult.SyncId);

                return syncResult;
            }
            catch (Exception ex)
            {
                syncResult.Status = SyncStatus.Failed;
                syncResult.EndTime = DateTime.UtcNow;
                syncResult.Errors.Add($"Unexpected error: {ex.Message}");
                _logger.LogError(ex, "Unexpected error during date range stats sync. SyncId: {SyncId}", syncResult.SyncId);
                return syncResult;
            }
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
                _logger.LogInformation("Starting stats sync for all weeks of season {Season}", season);

                try
                {
                    // Get all weeks for the season
                    var weeks = await _espnApiService.GetWeeksAsync(season, 2, cancellationToken); // Regular season
                    var weeksList = weeks.ToList();

                    _logger.LogInformation("Found {WeekCount} weeks for season {Season}", weeksList.Count, season);

                    var totalStatsProcessed = 0;
                    var totalStatsErrors = 0;

                    foreach (var week in weeksList)
                    {
                        try
                        {
                            _logger.LogDebug("Syncing stats for Season {Season}, Week {Week}", season, week.WeekNumber);

                            var weekStatsResult = await SyncPlayerStatsAsync(season, week.WeekNumber, options, cancellationToken);

                            totalStatsProcessed += weekStatsResult.StatsRecordsProcessed;
                            totalStatsErrors += weekStatsResult.DataErrors;

                            if (weekStatsResult.Status == SyncStatus.Failed)
                            {
                                syncResult.Warnings.Add($"Failed to sync stats for Week {week.WeekNumber}");
                                syncResult.Errors.AddRange(weekStatsResult.Errors);
                            }
                            else
                            {
                                _logger.LogDebug("Successfully synced stats for Season {Season}, Week {Week}: {StatsCount} records",
                                    season, week.WeekNumber, weekStatsResult.StatsRecordsProcessed);
                            }
                        }
                        catch (Exception weekEx)
                        {
                            _logger.LogWarning(weekEx, "Failed to sync stats for Season {Season}, Week {Week}", season, week.WeekNumber);
                            syncResult.Warnings.Add($"Failed to sync stats for Week {week.WeekNumber}: {weekEx.Message}");
                            totalStatsErrors++;
                        }
                    }

                    syncResult.StatsRecordsProcessed = totalStatsProcessed;
                    syncResult.DataErrors += totalStatsErrors;

                    _logger.LogInformation("Completed stats sync for season {Season}: {TotalStats} stats processed, {TotalErrors} errors",
                        season, totalStatsProcessed, totalStatsErrors);
                }
                catch (Exception statsEx)
                {
                    _logger.LogError(statsEx, "Failed to sync stats for season {Season}", season);
                    syncResult.Errors.Add($"Stats sync failed for season {season}: {statsEx.Message}");
                }

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
            try
            {
                _logger.LogInformation("Getting last sync report for type: {SyncType}", syncType);
                return await _databaseService.GetLastSyncReportAsync(syncType, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting last sync report for type: {SyncType}", syncType);
                return null;
            }
        }

        /// <summary>
        /// Get sync history for analysis and monitoring
        /// </summary>
        public async Task<List<SyncReport>> GetSyncHistoryAsync(int limit = 50, SyncType? syncType = null, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Getting sync history. Limit: {Limit}, Type: {SyncType}", limit, syncType);
                return await _databaseService.GetSyncHistoryAsync(limit, syncType, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sync history. Limit: {Limit}, Type: {SyncType}", limit, syncType);
                return new List<SyncReport>();
            }
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
            var validation = new SyncValidationResult();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Test ESPN API connectivity
                _logger.LogDebug("Validating ESPN API connectivity");

                // Test ESPN API with a simple call
                try
                {
                    var currentWeek = await _espnApiService.GetCurrentWeekAsync(cancellationToken);
                    validation.IsEspnApiAccessible = currentWeek != null;
                    validation.EspnApiResponseTime = stopwatch.ElapsedMilliseconds;
                    validation.AvailableEndpoints.Add("/nfl/week");

                    if (validation.IsEspnApiAccessible)
                    {
                        validation.AvailableEndpoints.Add("/nfl/players");
                        validation.AvailableEndpoints.Add("/nfl/scoreboard");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ESPN API validation failed");
                    validation.IsEspnApiAccessible = false;
                    validation.ValidationErrors.Add($"ESPN API not accessible: {ex.Message}");
                }

                // Test database connectivity
                _logger.LogDebug("Validating database connectivity");
                var dbStopwatch = Stopwatch.StartNew();

                try
                {
                    // Try a simple database operation to test connectivity
                    await _databaseService.FindTeamIdByAbbreviationAsync("KC", cancellationToken);
                    validation.IsDatabaseAccessible = true;
                    validation.DatabaseResponseTime = dbStopwatch.ElapsedMilliseconds;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Database validation failed");
                    validation.IsDatabaseAccessible = false;
                    validation.ValidationErrors.Add($"Database not accessible: {ex.Message}");
                }

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
            try
            {
                _logger.LogInformation("Getting sync metrics from {FromDate} to {ToDate}", fromDate, toDate);
                return await _databaseService.GetSyncMetricsAsync(fromDate, toDate, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sync metrics from {FromDate} to {ToDate}", fromDate, toDate);
                return new SyncMetrics
                {
                    FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
                    ToDate = toDate ?? DateTime.UtcNow
                };
            }
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
        /// Convert DatabasePlayerStats to PlayerStatsRecord for Supabase storage (existing table structure)
        /// </summary>
        private static PlayerStatsRecord ConvertToPlayerStatsRecord(DatabasePlayerStats dbStats, long playerId)
        {
            var statsRecord = new PlayerStatsRecord
            {
                PlayerId = playerId,
                EspnPlayerId = dbStats.EspnPlayerId,
                EspnGameId = dbStats.EspnGameId,
                Season = dbStats.Season,
                Week = dbStats.Week,
                Name = dbStats.Name,
                PlayerCode = dbStats.PlayerCode,
                Team = dbStats.Team,
                GameDate = dbStats.GameDate,
                GameLocation = dbStats.GameLocation,

                // Store stats as JSONB objects (matching existing table structure)
                Passing = dbStats.Passing,
                Rushing = dbStats.Rushing,
                Receiving = dbStats.Receiving
            };

            return statsRecord;
        }

        /// <summary>
        /// Maps full team names to database abbreviations
        /// </summary>
        private static string ConvertTeamNameToAbbreviation(string fullTeamName)
        {
            if (string.IsNullOrWhiteSpace(fullTeamName))
                return "FA"; // Default to Free Agent

            return fullTeamName.ToUpper().Trim() switch
            {
                "KANSAS CITY CHIEFS" => "KAN",
                "NEW ENGLAND PATRIOTS" => "NWE",
                "NEW ORLEANS SAINTS" => "NOR",
                "SAN FRANCISCO 49ERS" => "SFO",
                "TAMPA BAY BUCCANEERS" => "TAM",
                "LAS VEGAS RAIDERS" => "LVR",
                "GREEN BAY PACKERS" => "GNB",
                "WASHINGTON COMMANDERS" => "WAS",
                "ARIZONA CARDINALS" => "ARI",
                "ATLANTA FALCONS" => "ATL",
                "BALTIMORE RAVENS" => "BAL",
                "BUFFALO BILLS" => "BUF",
                "CAROLINA PANTHERS" => "CAR",
                "CHICAGO BEARS" => "CHI",
                "CINCINNATI BENGALS" => "CIN",
                "CLEVELAND BROWNS" => "CLE",
                "DALLAS COWBOYS" => "DAL",
                "DENVER BRONCOS" => "DEN",
                "DETROIT LIONS" => "DET",
                "HOUSTON TEXANS" => "HOU",
                "INDIANAPOLIS COLTS" => "IND",
                "JACKSONVILLE JAGUARS" => "JAX",
                "LOS ANGELES CHARGERS" => "LAC",
                "LOS ANGELES RAMS" => "LAR",
                "MIAMI DOLPHINS" => "MIA",
                "MINNESOTA VIKINGS" => "MIN",
                "NEW YORK GIANTS" => "NYG",
                "NEW YORK JETS" => "NYJ",
                "PHILADELPHIA EAGLES" => "PHI",
                "PITTSBURGH STEELERS" => "PIT",
                "SEATTLE SEAHAWKS" => "SEA",
                "TENNESSEE TITANS" => "TEN",
                _ => "FA"  // Default to Free Agent for unknown teams
            };
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