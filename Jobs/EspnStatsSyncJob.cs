using ESPNScrape.Models.DataSync;
using ESPNScrape.Services.Interfaces;
using Quartz;
using System.Text.Json;

namespace ESPNScrape.Jobs;

/// <summary>
/// Quartz job for scheduled synchronization of ESPN player statistics data
/// Runs weekly to collect current season statistics
/// </summary>
[DisallowConcurrentExecution]
public class EspnStatsSyncJob : IJob
{
    private readonly ILogger<EspnStatsSyncJob> _logger;
    private readonly IEspnDataSyncService _dataSyncService;
    private readonly IEspnApiService _espnApiService;
    private readonly IConfiguration _configuration;

    // Job execution tracking
    private static readonly Dictionary<string, DateTime> _lastExecutionTimes = new();
    private static readonly object _lockObject = new();

    public EspnStatsSyncJob(
        ILogger<EspnStatsSyncJob> logger,
        IEspnDataSyncService dataSyncService,
        IEspnApiService espnApiService,
        IConfiguration configuration)
    {
        _logger = logger;
        _dataSyncService = dataSyncService;
        _espnApiService = espnApiService;
        _configuration = configuration;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.FireInstanceId;
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting ESPN stats sync job {JobId} at {StartTime}", jobId, startTime);

            // Check if we're in NFL season
            if (!IsNflSeason())
            {
                _logger.LogInformation("ESPN stats sync job {JobId} skipped - currently off-season", jobId);
                return;
            }

            // Prevent overlapping executions
            if (!CanExecute(jobId))
            {
                _logger.LogWarning("ESPN stats sync job {JobId} skipped - previous execution still running", jobId);
                return;
            }

            // Mark execution start
            MarkExecutionStart(jobId, startTime);

            // Check if sync is already running
            if (await _dataSyncService.IsSyncRunningAsync(context.CancellationToken))
            {
                _logger.LogWarning("ESPN stats sync job {JobId} skipped - another sync operation is already running", jobId);
                return;
            }

            // Get current NFL season and week information
            var currentSeason = GetCurrentNflSeason();
            var currentWeek = await _espnApiService.GetCurrentWeekAsync(context.CancellationToken);

            _logger.LogInformation("Syncing statistics for NFL Season {Season}, Week {Week}, SeasonType {SeasonType}",
                currentSeason, currentWeek.WeekNumber, currentWeek.SeasonType);

            // Configure sync options
            var syncOptions = GetSyncOptions();

            // Sync current week stats
            var currentWeekResult = await SyncWeekStatsAsync(currentSeason, currentWeek.WeekNumber,
                syncOptions, context.CancellationToken);

            // Optionally sync previous weeks if configured to do so
            var syncPreviousWeeks = _configuration.GetValue("DataSync:StatsSync:SyncPreviousWeeks", false);
            var previousWeekResults = new List<SyncResult>();

            if (syncPreviousWeeks && currentWeek.WeekNumber > 1)
            {
                var weeksToSync = _configuration.GetValue("DataSync:StatsSync:PreviousWeeksCount", 2);
                previousWeekResults = await SyncPreviousWeeksAsync(currentSeason, currentWeek.WeekNumber,
                    weeksToSync, syncOptions, context.CancellationToken);
            }

            // Aggregate and log results
            var totalResults = AggregateResults(currentWeekResult, previousWeekResults);
            LogSyncResults(jobId, totalResults);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("ESPN stats sync job {JobId} completed successfully in {Duration}ms. " +
                "Total processed: {PlayersProcessed}, Updated: {PlayersUpdated}, Errors: {DataErrors}",
                jobId, duration.TotalMilliseconds, totalResults.PlayersProcessed,
                totalResults.PlayersUpdated, totalResults.DataErrors);

            // Record success metrics
            await RecordJobMetricsAsync(jobId, true, duration, totalResults, null, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ESPN stats sync job {JobId} was cancelled", jobId);
            throw;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "ESPN stats sync job {JobId} failed after {Duration}ms", jobId, duration.TotalMilliseconds);

            // Record failure metrics
            await RecordJobMetricsAsync(jobId, false, duration, null, ex.Message, context.CancellationToken);

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
    /// Synchronizes statistics for a specific week
    /// </summary>
    private async Task<SyncResult> SyncWeekStatsAsync(int season, int week, SyncOptions syncOptions, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Syncing statistics for Season {Season}, Week {Week}", season, week);

            var result = await _dataSyncService.SyncPlayerStatsAsync(season, week, syncOptions, cancellationToken);

            _logger.LogInformation("Week {Week} sync completed: Processed {PlayersProcessed}, Updated {PlayersUpdated}, Errors {DataErrors}",
                week, result.PlayersProcessed, result.PlayersUpdated, result.DataErrors);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync statistics for Season {Season}, Week {Week}", season, week);
            throw;
        }
    }

    /// <summary>
    /// Synchronizes statistics for previous weeks as a catch-up mechanism
    /// </summary>
    private async Task<List<SyncResult>> SyncPreviousWeeksAsync(int season, int currentWeek, int weeksToSync,
        SyncOptions syncOptions, CancellationToken cancellationToken)
    {
        var results = new List<SyncResult>();

        // Sync previous weeks in reverse order (most recent first)
        for (int week = currentWeek - 1; week >= Math.Max(1, currentWeek - weeksToSync); week--)
        {
            try
            {
                _logger.LogInformation("Syncing catch-up statistics for Season {Season}, Week {Week}", season, week);

                // Use lighter sync options for catch-up
                var catchupOptions = new SyncOptions
                {
                    ForceFullSync = false, // Use incremental for catch-up
                    SkipInactives = syncOptions.SkipInactives,
                    BatchSize = syncOptions.BatchSize,
                    DryRun = syncOptions.DryRun,
                    MaxRetries = Math.Max(1, syncOptions.MaxRetries - 1), // Fewer retries for catch-up
                    RetryDelayMs = syncOptions.RetryDelayMs
                };

                var result = await _dataSyncService.SyncPlayerStatsAsync(season, week, catchupOptions, cancellationToken);
                results.Add(result);

                _logger.LogInformation("Catch-up Week {Week} sync completed: Processed {PlayersProcessed}, Updated {PlayersUpdated}",
                    week, result.PlayersProcessed, result.PlayersUpdated);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync catch-up statistics for Season {Season}, Week {Week}", season, week);

                // Create a failed result for tracking
                var failedResult = new SyncResult
                {
                    PlayersProcessed = 0,
                    PlayersUpdated = 0,
                    NewPlayersAdded = 0,
                    DataErrors = 1,
                    MatchingErrors = 0,
                    Errors = new List<string> { ex.Message }
                };
                failedResult.EndTime = failedResult.StartTime; // Set end time to calculate duration
                results.Add(failedResult);
            }
        }

        return results;
    }

    /// <summary>
    /// Aggregates multiple sync results into a single result
    /// </summary>
    private SyncResult AggregateResults(SyncResult currentWeekResult, List<SyncResult> previousWeekResults)
    {
        var aggregated = new SyncResult
        {
            PlayersProcessed = currentWeekResult.PlayersProcessed,
            PlayersUpdated = currentWeekResult.PlayersUpdated,
            NewPlayersAdded = currentWeekResult.NewPlayersAdded,
            DataErrors = currentWeekResult.DataErrors,
            MatchingErrors = currentWeekResult.MatchingErrors,
            Errors = new List<string>(currentWeekResult.Errors),
            Warnings = new List<string>(currentWeekResult.Warnings)
        };

        foreach (var result in previousWeekResults)
        {
            aggregated.PlayersProcessed += result.PlayersProcessed;
            aggregated.PlayersUpdated += result.PlayersUpdated;
            aggregated.NewPlayersAdded += result.NewPlayersAdded;
            aggregated.DataErrors += result.DataErrors;
            aggregated.MatchingErrors += result.MatchingErrors;
            aggregated.Errors.AddRange(result.Errors);
            aggregated.Warnings.AddRange(result.Warnings);
        }

        // Set end time to calculate total duration
        aggregated.EndTime = DateTime.UtcNow;

        return aggregated;
    }

    /// <summary>
    /// Gets current NFL season year
    /// </summary>
    private int GetCurrentNflSeason()
    {
        var now = DateTime.Now;

        // NFL season spans calendar years (September 2024 - February 2025 = 2024 season)
        if (now.Month >= 3 && now.Month <= 8)
        {
            // March through August - off-season, return next season
            return now.Year;
        }
        else if (now.Month >= 9)
        {
            // September through December - current year season
            return now.Year;
        }
        else
        {
            // January and February - previous year season
            return now.Year - 1;
        }
    }

    /// <summary>
    /// Gets sync options from configuration
    /// </summary>
    private SyncOptions GetSyncOptions()
    {
        var options = new SyncOptions();

        // Configure from appsettings or use defaults
        var config = _configuration.GetSection("DataSync:StatsSync");

        options.ForceFullSync = config.GetValue("ForceFullSync", false);
        options.SkipInactives = config.GetValue("SkipInactives", true);
        options.BatchSize = config.GetValue("BatchSize", 200); // Increased batch size for faster processing
        options.DryRun = config.GetValue("DryRun", false);
        options.MaxRetries = config.GetValue("MaxRetries", 3);
        options.RetryDelayMs = config.GetValue("RetryDelayMs", 2000); // Longer delay for stats

        return options;
    }

    /// <summary>
    /// Logs detailed sync results for monitoring and debugging
    /// </summary>
    private void LogSyncResults(string jobId, SyncResult syncResult)
    {
        if (syncResult.DataErrors > 0 || syncResult.MatchingErrors > 0)
        {
            _logger.LogWarning("ESPN stats sync job {JobId} completed with errors. " +
                "Data errors: {DataErrors}, Matching errors: {MatchingErrors}",
                jobId, syncResult.DataErrors, syncResult.MatchingErrors);

            // Log specific errors if available
            if (syncResult.Errors.Any())
            {
                foreach (var error in syncResult.Errors.Take(10)) // Limit to first 10 errors
                {
                    _logger.LogWarning("Sync error: {Error}", error);
                }

                if (syncResult.Errors.Count > 10)
                {
                    _logger.LogWarning("... and {AdditionalErrors} more errors", syncResult.Errors.Count - 10);
                }
            }
        }

        if (syncResult.Warnings.Any())
        {
            _logger.LogInformation("ESPN stats sync job {JobId} completed with {WarningCount} warnings",
                jobId, syncResult.Warnings.Count);

            // Log specific warnings if available
            foreach (var warning in syncResult.Warnings.Take(5)) // Limit to first 5 warnings
            {
                _logger.LogInformation("Sync warning: {Warning}", warning);
            }
        }
    }

    /// <summary>
    /// Determines if we're currently in NFL season
    /// </summary>
    private bool IsNflSeason()
    {
        var now = DateTime.Now;

        // NFL season typically runs from August through February of the following year
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
            var timeoutMinutes = int.TryParse(timeoutConfig, out var timeout) ? timeout : 120; // Stats sync can take longer

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
    private async Task RecordJobMetricsAsync(string jobId, bool success, TimeSpan duration, SyncResult? syncResult, string? errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            var metrics = new
            {
                JobId = jobId,
                JobType = "StatsSync",
                Success = success,
                Duration = duration.TotalMilliseconds,
                Timestamp = DateTime.UtcNow,
                ErrorMessage = errorMessage,
                SyncMetrics = syncResult != null ? new
                {
                    PlayersProcessed = syncResult.PlayersProcessed,
                    PlayersUpdated = syncResult.PlayersUpdated,
                    NewPlayersAdded = syncResult.NewPlayersAdded,
                    DataErrors = syncResult.DataErrors,
                    MatchingErrors = syncResult.MatchingErrors,
                    WarningCount = syncResult.Warnings.Count
                } : null
            };

            var fileName = $"stats_sync_metrics_{DateTime.UtcNow:yyyyMMdd}.json";
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

    /// <summary>
    /// Gets the full file path for data storage
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
    }
}