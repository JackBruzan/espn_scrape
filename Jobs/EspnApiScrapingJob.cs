using ESPNScrape.Models.Espn;
using ESPNScrape.Services.Interfaces;
using Quartz;
using System.Text.Json;

namespace ESPNScrape.Jobs;

/// <summary>
/// Quartz job for scheduled collection of ESPN NFL data using the ESPN API service
/// </summary>
[DisallowConcurrentExecution]
public class EspnApiScrapingJob : IJob
{
    private readonly ILogger<EspnApiScrapingJob> _logger;
    private readonly IEspnApiService _espnApiService;
    private readonly IEspnCacheService _cacheService;
    private readonly IConfiguration _configuration;

    // Job execution tracking
    private static readonly Dictionary<string, DateTime> _lastExecutionTimes = new();
    private static readonly object _lockObject = new();

    public EspnApiScrapingJob(
        ILogger<EspnApiScrapingJob> logger,
        IEspnApiService espnApiService,
        IEspnCacheService cacheService,
        IConfiguration configuration)
    {
        _logger = logger;
        _espnApiService = espnApiService;
        _cacheService = cacheService;
        _configuration = configuration;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.FireInstanceId;
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting ESPN API scraping job {JobId} at {StartTime}", jobId, startTime);

            // Check if we're in NFL season
            if (!IsNflSeason())
            {
                _logger.LogInformation("ESPN API scraping job {JobId} skipped - currently off-season", jobId);
                return;
            }

            // Prevent overlapping executions
            if (!CanExecute(jobId))
            {
                _logger.LogWarning("ESPN API scraping job {JobId} skipped - previous execution still running", jobId);
                return;
            }

            // Mark execution start
            MarkExecutionStart(jobId, startTime);

            // Get current week information
            var currentWeek = await _espnApiService.GetCurrentWeekAsync(context.CancellationToken);
            var currentYear = DateTime.Now.Year;

            _logger.LogInformation("Collecting data for NFL Season {Year}, Week {Week}, SeasonType {SeasonType}",
                currentYear, currentWeek.WeekNumber, currentWeek.SeasonType);

            // Collect week data
            await CollectWeekDataAsync(currentYear, currentWeek.WeekNumber, currentWeek.SeasonType, context.CancellationToken);

            // Update cache warming for next week
            await _cacheService.WarmCacheAsync(currentYear, currentWeek.WeekNumber + 1, context.CancellationToken);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("ESPN API scraping job {JobId} completed successfully in {Duration}ms",
                jobId, duration.TotalMilliseconds);

            // Record success metrics
            await RecordJobMetricsAsync(jobId, true, duration, null, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ESPN API scraping job {JobId} was cancelled", jobId);
            throw;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "ESPN API scraping job {JobId} failed after {Duration}ms", jobId, duration.TotalMilliseconds);

            // Record failure metrics
            await RecordJobMetricsAsync(jobId, false, duration, ex.Message, context.CancellationToken);

            // Re-throw to let Quartz handle retry logic
            throw;
        }
        finally
        {
            // Mark execution complete
            MarkExecutionComplete(jobId);
        }
    }

    /// <summary>
    /// Collects comprehensive data for a specific week
    /// </summary>
    private async Task CollectWeekDataAsync(int year, int weekNumber, int seasonType, CancellationToken cancellationToken)
    {
        try
        {
            // Get games for the week
            var games = await _espnApiService.GetGamesAsync(year, weekNumber, seasonType, cancellationToken);
            _logger.LogInformation("Found {GameCount} games for Week {Week}", games.Count(), weekNumber);

            // Save games data
            await SaveGamesDataAsync(year, weekNumber, seasonType, games, cancellationToken);

            // Collect detailed data for each game
            var gameDetailTasks = games.Select(game => CollectGameDetailDataAsync(game, cancellationToken));
            await Task.WhenAll(gameDetailTasks);

            // Collect aggregated week statistics
            await CollectWeekStatisticsAsync(year, weekNumber, seasonType, cancellationToken);

            _logger.LogInformation("Successfully collected complete data for Week {Week}", weekNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect data for Week {Week}", weekNumber);
            throw;
        }
    }

    /// <summary>
    /// Collects detailed data for a specific game
    /// </summary>
    private async Task CollectGameDetailDataAsync(GameEvent game, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrEmpty(game.Id))
            {
                _logger.LogWarning("Skipping game with empty ID");
                return;
            }

            _logger.LogDebug("Collecting detailed data for game {GameId}", game.Id);

            // Get box score data
            var boxScore = await _espnApiService.GetBoxScoreAsync(game.Id, cancellationToken);
            await SaveBoxScoreDataAsync(game.Id, boxScore, cancellationToken);

            // Get player statistics
            var playerStats = await _espnApiService.GetGamePlayerStatsAsync(game.Id, cancellationToken);
            await SavePlayerStatsDataAsync(game.Id, playerStats, cancellationToken);

            _logger.LogDebug("Successfully collected detailed data for game {GameId}", game.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect detailed data for game {GameId}", game.Id);
            // Don't re-throw individual game failures
        }
    }

    /// <summary>
    /// Collects aggregated statistics for the entire week
    /// </summary>
    private async Task CollectWeekStatisticsAsync(int year, int weekNumber, int seasonType, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug("Collecting aggregated statistics for Week {Week}", weekNumber);

            // Get all player statistics for the week
            var weekPlayerStats = await _espnApiService.GetAllPlayersWeekStatsAsync(year, weekNumber, seasonType, cancellationToken);
            await SaveWeekPlayerStatsAsync(year, weekNumber, seasonType, weekPlayerStats, cancellationToken);

            _logger.LogDebug("Successfully collected aggregated statistics for Week {Week}", weekNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect aggregated statistics for Week {Week}", weekNumber);
            // Don't re-throw aggregated stats failures
        }
    }

    /// <summary>
    /// Saves games data to JSON file
    /// </summary>
    private async Task SaveGamesDataAsync(int year, int weekNumber, int seasonType, IEnumerable<GameEvent> games, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = $"games_y{year}_w{weekNumber}_st{seasonType}.json";
            var filePath = GetDataFilePath(fileName);

            var json = JsonSerializer.Serialize(games, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            _logger.LogDebug("Saved games data to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save games data for Week {Week}", weekNumber);
            throw;
        }
    }

    /// <summary>
    /// Saves box score data to JSON file
    /// </summary>
    private async Task SaveBoxScoreDataAsync(string gameId, BoxScore boxScore, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = $"boxscore_{gameId}.json";
            var filePath = GetDataFilePath("boxscores", fileName);

            var json = JsonSerializer.Serialize(boxScore, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            _logger.LogDebug("Saved box score data to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save box score data for game {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// Saves player statistics data to JSON file
    /// </summary>
    private async Task SavePlayerStatsDataAsync(string gameId, IEnumerable<PlayerStats> playerStats, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = $"playerstats_{gameId}.json";
            var filePath = GetDataFilePath("playerstats", fileName);

            var json = JsonSerializer.Serialize(playerStats, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            _logger.LogDebug("Saved player stats data to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save player stats data for game {GameId}", gameId);
            throw;
        }
    }

    /// <summary>
    /// Saves week-aggregated player statistics to JSON file
    /// </summary>
    private async Task SaveWeekPlayerStatsAsync(int year, int weekNumber, int seasonType, IEnumerable<PlayerStats> playerStats, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = $"week_playerstats_y{year}_w{weekNumber}_st{seasonType}.json";
            var filePath = GetDataFilePath("weekly_stats", fileName);

            var json = JsonSerializer.Serialize(playerStats, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json, cancellationToken);
            _logger.LogDebug("Saved week player stats data to {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save week player stats data for Week {Week}", weekNumber);
            throw;
        }
    }

    /// <summary>
    /// Gets the full file path for data storage
    /// </summary>
    private string GetDataFilePath(string fileName)
    {
        var dataDirectory = _configuration["DataStorage:Directory"] ?? "data";
        var fullPath = Path.Combine(dataDirectory, fileName);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        return fullPath;
    }

    /// <summary>
    /// Gets the full file path for data storage with subdirectory
    /// </summary>
    private string GetDataFilePath(string subdirectory, string fileName)
    {
        var dataDirectory = _configuration["DataStorage:Directory"] ?? "data";
        var fullPath = Path.Combine(dataDirectory, subdirectory, fileName);

        // Ensure directory exists
        var directory = Path.GetDirectoryName(fullPath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory!);
        }

        return fullPath;
    }    /// <summary>
         /// Determines if we're currently in NFL season
         /// </summary>
    private bool IsNflSeason()
    {
        var now = DateTime.Now;
        var currentYear = now.Year;

        // NFL season typically runs from September through February of the following year
        // Preseason: August
        // Regular season: September - December/January
        // Postseason: January - February

        if (now.Month >= 8 && now.Month <= 12)
        {
            // August through December of current year
            return true;
        }
        else if (now.Month >= 1 && now.Month <= 2)
        {
            // January and February (postseason)
            return true;
        }

        // Override for testing or manual execution
        var forceExecutionConfig = _configuration["Job:ForceExecution"];
        var forceExecution = bool.TryParse(forceExecutionConfig, out var force) && force;
        if (forceExecution)
        {
            _logger.LogInformation("Forcing job execution due to configuration setting");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the job can execute (prevents overlapping executions)
    /// </summary>
    private bool CanExecute(string jobId)
    {
        lock (_lockObject)
        {
            var timeoutConfig = _configuration["Job:TimeoutMinutes"];
            var timeoutMinutes = int.TryParse(timeoutConfig, out var timeout) ? timeout : 30;

            if (_lastExecutionTimes.TryGetValue(jobId, out var lastExecution))
            {
                var elapsed = DateTime.UtcNow - lastExecution;
                if (elapsed.TotalMinutes < timeoutMinutes)
                {
                    return false; // Still running
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Marks the start of job execution
    /// </summary>
    private void MarkExecutionStart(string jobId, DateTime startTime)
    {
        lock (_lockObject)
        {
            _lastExecutionTimes[jobId] = startTime;
        }
    }

    /// <summary>
    /// Marks the completion of job execution
    /// </summary>
    private void MarkExecutionComplete(string jobId)
    {
        lock (_lockObject)
        {
            _lastExecutionTimes.Remove(jobId);
        }
    }

    /// <summary>
    /// Records job execution metrics for monitoring
    /// </summary>
    private async Task RecordJobMetricsAsync(string jobId, bool success, TimeSpan duration, string? errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            var metrics = new
            {
                JobId = jobId,
                Success = success,
                Duration = duration.TotalMilliseconds,
                Timestamp = DateTime.UtcNow,
                ErrorMessage = errorMessage
            };

            var fileName = $"job_metrics_{DateTime.UtcNow:yyyyMMdd}.json";
            var filePath = GetDataFilePath("metrics", fileName);

            var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            // Append to daily metrics file
            await File.AppendAllTextAsync(filePath, json + Environment.NewLine, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to record job metrics for {JobId}", jobId);
            // Don't re-throw metrics failures
        }
    }
}