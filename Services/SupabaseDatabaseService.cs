using ESPNScrape.Models.Supabase;
using ESPNScrape.Models.DataSync;
using Microsoft.Extensions.Options;
using Supabase;
using System.Text.Json;

namespace ESPNScrape.Services;

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
            _logger.LogDebug("Searching for player with ESPN ID: {EspnId}", espnId);

            var response = await _supabaseClient
                .From<PlayerRecord>()
                .Where(p => p.espn_player_id == espnId)
                .Single();

            if (response != null)
            {
                _logger.LogDebug("Found player with ID {PlayerId} for ESPN ID {EspnId}", response.id, espnId);
                return response.id;
            }

            _logger.LogDebug("No player found for ESPN ID: {EspnId}", espnId);
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
            _logger.LogDebug("Searching for matching player: {FirstName} {LastName}", espnPlayer.FirstName, espnPlayer.LastName);

            // First try exact name match
            var exactMatch = await _supabaseClient
                .From<PlayerRecord>()
                .Where(p => p.first_name == espnPlayer.FirstName && p.last_name == espnPlayer.LastName)
                .Single();

            if (exactMatch != null)
            {
                var fullName = $"{exactMatch.first_name} {exactMatch.last_name}";
                _logger.LogDebug("Found exact match: Player ID {PlayerId} - {Name}", exactMatch.id, fullName);
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
                    _logger.LogDebug("Found fuzzy match: Player ID {PlayerId} - {Name}", fuzzyMatch.id, fullName);
                    return (fuzzyMatch.id, fullName);
                }
            }

            _logger.LogDebug("No matching player found for {FirstName} {LastName}", espnPlayer.FirstName, espnPlayer.LastName);
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

            // Get team and position IDs
            _logger.LogInformation("Player {PlayerName} team info: Team={Team}, Abbreviation={TeamAbbr}",
                espnPlayer.DisplayName, espnPlayer.Team?.DisplayName ?? "NULL", espnPlayer.Team?.Abbreviation ?? "NULL");

            long? teamId = null;
            if (espnPlayer.Team != null && !string.IsNullOrEmpty(espnPlayer.Team.Abbreviation))
            {
                // Map ESPN team abbreviations to database abbreviations
                var dbTeamAbbr = MapEspnTeamAbbreviation(espnPlayer.Team.Abbreviation);

                _logger.LogInformation("Looking up team ID for abbreviation: '{TeamAbbr}' (mapped from ESPN '{EspnAbbr}') for player {PlayerName}",
                    dbTeamAbbr, espnPlayer.Team.Abbreviation, espnPlayer.DisplayName);
                teamId = await FindTeamIdByAbbreviationAsync(dbTeamAbbr, cancellationToken);
                _logger.LogInformation("Team lookup result for '{TeamAbbr}': {TeamId}",
                    dbTeamAbbr, teamId?.ToString() ?? "NULL");
            }
            else
            {
                _logger.LogInformation("Player {PlayerName} has no team data - Team is null or abbreviation is empty",
                    espnPlayer.DisplayName);
            }

            // If no team found or player has no team, default to Free Agent (FA)
            if (teamId == null)
            {
                try
                {
                    teamId = await FindTeamIdByAbbreviationAsync("FA", cancellationToken);
                    _logger.LogDebug("Player {PlayerName} assigned to Free Agent team (ID: {TeamId})",
                        espnPlayer.DisplayName, teamId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to lookup FA team, using hardcoded ID 33");
                    teamId = 33; // Hardcoded FA team ID as fallback
                }

                // Final safety check - if FA lookup also failed, use hardcoded ID
                if (teamId == null)
                {
                    _logger.LogWarning("FA team lookup returned null, using hardcoded ID 33 for player {PlayerName}",
                        espnPlayer.DisplayName);
                    teamId = 33; // Free Agent team ID
                }
            }

            var positionId = espnPlayer.Position != null ? await FindPositionIdByNameAsync(espnPlayer.Position.Abbreviation, cancellationToken) : null;

            _logger.LogDebug("Looked up IDs for {PlayerName}: TeamId={TeamId}, PositionId={PositionId}",
                espnPlayer.DisplayName, teamId?.ToString() ?? "NULL", positionId?.ToString() ?? "NULL");

            var playerRecord = new PlayerRecord
            {
                first_name = espnPlayer.FirstName,
                last_name = espnPlayer.LastName,
                espn_player_id = espnPlayer.Id,
                team_id = teamId,
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
            _logger.LogError(ex, "Error adding player {FirstName} {LastName}", espnPlayer.FirstName, espnPlayer.LastName);
            return false;
        }
    }

    public async Task<bool> UpdatePlayerAsync(long playerId, Models.Player espnPlayer, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Updating player {PlayerId} with ESPN data", playerId);

            // Get team and position IDs - same logic as AddPlayerAsync
            long? teamId = null;
            if (espnPlayer.Team != null && !string.IsNullOrEmpty(espnPlayer.Team.Abbreviation))
            {
                // Map ESPN team abbreviations to database abbreviations
                var dbTeamAbbr = MapEspnTeamAbbreviation(espnPlayer.Team.Abbreviation);
                teamId = await FindTeamIdByAbbreviationAsync(dbTeamAbbr, cancellationToken);
            }

            // If no team found or player has no team, default to Free Agent (FA)
            if (teamId == null)
            {
                try
                {
                    teamId = await FindTeamIdByAbbreviationAsync("FA", cancellationToken);
                    _logger.LogDebug("Player {PlayerName} (ID: {PlayerId}) assigned to Free Agent team (ID: {TeamId})",
                        espnPlayer.DisplayName, playerId, teamId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to lookup FA team for player update, using hardcoded ID 33");
                    teamId = 33; // Hardcoded FA team ID as fallback
                }

                // Final safety check - if FA lookup also failed, use hardcoded ID
                if (teamId == null)
                {
                    _logger.LogWarning("FA team lookup returned null for player update, using hardcoded ID 33 for player {PlayerName} (ID: {PlayerId})",
                        espnPlayer.DisplayName, playerId);
                    teamId = 33; // Free Agent team ID
                }
            }

            var positionId = espnPlayer.Position != null ? await FindPositionIdByNameAsync(espnPlayer.Position.Abbreviation, cancellationToken) : null;

            var updateRecord = new PlayerRecord
            {
                id = playerId,
                first_name = espnPlayer.FirstName,
                last_name = espnPlayer.LastName,
                espn_player_id = espnPlayer.Id,
                team_id = teamId,
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
            _logger.LogDebug("Finding team ID for abbreviation: {Abbreviation}", abbreviation);

            var team = await _supabaseClient
                .From<TeamRecord>()
                .Where(t => t.abbreviation == abbreviation)
                .Single();

            if (team != null)
            {
                _logger.LogDebug("Found team ID {TeamId} for abbreviation {Abbreviation}", team.id, abbreviation);
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
            _logger.LogDebug("Finding position ID for name: {PositionName}", positionName);

            var position = await _supabaseClient
                .From<PositionRecord>()
                .Where(p => p.name == positionName)
                .Single();

            if (position != null)
            {
                _logger.LogDebug("Found position ID {PositionId} for name {PositionName}", position.id, positionName);
                return position.id;
            }

            _logger.LogDebug("No position found for name: {PositionName}", positionName);
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

        // Case-insensitive exact match
        if (string.Equals(dbName.Trim(), espnName.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;

        // Handle common nickname patterns
        var dbNameLower = dbName.Trim().ToLowerInvariant();
        var espnNameLower = espnName.Trim().ToLowerInvariant();

        // Check if one name contains the other (for nicknames)
        return dbNameLower.Contains(espnNameLower) || espnNameLower.Contains(dbNameLower);
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
            _logger.LogDebug("Saving player stats for player code {PlayerCode}, game date {GameDate}",
                playerStats.PlayerCode, playerStats.GameDate);

            // Set timestamps
            var now = DateTime.UtcNow;
            playerStats.CreatedAt = now;
            playerStats.UpdatedAt = now;

            // Check if stats already exist for this player and game (using composite key)
            var existingStats = await _supabaseClient
                .From<PlayerStatsRecord>()
                .Select("id")
                .Filter("player_code", Supabase.Postgrest.Constants.Operator.Equals, playerStats.PlayerCode)
                .Filter("game_date", Supabase.Postgrest.Constants.Operator.Equals, playerStats.GameDate.ToString("O"))
                .Get(cancellationToken);

            if (existingStats?.Models?.Any() == true)
            {
                // Update existing record using composite key
                playerStats.CreatedAt = existingStats.Models.First().CreatedAt; // Keep original creation time

                var updateResult = await _supabaseClient
                    .From<PlayerStatsRecord>()
                    .Where(ps => ps.PlayerCode == playerStats.PlayerCode && ps.GameDate == playerStats.GameDate)
                    .Update(playerStats);

                _logger.LogDebug("Updated existing player stats record for player {PlayerCode} on {GameDate}",
                    playerStats.PlayerCode, playerStats.GameDate);
                return updateResult?.Models?.Any() == true;
            }
            else
            {
                // Insert new record
                var insertResult = await _supabaseClient
                    .From<PlayerStatsRecord>()
                    .Insert(playerStats);

                _logger.LogDebug("Inserted new player stats record for player {PlayerCode} on {GameDate}",
                    playerStats.PlayerCode, playerStats.GameDate);
                return insertResult?.Models?.Any() == true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save player stats for player code {PlayerCode}, game date {GameDate}. Team: {Team}, Error: {ErrorMessage}",
                playerStats.PlayerCode, playerStats.GameDate, playerStats.Team, ex.Message);
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

    #endregion
}
