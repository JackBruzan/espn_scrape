using ESPNScrape.Models.DataSync;
using ESPNScrape.Services.Interfaces;
using Quartz;
using System.Text.Json;

namespace ESPNScrape.Jobs;

/// <summary>
/// Quartz job for scheduled synchronization of ESPN player roster data
/// Runs daily to keep player information up-to-date
/// </summary>
[DisallowConcurrentExecution]
public class EspnPlayerSyncJob : IJob
{
    private readonly ILogger<EspnPlayerSyncJob> _logger;
    private readonly IEspnDataSyncService _dataSyncService;
    private readonly IConfiguration _configuration;

    // Job execution tracking
    private static readonly Dictionary<string, DateTime> _lastExecutionTimes = new();
    private static readonly object _lockObject = new();

    public EspnPlayerSyncJob(
        ILogger<EspnPlayerSyncJob> logger,
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
            _logger.LogInformation("Starting ESPN player sync job {JobId} at {StartTime}", jobId, startTime);

            // Check if we're in NFL season
            if (!IsNflSeason())
            {
                _logger.LogInformation("ESPN player sync job {JobId} skipped - currently off-season", jobId);
                return;
            }

            // Prevent overlapping executions
            if (!CanExecute(jobId))
            {
                _logger.LogWarning("ESPN player sync job {JobId} skipped - previous execution still running", jobId);
                return;
            }

            // Mark execution start
            MarkExecutionStart(jobId, startTime);

            // Check if sync is already running
            if (await _dataSyncService.IsSyncRunningAsync(context.CancellationToken))
            {
                _logger.LogWarning("ESPN player sync job {JobId} skipped - another sync operation is already running", jobId);
                return;
            }

            // Configure sync options
            var syncOptions = GetSyncOptions();

            _logger.LogInformation("Starting player roster synchronization with options: {@SyncOptions}", syncOptions);

            // Perform player synchronization
            var syncResult = await _dataSyncService.SyncPlayersAsync(syncOptions, context.CancellationToken);

            // Log sync results
            LogSyncResults(jobId, syncResult);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("ESPN player sync job {JobId} completed successfully in {Duration}ms. " +
                "Processed: {PlayersProcessed}, Updated: {PlayersUpdated}, New: {NewPlayersAdded}, Errors: {DataErrors}",
                jobId, duration.TotalMilliseconds, syncResult.PlayersProcessed, syncResult.PlayersUpdated,
                syncResult.NewPlayersAdded, syncResult.DataErrors);

            // Record success metrics
            await RecordJobMetricsAsync(jobId, true, duration, syncResult, null, context.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ESPN player sync job {JobId} was cancelled", jobId);
            throw;
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "ESPN player sync job {JobId} failed after {Duration}ms", jobId, duration.TotalMilliseconds);

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
    /// Gets sync options from configuration
    /// </summary>
    private SyncOptions GetSyncOptions()
    {
        var options = new SyncOptions();

        // Configure from appsettings or use defaults
        var config = _configuration.GetSection("DataSync:PlayerSync");

        options.ForceFullSync = config.GetValue("ForceFullSync", false);
        options.SkipInactives = config.GetValue("SkipInactives", true);
        options.BatchSize = config.GetValue("BatchSize", 100);
        options.DryRun = config.GetValue("DryRun", false);
        options.MaxRetries = config.GetValue("MaxRetries", 3);
        options.RetryDelayMs = config.GetValue("RetryDelayMs", 1000);

        return options;
    }

    /// <summary>
    /// Logs detailed sync results for monitoring and debugging
    /// </summary>
    private void LogSyncResults(string jobId, SyncResult syncResult)
    {
        if (syncResult.DataErrors > 0 || syncResult.MatchingErrors > 0)
        {
            _logger.LogWarning("ESPN player sync job {JobId} completed with errors. " +
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
            _logger.LogInformation("ESPN player sync job {JobId} completed with {WarningCount} warnings",
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
            var timeoutMinutes = int.TryParse(timeoutConfig, out var timeout) ? timeout : 60; // Player sync can take longer

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
                JobType = "PlayerSync",
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

            var fileName = $"player_sync_metrics_{DateTime.UtcNow:yyyyMMdd}.json";
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