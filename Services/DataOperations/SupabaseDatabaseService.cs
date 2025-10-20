using ESPNScrape.Models.Supabase;
using ESPNScrape.Models.DataSync;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Options;
using Supabase;
using System.Text.Json;

namespace ESPNScrape.Services.DataOperations;

public class SupabaseConfiguration
{
    public string Url { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
}

public class SupabaseDatabaseService : ISupabaseDatabaseService
{
    private readonly ILogger<SupabaseDatabaseService> _logger;
    private readonly Supabase.Client _supabaseClient;
    private readonly SupabaseConfiguration _config;

    public SupabaseDatabaseService(
        ILogger<SupabaseDatabaseService> logger,
        IConfiguration configuration)
    {
        _logger = logger;
        _config = configuration.GetSection("Supabase").Get<SupabaseConfiguration>()
                  ?? throw new InvalidOperationException("Supabase configuration is missing");

        var options = new SupabaseOptions
        {
            AutoConnectRealtime = false,
            AutoRefreshToken = false
        };

        _supabaseClient = new Supabase.Client(_config.Url, _config.ServiceRoleKey, options);
        _logger.LogInformation("SupabaseDatabaseService initialized with URL: {Url}", _config.Url);
    }

    public async Task<long?> FindPlayerByEspnIdAsync(string espnId, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _supabaseClient
                .From<PlayerRecord>()
                .Where(p => p.espn_player_id == espnId)
                .Single();

            if (response != null)
            {
                return response.id;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding player by ESPN ID {EspnId}", espnId);
            return null;
        }
    }

    public async Task<(long? PlayerId, string? Name)> FindMatchingPlayerAsync(Models.Player espnPlayer, CancellationToken cancellationToken = default)
    {
        try
        {
            // First try exact name match
            var exactMatch = await _supabaseClient
                .From<PlayerRecord>()
                .Where(p => p.first_name == espnPlayer.FirstName && p.last_name == espnPlayer.LastName)
                .Single();

            if (exactMatch != null)
            {
                var fullName = $"{exactMatch.first_name} {exactMatch.last_name}";
                return (exactMatch.id, fullName);
            }

            // Try fuzzy matching with case-insensitive search
            var players = await _supabaseClient
                .From<PlayerRecord>()
                .Where(p => p.active == true)
                .Get();

            if (players?.Models != null)
            {
                var fuzzyMatch = players.Models.FirstOrDefault(p =>
                    IsNameSimilar(p.first_name, espnPlayer.FirstName) &&
                    IsNameSimilar(p.last_name, espnPlayer.LastName));

                if (fuzzyMatch != null)
                {
                    var fullName = $"{fuzzyMatch.first_name} {fuzzyMatch.last_name}";
                    return (fuzzyMatch.id, fullName);
                }
            }

            _logger.LogWarning("No matching player found for ESPN player {FirstName} {LastName} (ESPN ID: {EspnId}). " +
                "Checked {PlayerCount} active players in database for both exact and fuzzy matches.",
                espnPlayer.FirstName, espnPlayer.LastName, espnPlayer.Id, players?.Models?.Count ?? 0);
            return (null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matching player for {FirstName} {LastName}", espnPlayer.FirstName, espnPlayer.LastName);
            return (null, null);
        }
    }

    public async Task<bool> AddPlayerAsync(Models.Player espnPlayer, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Adding new player: {FirstName} {LastName} (ESPN ID: {EspnId})",
                espnPlayer.FirstName, espnPlayer.LastName, espnPlayer.Id);

            // Double-check if player exists before inserting (prevents race condition)
            var existingPlayerId = await FindPlayerByEspnIdAsync(espnPlayer.Id, cancellationToken);
            if (existingPlayerId.HasValue)
            {
                _logger.LogInformation("Player {FirstName} {LastName} (ESPN ID: {EspnId}) already exists with ID {PlayerId}",
                    espnPlayer.FirstName, espnPlayer.LastName, espnPlayer.Id, existingPlayerId.Value);
                return true; // Player already exists, consider it a success
            }

            // Skip team assignments - teams will be managed elsewhere
            _logger.LogDebug("Skipping team assignment for new player {PlayerName} - teams managed externally", espnPlayer.DisplayName);

            var positionId = espnPlayer.Position != null ? await FindPositionIdByNameAsync(espnPlayer.Position.Abbreviation, cancellationToken) : null;

            _logger.LogDebug("Looked up IDs for {PlayerName}: PositionId={PositionId} (TeamId skipped - managed externally)",
                espnPlayer.DisplayName, positionId?.ToString() ?? "NULL");

            var playerRecord = new PlayerRecord
            {
                first_name = espnPlayer.FirstName,
                last_name = espnPlayer.LastName,
                espn_player_id = espnPlayer.Id,
                // team_id excluded - teams managed externally
                position_id = positionId,
                active = espnPlayer.Active,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };

            var response = await _supabaseClient
                .From<PlayerRecord>()
                .Insert(playerRecord);

            if (response?.Models?.Count > 0)
            {
                var addedPlayer = response.Models.First();
                _logger.LogInformation("Successfully added player: {FirstName} {LastName} with ID {PlayerId}",
                    espnPlayer.FirstName, espnPlayer.LastName, addedPlayer.id);
                return true;
            }

            _logger.LogWarning("Failed to add player: {FirstName} {LastName}", espnPlayer.FirstName, espnPlayer.LastName);
            return false;
        }
        catch (Exception ex)
        {
            // Check if this is a unique constraint violation (duplicate ESPN ID)
            if (ex.Message.Contains("23505") || ex.Message.Contains("duplicate key") || ex.Message.Contains("already exists"))
            {
                _logger.LogWarning("Player {FirstName} {LastName} (ESPN ID: {EspnId}) already exists (unique constraint violation). This is expected during concurrent operations.",
                    espnPlayer.FirstName, espnPlayer.LastName, espnPlayer.Id);
                return true; // Treat as success - player exists
            }

            _logger.LogError(ex, "Error adding player {FirstName} {LastName} (ESPN ID: {EspnId})",
                espnPlayer.FirstName, espnPlayer.LastName, espnPlayer.Id);
            return false;
        }
    }

    public async Task<bool> UpdatePlayerAsync(long playerId, Models.Player espnPlayer, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Updating player {PlayerId} with ESPN data", playerId);

            // Skip team updates - teams will be managed elsewhere
            _logger.LogDebug("Skipping team update for player {PlayerName} - teams managed externally", espnPlayer.DisplayName);

            var positionId = espnPlayer.Position != null ? await FindPositionIdByNameAsync(espnPlayer.Position.Abbreviation, cancellationToken) : null;

            var updateRecord = new PlayerRecord
            {
                id = playerId,
                first_name = espnPlayer.FirstName,
                last_name = espnPlayer.LastName,
                espn_player_id = espnPlayer.Id,
                // team_id excluded - teams managed externally
                position_id = positionId,
                active = espnPlayer.Active,
                updated_at = DateTime.UtcNow
            };

            var response = await _supabaseClient
                .From<PlayerRecord>()
                .Where(p => p.id == playerId)
                .Update(updateRecord);

            if (response?.Models?.Count > 0)
            {
                _logger.LogInformation("Successfully updated player {PlayerId} with ESPN data (ESPN ID: {EspnId})",
                    playerId, espnPlayer.Id);
                return true;
            }

            _logger.LogWarning("Failed to update player {PlayerId}", playerId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating player {PlayerId}", playerId);
            return false;
        }
    }

    public async Task<long?> FindTeamIdByAbbreviationAsync(string abbreviation, CancellationToken cancellationToken = default)
    {
        try
        {
            var team = await _supabaseClient
                .From<TeamRecord>()
                .Where(t => t.abbreviation == abbreviation)
                .Single();

            if (team != null)
            {
                return team.id;
            }

            _logger.LogWarning("No team found for abbreviation: {Abbreviation}", abbreviation);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding team by abbreviation {Abbreviation}", abbreviation);
            return null;
        }
    }

    public async Task<long?> FindPositionIdByNameAsync(string positionName, CancellationToken cancellationToken = default)
    {
        try
        {
            var position = await _supabaseClient
                .From<PositionRecord>()
                .Where(p => p.name == positionName)
                .Single();

            if (position != null)
            {
                return position.id;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding position by name {PositionName}", positionName);
            return null;
        }
    }

    private static bool IsNameSimilar(string? dbName, string espnName)
    {
        if (string.IsNullOrWhiteSpace(dbName) || string.IsNullOrWhiteSpace(espnName))
            return false;

        // Normalize names for better comparison (handles C.J. -> CJ, etc.)
        var normalizedDbName = StringMatchingAlgorithms.NormalizeName(dbName.Trim());
        var normalizedEspnName = StringMatchingAlgorithms.NormalizeName(espnName.Trim());

        // Exact match after normalization
        if (string.Equals(normalizedDbName, normalizedEspnName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check if one name contains the other (for nicknames)
        return normalizedDbName.Contains(normalizedEspnName) || normalizedEspnName.Contains(normalizedDbName);
    }

    // Sync Operations Implementation
    public async Task<bool> SaveSyncReportAsync(SyncResult syncResult, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Saving sync report for SyncId: {SyncId}", syncResult.SyncId);

            var syncRecord = new SyncReportRecord
            {
                sync_id = syncResult.SyncId,
                sync_type = syncResult.SyncType.ToString(),
                status = syncResult.Status.ToString(),
                start_time = syncResult.StartTime,
                end_time = syncResult.EndTime,
                players_processed = syncResult.PlayersProcessed,
                new_players_added = syncResult.NewPlayersAdded,
                players_updated = syncResult.PlayersUpdated,
                stats_records_processed = syncResult.StatsRecordsProcessed,
                data_errors = syncResult.DataErrors,
                matching_errors = syncResult.MatchingErrors,
                errors = JsonSerializer.Serialize(syncResult.Errors),
                warnings = JsonSerializer.Serialize(syncResult.Warnings),
                options = JsonSerializer.Serialize(syncResult.Options),
                created_at = DateTime.UtcNow
            };

            var response = await _supabaseClient
                .From<SyncReportRecord>()
                .Insert(syncRecord);

            if (response?.Models?.Count > 0)
            {
                _logger.LogInformation("Successfully saved sync report for SyncId: {SyncId}", syncResult.SyncId);
                return true;
            }

            _logger.LogWarning("Failed to save sync report for SyncId: {SyncId}", syncResult.SyncId);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving sync report for SyncId: {SyncId}", syncResult.SyncId);
            return false;
        }
    }

    public async Task<SyncReport?> GetLastSyncReportAsync(SyncType? syncType = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting last sync report for type: {SyncType}", syncType);

            var response = syncType.HasValue
                ? await _supabaseClient.From<SyncReportRecord>()
                    .Where(r => r.sync_type == syncType.Value.ToString())
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Limit(1)
                    .Get()
                : await _supabaseClient.From<SyncReportRecord>()
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Limit(1)
                    .Get();

            if (response?.Models?.Count > 0)
            {
                var record = response.Models.First();
                var syncReport = ConvertToSyncReport(record);
                _logger.LogDebug("Found last sync report: {SyncId}", syncReport.Result.SyncId);
                return syncReport;
            }

            _logger.LogDebug("No sync reports found for type: {SyncType}", syncType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting last sync report for type: {SyncType}", syncType);
            return null;
        }
    }

    public async Task<List<SyncReport>> GetSyncHistoryAsync(int limit = 50, SyncType? syncType = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting sync history. Limit: {Limit}, Type: {SyncType}", limit, syncType);

            var response = syncType.HasValue
                ? await _supabaseClient.From<SyncReportRecord>()
                    .Where(r => r.sync_type == syncType.Value.ToString())
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Limit(limit)
                    .Get()
                : await _supabaseClient.From<SyncReportRecord>()
                    .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                    .Limit(limit)
                    .Get();

            var syncReports = new List<SyncReport>();

            if (response?.Models?.Count > 0)
            {
                foreach (var record in response.Models)
                {
                    syncReports.Add(ConvertToSyncReport(record));
                }
                _logger.LogDebug("Retrieved {Count} sync reports", syncReports.Count);
            }

            return syncReports;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sync history. Limit: {Limit}, Type: {SyncType}", limit, syncType);
            return new List<SyncReport>();
        }
    }

    public async Task<SyncMetrics> GetSyncMetricsAsync(DateTime? fromDate = null, DateTime? toDate = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Getting sync metrics from {FromDate} to {ToDate}", fromDate, toDate);

            var response = (fromDate.HasValue && toDate.HasValue)
                ? await _supabaseClient.From<SyncReportRecord>()
                    .Where(r => r.created_at >= fromDate.Value)
                    .Where(r => r.created_at <= toDate.Value)
                    .Get()
                : fromDate.HasValue
                    ? await _supabaseClient.From<SyncReportRecord>()
                        .Where(r => r.created_at >= fromDate.Value)
                        .Get()
                    : toDate.HasValue
                        ? await _supabaseClient.From<SyncReportRecord>()
                            .Where(r => r.created_at <= toDate.Value)
                            .Get()
                        : await _supabaseClient.From<SyncReportRecord>()
                            .Get();

            var metrics = new SyncMetrics
            {
                FromDate = fromDate ?? DateTime.UtcNow.AddDays(-30),
                ToDate = toDate ?? DateTime.UtcNow
            };

            if (response?.Models?.Count > 0)
            {
                var records = response.Models;

                metrics.TotalSyncOperations = records.Count;
                metrics.SuccessfulSyncs = records.Count(r => r.status == "Completed");
                metrics.FailedSyncs = records.Count(r => r.status == "Failed");

                // Calculate averages
                var completedSyncs = records.Where(r => r.end_time.HasValue).ToList();
                if (completedSyncs.Any())
                {
                    var durations = completedSyncs.Select(r =>
                        r.end_time!.Value - r.start_time).ToList();

                    metrics.AverageSyncDuration = TimeSpan.FromMilliseconds(durations.Average(d => d.TotalMilliseconds));
                }

                metrics.TotalRecordsProcessed = records.Sum(r => r.players_processed + r.stats_records_processed);
                metrics.TotalErrors = records.Sum(r => r.data_errors + r.matching_errors);

                _logger.LogDebug("Calculated metrics: {TotalSyncs} total, {SuccessfulSyncs} successful",
                    metrics.TotalSyncOperations, metrics.SuccessfulSyncs);
            }

            return metrics;
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

    public async Task UpdateSyncMetricsAsync(DateTime date, SyncResult syncResult, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Updating sync metrics for date: {Date}", date);

            var dateOnly = date.Date;

            // Try to get existing metrics for the date
            var existingMetrics = await _supabaseClient
                .From<SyncMetricsRecord>()
                .Where(m => m.date == dateOnly)
                .Single();

            if (existingMetrics != null)
            {
                // Update existing metrics
                existingMetrics.total_syncs++;
                if (syncResult.Status == SyncStatus.Completed)
                    existingMetrics.successful_syncs++;
                else if (syncResult.Status == SyncStatus.Failed)
                    existingMetrics.failed_syncs++;

                if (syncResult.EndTime.HasValue)
                {
                    var duration = (syncResult.EndTime.Value - syncResult.StartTime).TotalMilliseconds;
                    existingMetrics.avg_duration_ms = (existingMetrics.avg_duration_ms + duration) / 2;
                }

                existingMetrics.total_players_processed += syncResult.PlayersProcessed;
                existingMetrics.total_stats_processed += syncResult.StatsRecordsProcessed;
                existingMetrics.total_errors += syncResult.DataErrors + syncResult.MatchingErrors;
                existingMetrics.updated_at = DateTime.UtcNow;

                await _supabaseClient
                    .From<SyncMetricsRecord>()
                    .Where(m => m.id == existingMetrics.id)
                    .Update(existingMetrics);
            }
            else
            {
                // Create new metrics record
                var newMetrics = new SyncMetricsRecord
                {
                    date = dateOnly,
                    total_syncs = 1,
                    successful_syncs = syncResult.Status == SyncStatus.Completed ? 1 : 0,
                    failed_syncs = syncResult.Status == SyncStatus.Failed ? 1 : 0,
                    avg_duration_ms = syncResult.EndTime.HasValue ?
                        (syncResult.EndTime.Value - syncResult.StartTime).TotalMilliseconds : 0,
                    total_players_processed = syncResult.PlayersProcessed,
                    total_stats_processed = syncResult.StatsRecordsProcessed,
                    total_errors = syncResult.DataErrors + syncResult.MatchingErrors,
                    created_at = DateTime.UtcNow,
                    updated_at = DateTime.UtcNow
                };

                await _supabaseClient
                    .From<SyncMetricsRecord>()
                    .Insert(newMetrics);
            }

            _logger.LogDebug("Successfully updated sync metrics for date: {Date}", date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating sync metrics for date: {Date}", date);
        }
    }

    private static SyncReport ConvertToSyncReport(SyncReportRecord record)
    {
        var syncResult = new SyncResult
        {
            SyncId = record.sync_id ?? string.Empty,
            SyncType = Enum.TryParse<SyncType>(record.sync_type, out var syncType) ? syncType : SyncType.Players,
            Status = Enum.TryParse<SyncStatus>(record.status, out var status) ? status : SyncStatus.Failed,
            StartTime = record.start_time,
            EndTime = record.end_time,
            PlayersProcessed = record.players_processed,
            NewPlayersAdded = record.new_players_added,
            PlayersUpdated = record.players_updated,
            StatsRecordsProcessed = record.stats_records_processed,
            DataErrors = record.data_errors,
            MatchingErrors = record.matching_errors,
            Errors = !string.IsNullOrEmpty(record.errors) ?
                JsonSerializer.Deserialize<List<string>>(record.errors) ?? new List<string>() :
                new List<string>(),
            Warnings = !string.IsNullOrEmpty(record.warnings) ?
                JsonSerializer.Deserialize<List<string>>(record.warnings) ?? new List<string>() :
                new List<string>()
        };

        return new SyncReport
        {
            Result = syncResult
        };
    }

    #region Player Stats Operations

    public async Task<bool> SavePlayerStatsAsync(PlayerStatsRecord playerStats, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Saving player stats for ESPN Player {EspnPlayerId}, Game {EspnGameId}",
                playerStats.EspnPlayerId, playerStats.EspnGameId);

            // Validate required fields for duplicate checking
            if (string.IsNullOrEmpty(playerStats.EspnPlayerId) || string.IsNullOrEmpty(playerStats.EspnGameId))
            {
                _logger.LogWarning("Cannot save player stats - missing required fields: EspnPlayerId={EspnPlayerId}, EspnGameId={EspnGameId}",
                    playerStats.EspnPlayerId, playerStats.EspnGameId);
                return false;
            }

            // Check if stats already exist for this player and game using the logical key (ESPN IDs)
            var existingStats = await _supabaseClient
                .From<PlayerStatsRecord>()
                .Select("*") // Select all fields to compare data changes
                .Filter("espn_player_id", Supabase.Postgrest.Constants.Operator.Equals, playerStats.EspnPlayerId)
                .Filter("espn_game_id", Supabase.Postgrest.Constants.Operator.Equals, playerStats.EspnGameId)
                .Get(cancellationToken);

            var now = DateTime.UtcNow;

            if (existingStats?.Models?.Any() == true)
            {
                var existingRecord = existingStats.Models.First();

                // Compare the data to see if anything has changed
                if (HasStatsDataChanged(existingRecord, playerStats))
                {
                    _logger.LogDebug("Stats data has changed for ESPN Player {EspnPlayerId}, Game {EspnGameId} - updating record",
                        playerStats.EspnPlayerId, playerStats.EspnGameId);

                    // Update existing record - preserve original creation time and set new updated time
                    playerStats.Id = existingRecord.Id; // Ensure we update the correct record
                    playerStats.CreatedAt = existingRecord.CreatedAt; // Keep original creation time
                    playerStats.UpdatedAt = now;

                    var updateResult = await _supabaseClient
                        .From<PlayerStatsRecord>()
                        .Where(ps => ps.EspnPlayerId == playerStats.EspnPlayerId && ps.EspnGameId == playerStats.EspnGameId)
                        .Update(playerStats);

                    _logger.LogDebug("Updated existing player stats record for ESPN Player {EspnPlayerId}, Game {EspnGameId}",
                        playerStats.EspnPlayerId, playerStats.EspnGameId);
                    return updateResult?.Models?.Any() == true;
                }
                else
                {
                    _logger.LogDebug("No changes detected for ESPN Player {EspnPlayerId}, Game {EspnGameId} - skipping update",
                        playerStats.EspnPlayerId, playerStats.EspnGameId);
                    return true; // Return success since the data is already correct
                }
            }
            else
            {
                // Insert new record
                playerStats.CreatedAt = now;
                playerStats.UpdatedAt = now;

                var insertResult = await _supabaseClient
                    .From<PlayerStatsRecord>()
                    .Insert(playerStats);

                _logger.LogDebug("Inserted new player stats record for ESPN Player {EspnPlayerId}, Game {EspnGameId}",
                    playerStats.EspnPlayerId, playerStats.EspnGameId);
                return insertResult?.Models?.Any() == true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save player stats for ESPN Player {EspnPlayerId}, Game {EspnGameId}. Team: {Team}, Error: {ErrorMessage}",
                playerStats.EspnPlayerId, playerStats.EspnGameId, playerStats.Team, ex.Message);
            return false;
        }
    }

    public async Task<List<PlayerStatsRecord>> GetPlayerStatsAsync(long playerId, int? season = null, int? week = null, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Retrieving player stats for player ID {PlayerId}, season {Season}, week {Week}",
                playerId, season, week);

            var query = _supabaseClient
                .From<PlayerStatsRecord>()
                .Filter("player_id", Supabase.Postgrest.Constants.Operator.Equals, playerId)
                .Order("game_date", Supabase.Postgrest.Constants.Ordering.Descending);

            if (season.HasValue)
            {
                query = query.Filter("season", Supabase.Postgrest.Constants.Operator.Equals, season.Value);
            }

            if (week.HasValue)
            {
                query = query.Filter("week", Supabase.Postgrest.Constants.Operator.Equals, week.Value);
            }

            var result = await query.Get(cancellationToken);

            var stats = result?.Models?.ToList() ?? new List<PlayerStatsRecord>();

            _logger.LogDebug("Retrieved {StatsCount} player stats records for player ID {PlayerId}",
                stats.Count, playerId);

            return stats;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve player stats for player ID {PlayerId}", playerId);
            return new List<PlayerStatsRecord>();
        }
    }

    #endregion

    #region Private Helper Methods

    /// <summary>
    /// Maps ESPN team abbreviations to database team abbreviations
    /// </summary>
    private static string MapEspnTeamAbbreviation(string espnAbbreviation)
    {
        // Handle common team abbreviation differences between ESPN and database
        return espnAbbreviation.ToUpper() switch
        {
            "KC" => "KAN",      // Kansas City Chiefs
            "NE" => "NWE",      // New England Patriots  
            "NO" => "NOR",      // New Orleans Saints
            "SF" => "SFO",      // San Francisco 49ers
            "TB" => "TAM",      // Tampa Bay Buccaneers
            "LV" => "LVR",      // Las Vegas Raiders
            "GB" => "GNB",      // Green Bay Packers
            "WSH" => "WAS",     // Washington Commanders
            _ => espnAbbreviation.ToUpper()  // Return as-is for all others
        };
    }

    /// <summary>
    /// Maps full team names to database abbreviations
    /// </summary>
    private static string MapFullTeamNameToAbbreviation(string fullTeamName)
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

    public async Task<bool> SaveScheduleAsync(ScheduleRecord schedule, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Saving schedule record for game {EspnGameId}: Team {AwayTeamId} @ Team {HomeTeamId}",
                schedule.espn_game_id, schedule.away_team_id, schedule.home_team_id);

            // Check if schedule already exists
            var existingSchedule = await _supabaseClient
                .From<ScheduleRecord>()
                .Where(s => s.espn_game_id == schedule.espn_game_id)
                .Get();

            if (existingSchedule?.Models?.Any() == true)
            {
                // Update existing record
                var existing = existingSchedule.Models.First();
                existing.home_team_id = schedule.home_team_id;
                existing.away_team_id = schedule.away_team_id;
                existing.game_time = schedule.game_time;
                existing.week = schedule.week;
                existing.year = schedule.year;
                existing.season_type = schedule.season_type;
                existing.betting_line = schedule.betting_line;
                existing.over_under = schedule.over_under;
                existing.home_implied_points = schedule.home_implied_points;
                existing.away_implied_points = schedule.away_implied_points;
                existing.updated_at = DateTime.UtcNow;

                var updateResult = await _supabaseClient
                    .From<ScheduleRecord>()
                    .Update(existing);

                _logger.LogDebug("Updated existing schedule record for game {EspnGameId}",
                    schedule.espn_game_id);
                return updateResult?.Models?.Any() == true;
            }
            else
            {
                // Insert new record
                schedule.created_at = DateTime.UtcNow;
                schedule.updated_at = DateTime.UtcNow;

                var insertResult = await _supabaseClient
                    .From<ScheduleRecord>()
                    .Insert(schedule);

                _logger.LogDebug("Inserted new schedule record for game {EspnGameId}",
                    schedule.espn_game_id);
                return insertResult?.Models?.Any() == true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save schedule for game {EspnGameId}. Error: {ErrorMessage}",
                schedule.espn_game_id, ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Upserts player stats using native Supabase upsert functionality
    /// This method is more efficient than the SavePlayerStatsAsync method for bulk operations
    /// </summary>
    /// <param name="playerStats">The player stats record to upsert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the operation was successful, false otherwise</returns>
    public async Task<bool> UpsertPlayerStatsAsync(PlayerStatsRecord playerStats, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Upserting player stats for ESPN Player {EspnPlayerId}, Game {EspnGameId}",
                playerStats.EspnPlayerId, playerStats.EspnGameId);

            // Validate required fields
            if (string.IsNullOrEmpty(playerStats.EspnPlayerId) || string.IsNullOrEmpty(playerStats.EspnGameId))
            {
                _logger.LogWarning("Cannot upsert player stats - missing required fields: EspnPlayerId={EspnPlayerId}, EspnGameId={EspnGameId}",
                    playerStats.EspnPlayerId, playerStats.EspnGameId);
                return false;
            }

            var now = DateTime.UtcNow;
            playerStats.UpdatedAt = now;

            // If CreatedAt is not set, set it to now (for new records)
            if (playerStats.CreatedAt == default(DateTime))
            {
                playerStats.CreatedAt = now;
            }

            // Use Supabase's upsert functionality with the unique constraint columns
            // This will INSERT if the record doesn't exist, or UPDATE if it does
            var result = await _supabaseClient
                .From<PlayerStatsRecord>()
                .Upsert(playerStats);

            _logger.LogDebug("Successfully upserted player stats for ESPN Player {EspnPlayerId}, Game {EspnGameId}",
                playerStats.EspnPlayerId, playerStats.EspnGameId);

            return result?.Models?.Any() == true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Upsert failed for ESPN Player {EspnPlayerId}, Game {EspnGameId}, falling back to SavePlayerStatsAsync",
                playerStats.EspnPlayerId, playerStats.EspnGameId);

            // Fallback to our more robust SavePlayerStatsAsync method
            try
            {
                return await SavePlayerStatsAsync(playerStats, cancellationToken);
            }
            catch (Exception fallbackEx)
            {
                _logger.LogError(fallbackEx, "Fallback save also failed for ESPN Player {EspnPlayerId}, Game {EspnGameId}. Team: {Team}",
                    playerStats.EspnPlayerId, playerStats.EspnGameId, playerStats.Team);
                return false;
            }
        }
    }

    /// <summary>
    /// Upserts multiple player stats records in a single batch operation
    /// This method is more efficient for bulk operations than individual upserts
    /// </summary>
    /// <param name="playerStatsList">List of player stats records to upsert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of records successfully upserted</returns>
    public async Task<int> UpsertPlayerStatsBatchAsync(List<PlayerStatsRecord> playerStatsList, CancellationToken cancellationToken = default)
    {
        if (playerStatsList?.Any() != true)
        {
            _logger.LogWarning("UpsertPlayerStatsBatchAsync called with empty or null list");
            return 0;
        }

        try
        {
            _logger.LogInformation("Batch upserting {Count} player stats records", playerStatsList.Count);

            var now = DateTime.UtcNow;

            // Prepare all records for upsert
            foreach (var playerStats in playerStatsList)
            {
                // Validate required fields
                if (string.IsNullOrEmpty(playerStats.EspnPlayerId) || string.IsNullOrEmpty(playerStats.EspnGameId))
                {
                    _logger.LogWarning("Skipping player stats record with missing required fields: EspnPlayerId={EspnPlayerId}, EspnGameId={EspnGameId}",
                        playerStats.EspnPlayerId, playerStats.EspnGameId);
                    continue;
                }

                playerStats.UpdatedAt = now;

                // If CreatedAt is not set, set it to now (for new records)
                if (playerStats.CreatedAt == default(DateTime))
                {
                    playerStats.CreatedAt = now;
                }
            }

            // Filter out invalid records
            var validRecords = playerStatsList
                .Where(ps => !string.IsNullOrEmpty(ps.EspnPlayerId) && !string.IsNullOrEmpty(ps.EspnGameId))
                .ToList();

            if (!validRecords.Any())
            {
                _logger.LogWarning("No valid records to upsert after filtering");
                return 0;
            }

            // Use individual upserts for better error handling since batch upsert 
            // fails completely when there are conflicts with unique constraints
            var successCount = 0;
            foreach (var record in validRecords)
            {
                try
                {
                    var individualResult = await _supabaseClient
                        .From<PlayerStatsRecord>()
                        .Upsert(record);

                    if (individualResult?.Models?.Any() == true)
                    {
                        successCount++;
                    }
                }
                catch (Exception)
                {
                    // If individual upsert fails, try using our SavePlayerStatsAsync method
                    // which handles conflicts more gracefully
                    try
                    {
                        var saved = await SavePlayerStatsAsync(record, cancellationToken);
                        if (saved)
                        {
                            successCount++;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to save player stats for ESPN Player {EspnPlayerId}, Game {EspnGameId}",
                                record.EspnPlayerId, record.EspnGameId);
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        _logger.LogWarning(fallbackEx, "Failed fallback save for ESPN Player {EspnPlayerId}, Game {EspnGameId}",
                            record.EspnPlayerId, record.EspnGameId);
                    }
                }
            }

            _logger.LogInformation("Successfully processed {SuccessCount}/{TotalCount} player stats records",
                successCount, validRecords.Count);

            return successCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch upsert {Count} player stats records. Error: {ErrorMessage}",
                playerStatsList?.Count ?? 0, ex.Message);
            return 0;
        }
    }



    /// <summary>
    /// Compares two PlayerStatsRecord objects to determine if the statistical data has changed
    /// </summary>
    /// <param name="existing">The existing record from the database</param>
    /// <param name="incoming">The new record to compare</param>
    /// <returns>True if the data has changed, false otherwise</returns>
    private bool HasStatsDataChanged(PlayerStatsRecord existing, PlayerStatsRecord incoming)
    {
        try
        {
            // Compare basic player information
            if (existing.Name != incoming.Name ||
                existing.Team != incoming.Team ||
                existing.GameLocation != incoming.GameLocation ||
                existing.Season != incoming.Season ||
                existing.Week != incoming.Week)
            {
                return true;
            }

            // Compare statistical data (JSONB fields)
            // Convert to JSON strings for comparison since these are complex objects
            var existingPassingJson = System.Text.Json.JsonSerializer.Serialize(existing.Passing);
            var incomingPassingJson = System.Text.Json.JsonSerializer.Serialize(incoming.Passing);

            var existingRushingJson = System.Text.Json.JsonSerializer.Serialize(existing.Rushing);
            var incomingRushingJson = System.Text.Json.JsonSerializer.Serialize(incoming.Rushing);

            var existingReceivingJson = System.Text.Json.JsonSerializer.Serialize(existing.Receiving);
            var incomingReceivingJson = System.Text.Json.JsonSerializer.Serialize(incoming.Receiving);

            if (existingPassingJson != incomingPassingJson ||
                existingRushingJson != incomingRushingJson ||
                existingReceivingJson != incomingReceivingJson)
            {
                return true;
            }

            return false; // No changes detected
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error comparing player stats data for ESPN Player {EspnPlayerId}, Game {EspnGameId}. Assuming data has changed.",
                existing.EspnPlayerId, existing.EspnGameId);
            return true; // If we can't compare, assume it changed to be safe
        }
    }

    #endregion
}
