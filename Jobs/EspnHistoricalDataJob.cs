using ESPNScrape.Models.DataSync;
using ESPNScrape.Services.Interfaces;
using Quartz;
using System.Text.Json;

namespace ESPNScrape.Jobs;

/// <summary>
/// Quartz job for historical data backfill operations
/// This job is designed to be manually triggered and should not run on a schedule
/// </summary>
[DisallowConcurrentExecution]
public class EspnHistoricalDataJob : IJob
{
    private readonly ILogger<EspnHistoricalDataJob> _logger;
    private readonly IEspnDataSyncService _dataSyncService;
    private readonly IConfiguration _configuration;

    // Job execution tracking
    private static readonly Dictionary<string, DateTime> _lastExecutionTimes = new();
    private static readonly object _lockObject = new();

    public EspnHistoricalDataJob(
        ILogger<EspnHistoricalDataJob> logger,
        IEspnDataSyncService dataSyncService,
        IConfiguration configuration)
    {
        _logger = logger;
        _dataSyncService = dataSyncService;
        _configuration = configuration;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.FireInstanceId;
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting ESPN historical data job {JobId} at {StartTime}", jobId, startTime);

            // Prevent overlapping executions
            if (!CanExecute(jobId))
            {
                _logger.LogWarning("ESPN historical data job {JobId} skipped - previous execution still running", jobId);
                return;
            }

            // Mark execution start
            MarkExecutionStart(jobId, startTime);

            // Check if sync is already running
            if (await _dataSyncService.IsSyncRunningAsync(context.CancellationToken))
            {
                _logger.LogWarning("ESPN historical data job {JobId} skipped - another sync operation is already running", jobId);
                return;
            }

            // Get job parameters from JobDataMap
            var jobData = context.JobDetail.JobDataMap;
            var mergedJobData = new JobDataMap();
            mergedJobData.PutAll(jobData);
            mergedJobData.PutAll(context.Trigger.JobDataMap);

            var syncType = mergedJobData.GetString("SyncType") ?? "Full";
            var season = mergedJobData.GetIntValue("Season");
            var startWeek = mergedJobData.GetIntValue("StartWeek");
            var endWeek = mergedJobData.GetIntValue("EndWeek");
            var startDate = mergedJobData.GetString("StartDate") ?? string.Empty;
            var endDate = mergedJobData.GetString("EndDate") ?? string.Empty;

            _logger.LogInformation("Historical data sync parameters: Type={SyncType}, Season={Season}, " +
                "StartWeek={StartWeek}, EndWeek={EndWeek}, StartDate={StartDate}, EndDate={EndDate}",
                syncType, season, startWeek, endWeek, startDate, endDate);

            // Configure sync options for historical data
            var syncOptions = GetHistoricalSyncOptions();

            SyncResult result;

            // Execute the appropriate sync operation based on parameters
            switch (syncType.ToLowerInvariant())
            {
                case "players":
                    result = await SyncHistoricalPlayersAsync(syncOptions, context.CancellationToken);
                    break;

                case "stats":
                    if (season > 0 && startWeek > 0 && endWeek > 0)
                    {
                        result = await SyncHistoricalStatsAsync(season, startWeek, endWeek, syncOptions, context.CancellationToken);
                    }
                    else
                    {
                        throw new ArgumentException("Season, StartWeek, and EndWeek are required for stats sync");
                    }
                    break;

                case "daterange":
                    if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
                    {
                        var parsedStartDate = DateTime.Parse(startDate);
                        var parsedEndDate = DateTime.Parse(endDate);
                        result = await _dataSyncService.SyncPlayerStatsForDateRangeAsync(parsedStartDate, parsedEndDate, syncOptions, context.CancellationToken);
                    }
                    else
                    {
                        throw new ArgumentException("StartDate and EndDate are required for date range sync");
                    }
                    break;

                case "full":
                default:
                    if (season > 0)
                    {
                        result = await _dataSyncService.FullSyncAsync(season, syncOptions, context.CancellationToken);
                    }
                    else
                    {
                        throw new ArgumentException("Season is required for full sync");
                    }
                    break;
            }

            // Log sync results
            LogSyncResults(jobId, result);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("ESPN historical data job {JobId} completed successfully in {Duration}ms. " +
                "Type: {SyncType}, Processed: {PlayersProcessed}, Updated: {PlayersUpdated}, Errors: {DataErrors}",
                jobId, duration.TotalMilliseconds, syncType, result.PlayersProcessed,
                result.PlayersUpdated, result.DataErrors);

            // Record success metrics
            await RecordJobMetricsAsync(jobId, true, duration, result, syncType, null, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ESPN historical data job {JobId} was cancelled", jobId);
            throw;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "ESPN historical data job {JobId} failed after {Duration}ms", jobId, duration.TotalMilliseconds);

            // Record failure metrics
            var jobData = context.JobDetail.JobDataMap;
            var mergedJobData = new JobDataMap();
            mergedJobData.PutAll(jobData);
            mergedJobData.PutAll(context.Trigger.JobDataMap);
            var syncType = mergedJobData.GetString("SyncType") ?? "Unknown";
            await RecordJobMetricsAsync(jobId, false, duration, null, syncType, ex.Message, context.CancellationToken);

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
    /// Synchronizes historical player data
    /// </summary>
    private async Task<SyncResult> SyncHistoricalPlayersAsync(SyncOptions syncOptions, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting historical player data synchronization");

        // Force full sync for historical data
        syncOptions.ForceFullSync = true;

        var result = await _dataSyncService.SyncPlayersAsync(syncOptions, cancellationToken);

        _logger.LogInformation("Historical player sync completed: Processed {PlayersProcessed}, Updated {PlayersUpdated}, New {NewPlayersAdded}",
            result.PlayersProcessed, result.PlayersUpdated, result.NewPlayersAdded);

        return result;
    }

    /// <summary>
    /// Synchronizes historical statistics for a range of weeks
    /// </summary>
    private async Task<SyncResult> SyncHistoricalStatsAsync(int season, int startWeek, int endWeek,
        SyncOptions syncOptions, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting historical stats synchronization for Season {Season}, Weeks {StartWeek}-{EndWeek}",
            season, startWeek, endWeek);

        var allResults = new List<SyncResult>();

        // Process each week sequentially to avoid overwhelming the API
        for (int week = startWeek; week <= endWeek; week++)
        {
            try
            {
                _logger.LogInformation("Processing historical stats for Season {Season}, Week {Week}", season, week);

                var weekResult = await _dataSyncService.SyncPlayerStatsAsync(season, week, syncOptions, cancellationToken);
                allResults.Add(weekResult);

                _logger.LogInformation("Week {Week} completed: Processed {PlayersProcessed}, Updated {PlayersUpdated}",
                    week, weekResult.PlayersProcessed, weekResult.PlayersUpdated);

                // Add delay between weeks to be respectful to the API
                var delayMs = _configuration.GetValue("HistoricalSync:DelayBetweenWeeksMs", 5000);
                if (delayMs > 0 && week < endWeek)
                {
                    _logger.LogDebug("Waiting {DelayMs}ms before processing next week", delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process historical stats for Season {Season}, Week {Week}", season, week);

                // Create a failed result for tracking
                var failedResult = new SyncResult
                {
                    PlayersProcessed = 0,
                    PlayersUpdated = 0,
                    NewPlayersAdded = 0,
                    DataErrors = 1,
                    MatchingErrors = 0,
                    Errors = new List<string> { $"Week {week}: {ex.Message}" }
                };
                failedResult.EndTime = failedResult.StartTime;
                allResults.Add(failedResult);

                // Decide whether to continue or stop based on configuration
                var continueOnError = _configuration.GetValue("HistoricalSync:ContinueOnError", true);
                if (!continueOnError)
                {
                    _logger.LogError("Stopping historical sync due to error in Week {Week}", week);
                    break;
                }
            }
        }

        // Aggregate all results
        var aggregatedResult = AggregateResults(allResults);

        _logger.LogInformation("Historical stats sync completed for Season {Season}, Weeks {StartWeek}-{EndWeek}. " +
            "Total processed: {PlayersProcessed}, Updated: {PlayersUpdated}, Errors: {DataErrors}",
            season, startWeek, endWeek, aggregatedResult.PlayersProcessed,
            aggregatedResult.PlayersUpdated, aggregatedResult.DataErrors);

        return aggregatedResult;
    }

    /// <summary>
    /// Aggregates multiple sync results into a single result
    /// </summary>
    private SyncResult AggregateResults(List<SyncResult> results)
    {
        if (!results.Any())
        {
            var emptyResult = new SyncResult();
            emptyResult.EndTime = emptyResult.StartTime;
            return emptyResult;
        }

        var aggregated = new SyncResult
        {
            StartTime = results.Min(r => r.StartTime),
            Errors = new List<string>(),
            Warnings = new List<string>()
        };

        foreach (var result in results)
        {
            aggregated.PlayersProcessed += result.PlayersProcessed;
            aggregated.PlayersUpdated += result.PlayersUpdated;
            aggregated.NewPlayersAdded += result.NewPlayersAdded;
            aggregated.DataErrors += result.DataErrors;
            aggregated.MatchingErrors += result.MatchingErrors;
            aggregated.StatsRecordsProcessed += result.StatsRecordsProcessed;
            aggregated.NewStatsAdded += result.NewStatsAdded;
            aggregated.Errors.AddRange(result.Errors);
            aggregated.Warnings.AddRange(result.Warnings);
        }

        // Set end time to latest completion time
        aggregated.EndTime = results.Where(r => r.EndTime.HasValue).Max(r => r.EndTime) ?? DateTime.UtcNow;

        return aggregated;
    }

    /// <summary>
    /// Gets sync options specifically configured for historical data operations
    /// </summary>
    private SyncOptions GetHistoricalSyncOptions()
    {
        var options = new SyncOptions();

        // Configure from appsettings or use defaults optimized for historical sync
        var config = _configuration.GetSection("DataSync:HistoricalSync");

        options.ForceFullSync = config.GetValue("ForceFullSync", true); // Default to full sync for historical
        options.SkipInactives = config.GetValue("SkipInactives", false); // Include inactive players for historical data
        options.BatchSize = config.GetValue("BatchSize", 25); // Smaller batches for historical to avoid timeouts
        options.DryRun = config.GetValue("DryRun", false);
        options.MaxRetries = config.GetValue("MaxRetries", 5); // More retries for historical
        options.RetryDelayMs = config.GetValue("RetryDelayMs", 5000); // Longer delays for historical
        options.SkipInvalidRecords = config.GetValue("SkipInvalidRecords", true);
        options.CreateBackup = config.GetValue("CreateBackup", true); // Always backup for historical operations

        return options;
    }

    /// <summary>
    /// Logs detailed sync results for monitoring and debugging
    /// </summary>
    private void LogSyncResults(string jobId, SyncResult syncResult)
    {
        if (syncResult.DataErrors > 0 || syncResult.MatchingErrors > 0)
        {
            _logger.LogWarning("ESPN historical data job {JobId} completed with errors. " +
                "Data errors: {DataErrors}, Matching errors: {MatchingErrors}",
                jobId, syncResult.DataErrors, syncResult.MatchingErrors);

            // Log specific errors if available
            if (syncResult.Errors.Any())
            {
                foreach (var error in syncResult.Errors.Take(15)) // More errors for historical debugging
                {
                    _logger.LogWarning("Sync error: {Error}", error);
                }

                if (syncResult.Errors.Count > 15)
                {
                    _logger.LogWarning("... and {AdditionalErrors} more errors", syncResult.Errors.Count - 15);
                }
            }
        }

        if (syncResult.Warnings.Any())
        {
            _logger.LogInformation("ESPN historical data job {JobId} completed with {WarningCount} warnings",
                jobId, syncResult.Warnings.Count);

            // Log specific warnings if available
            foreach (var warning in syncResult.Warnings.Take(10)) // More warnings for historical
            {
                _logger.LogInformation("Sync warning: {Warning}", warning);
            }
        }
    }

    /// <summary>
    /// Checks if the job can execute (prevents overlapping executions)
    /// </summary>
    private bool CanExecute(string jobId)
    {
        lock (_lockObject)
        {
            var timeoutConfig = _configuration["Job:HistoricalTimeoutMinutes"];
            var timeoutMinutes = int.TryParse(timeoutConfig, out var timeout) ? timeout : 480; // 8 hours for historical

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
    private async Task RecordJobMetricsAsync(string jobId, bool success, TimeSpan duration, SyncResult? syncResult,
        string syncType, string? errorMessage, CancellationToken cancellationToken)
    {
        try
        {
            var metrics = new
            {
                JobId = jobId,
                JobType = "HistoricalDataSync",
                SyncType = syncType,
                Success = success,
                Duration = duration.TotalMilliseconds,
                Timestamp = DateTime.UtcNow,
                ErrorMessage = errorMessage,
                SyncMetrics = syncResult != null ? new
                {
                    PlayersProcessed = syncResult.PlayersProcessed,
                    PlayersUpdated = syncResult.PlayersUpdated,
                    NewPlayersAdded = syncResult.NewPlayersAdded,
                    StatsRecordsProcessed = syncResult.StatsRecordsProcessed,
                    NewStatsAdded = syncResult.NewStatsAdded,
                    DataErrors = syncResult.DataErrors,
                    MatchingErrors = syncResult.MatchingErrors,
                    WarningCount = syncResult.Warnings.Count
                } : null
            };

            var fileName = $"historical_sync_metrics_{DateTime.UtcNow:yyyyMMdd}.json";
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