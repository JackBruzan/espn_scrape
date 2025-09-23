using ESPNScrape.Models.DataSync;
using ESPNScrape.Models.PlayerMatching;
using ESPNScrape.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Quartz;
using System.Text.Json;

namespace ESPNScrape.Controllers;

/// <summary>
/// Controller for managing ESPN integration jobs and monitoring their status
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EspnIntegrationController : ControllerBase
{
    private readonly ILogger<EspnIntegrationController> _logger;
    private readonly IEspnDataSyncService _dataSyncService;
    private readonly IEspnPlayerMatchingService _playerMatchingService;
    private readonly ISchedulerFactory _schedulerFactory;
    private readonly IConfiguration _configuration;
    private readonly IEspnScheduleService _scheduleService;

    public EspnIntegrationController(
        ILogger<EspnIntegrationController> logger,
        IEspnDataSyncService dataSyncService,
        IEspnPlayerMatchingService playerMatchingService,
        ISchedulerFactory schedulerFactory,
        IConfiguration configuration,
        IEspnScheduleService scheduleService)
    {
        _logger = logger;
        _dataSyncService = dataSyncService;
        _playerMatchingService = playerMatchingService;
        _schedulerFactory = schedulerFactory;
        _configuration = configuration;
        _scheduleService = scheduleService;
    }

    /// <summary>
    /// Trigger manual player synchronization
    /// </summary>
    /// <param name="options">Sync configuration options</param>
    /// <returns>Sync result</returns>
    [HttpPost("sync/players")]
    public async Task<ActionResult<SyncResult>> TriggerPlayerSync([FromBody] SyncOptions? options = null)
    {
        try
        {
            _logger.LogInformation("Manual player sync triggered via API");

            // Check if sync is already running
            if (await _dataSyncService.IsSyncRunningAsync())
            {
                return Conflict(new { message = "A sync operation is already running" });
            }

            var result = await _dataSyncService.SyncPlayersAsync(options);

            _logger.LogInformation("Manual player sync completed. Processed: {PlayersProcessed}, Updated: {PlayersUpdated}, Errors: {DataErrors}",
                result.PlayersProcessed, result.PlayersUpdated, result.DataErrors);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute manual player sync");
            return StatusCode(500, new { message = "Failed to execute player sync", error = ex.Message });
        }
    }

    /// <summary>
    /// Trigger manual stats synchronization
    /// </summary>
    /// <param name="request">Stats sync request parameters</param>
    /// <returns>Sync result</returns>
    [HttpPost("sync/stats")]
    public async Task<ActionResult<SyncResult>> TriggerStatsSync([FromBody] StatsSyncRequest request)
    {
        try
        {
            _logger.LogInformation("Manual stats sync triggered via API for Season {Season}, Week {Week}",
                request.Season, request.Week);

            // Check if sync is already running
            if (await _dataSyncService.IsSyncRunningAsync())
            {
                return Conflict(new { message = "A sync operation is already running" });
            }

            var result = await _dataSyncService.SyncPlayerStatsAsync(request.Season, request.Week, request.Options);

            _logger.LogInformation("Manual stats sync completed for Season {Season}, Week {Week}. " +
                "Processed: {PlayersProcessed}, Updated: {PlayersUpdated}, Errors: {DataErrors}",
                request.Season, request.Week, result.PlayersProcessed, result.PlayersUpdated, result.DataErrors);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute manual stats sync for Season {Season}, Week {Week}",
                request.Season, request.Week);
            return StatusCode(500, new { message = "Failed to execute stats sync", error = ex.Message });
        }
    }

    /// <summary>
    /// Trigger historical data synchronization
    /// </summary>
    /// <param name="request">Historical sync request parameters</param>
    /// <returns>Job execution result</returns>
    [HttpPost("sync/historical")]
    public async Task<ActionResult<JobExecutionResult>> TriggerHistoricalSync([FromBody] HistoricalSyncRequest request)
    {
        try
        {
            _logger.LogInformation("Manual historical sync triggered via API: {SyncType}, Season {Season}",
                request.SyncType, request.Season);

            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey("EspnHistoricalDataJob");

            // Create job data map with parameters
            var jobDataMap = new JobDataMap
            {
                ["SyncType"] = request.SyncType,
                ["Season"] = request.Season,
                ["StartWeek"] = request.StartWeek ?? 0,
                ["EndWeek"] = request.EndWeek ?? 0,
                ["StartDate"] = request.StartDate ?? string.Empty,
                ["EndDate"] = request.EndDate ?? string.Empty
            };

            // Trigger the job manually
            await scheduler.TriggerJob(jobKey, jobDataMap);

            var result = new JobExecutionResult
            {
                JobKey = jobKey.ToString(),
                TriggeredAt = DateTime.UtcNow,
                Message = $"Historical sync job triggered successfully with type: {request.SyncType}"
            };

            _logger.LogInformation("Historical sync job triggered successfully");

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger historical sync job");
            return StatusCode(500, new { message = "Failed to trigger historical sync", error = ex.Message });
        }
    }

    /// <summary>
    /// Get current sync status
    /// </summary>
    /// <returns>Sync status information</returns>
    [HttpGet("sync/status")]
    public async Task<ActionResult<SyncStatus>> GetSyncStatus()
    {
        try
        {
            var isRunning = await _dataSyncService.IsSyncRunningAsync();
            var lastReport = await _dataSyncService.GetLastSyncReportAsync();

            var status = new SyncStatusResponse
            {
                IsRunning = isRunning,
                LastSyncReport = lastReport,
                LastPlayerSync = await GetLastJobExecution("EspnPlayerSyncJob"),
                LastStatsSync = await GetLastJobExecution("EspnStatsSyncJob"),
                LastHistoricalSync = await GetLastJobExecution("EspnHistoricalDataJob")
            };

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sync status");
            return StatusCode(500, new { message = "Failed to get sync status", error = ex.Message });
        }
    }

    /// <summary>
    /// Get sync history and reports
    /// </summary>
    /// <param name="limit">Maximum number of reports to return</param>
    /// <param name="syncType">Filter by sync type</param>
    /// <returns>List of sync reports</returns>
    [HttpGet("reports/sync-history")]
    public async Task<ActionResult<List<SyncReport>>> GetSyncHistory(
        [FromQuery] int limit = 50,
        [FromQuery] SyncType? syncType = null)
    {
        try
        {
            var reports = await _dataSyncService.GetSyncHistoryAsync(limit, syncType);
            return Ok(reports);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get sync history");
            return StatusCode(500, new { message = "Failed to get sync history", error = ex.Message });
        }
    }

    /// <summary>
    /// Get job execution history and metrics
    /// </summary>
    /// <param name="jobName">Job name to get metrics for</param>
    /// <param name="days">Number of days to look back</param>
    /// <returns>Job metrics</returns>
    [HttpGet("jobs/{jobName}/metrics")]
    public async Task<ActionResult<JobMetrics>> GetJobMetrics(string jobName, [FromQuery] int days = 7)
    {
        try
        {
            var metrics = await GetJobMetricsFromFiles(jobName, days);
            return Ok(metrics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get job metrics for {JobName}", jobName);
            return StatusCode(500, new { message = "Failed to get job metrics", error = ex.Message });
        }
    }

    /// <summary>
    /// Get all scheduled jobs and their status
    /// </summary>
    /// <returns>List of job information</returns>
    [HttpGet("jobs")]
    public async Task<ActionResult<List<JobInfo>>> GetScheduledJobs()
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup());

            var jobInfos = new List<JobInfo>();

            foreach (var jobKey in jobKeys)
            {
                var jobDetail = await scheduler.GetJobDetail(jobKey);
                var triggers = await scheduler.GetTriggersOfJob(jobKey);

                if (jobDetail != null)
                {
                    var triggerInfos = new List<TriggerInfo>();
                    foreach (var trigger in triggers)
                    {
                        var triggerState = await scheduler.GetTriggerState(trigger.Key);
                        triggerInfos.Add(new TriggerInfo
                        {
                            TriggerKey = trigger.Key.ToString(),
                            TriggerName = trigger.Key.Name,
                            TriggerGroup = trigger.Key.Group,
                            NextFireTime = trigger.GetNextFireTimeUtc()?.DateTime,
                            PreviousFireTime = trigger.GetPreviousFireTimeUtc()?.DateTime,
                            TriggerState = triggerState.ToString()
                        });
                    }

                    var jobInfo = new JobInfo
                    {
                        JobKey = jobKey.ToString(),
                        JobName = jobKey.Name,
                        JobGroup = jobKey.Group,
                        JobType = jobDetail.JobType.Name,
                        Description = jobDetail.Description,
                        IsDurable = jobDetail.Durable,
                        Triggers = triggerInfos
                    };

                    jobInfos.Add(jobInfo);
                }
            }

            return Ok(jobInfos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get scheduled jobs");
            return StatusCode(500, new { message = "Failed to get scheduled jobs", error = ex.Message });
        }
    }

    /// <summary>
    /// Get list of ESPN players that couldn't be matched automatically
    /// </summary>
    /// <returns>List of unmatched players requiring manual review</returns>
    [HttpGet("players/unmatched")]
    public async Task<ActionResult<List<UnmatchedPlayer>>> GetUnmatchedPlayers()
    {
        try
        {
            _logger.LogInformation("Retrieving unmatched players via API");

            var unmatchedPlayers = await _playerMatchingService.GetUnmatchedPlayersAsync();

            _logger.LogInformation("Retrieved {Count} unmatched players", unmatchedPlayers.Count);

            return Ok(unmatchedPlayers);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve unmatched players");
            return StatusCode(500, new { message = "Failed to retrieve unmatched players", error = ex.Message });
        }
    }

    /// <summary>
    /// Manually link an ESPN player to a database player
    /// </summary>
    /// <param name="request">Player link request with database player ID and ESPN player ID</param>
    /// <returns>Link operation result</returns>
    [HttpPost("players/link")]
    public async Task<ActionResult<PlayerLinkResult>> LinkPlayer([FromBody] PlayerLinkRequest request)
    {
        try
        {
            _logger.LogInformation("Manual player link requested: Database Player {DatabasePlayerId} -> ESPN Player {EspnPlayerId}",
                request.DatabasePlayerId, request.EspnPlayerId);

            var success = await _playerMatchingService.LinkPlayerAsync(request.DatabasePlayerId, request.EspnPlayerId);

            var result = new PlayerLinkResult
            {
                Success = success,
                DatabasePlayerId = request.DatabasePlayerId,
                EspnPlayerId = request.EspnPlayerId,
                Message = success ? "Player linked successfully" : "Failed to link player",
                LinkedAt = DateTime.UtcNow
            };

            if (success)
            {
                _logger.LogInformation("Successfully linked Database Player {DatabasePlayerId} to ESPN Player {EspnPlayerId}",
                    request.DatabasePlayerId, request.EspnPlayerId);
                return Ok(result);
            }
            else
            {
                _logger.LogWarning("Failed to link Database Player {DatabasePlayerId} to ESPN Player {EspnPlayerId}",
                    request.DatabasePlayerId, request.EspnPlayerId);
                return BadRequest(result);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to link Database Player {DatabasePlayerId} to ESPN Player {EspnPlayerId}",
                request.DatabasePlayerId, request.EspnPlayerId);
            return StatusCode(500, new { message = "Failed to link player", error = ex.Message });
        }
    }

    /// <summary>
    /// Get player matching statistics and performance metrics
    /// </summary>
    /// <returns>Matching statistics</returns>
    [HttpGet("players/matching-stats")]
    public async Task<ActionResult<MatchingStatistics>> GetMatchingStatistics()
    {
        try
        {
            _logger.LogInformation("Retrieving player matching statistics via API");

            var statistics = await _playerMatchingService.GetMatchingStatisticsAsync();

            _logger.LogInformation("Retrieved matching statistics: {TotalPlayers} total, {Successful} successful matches",
                statistics.TotalEspnPlayers, statistics.SuccessfulMatches);

            return Ok(statistics);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve matching statistics");
            return StatusCode(500, new { message = "Failed to retrieve matching statistics", error = ex.Message });
        }
    }

    /// <summary>
    /// Cancel any running sync operations
    /// </summary>
    /// <returns>Cancellation result</returns>
    [HttpPost("sync/cancel")]
    public async Task<ActionResult<CancellationResult>> CancelRunningSync()
    {
        try
        {
            var cancelled = await _dataSyncService.CancelRunningSyncAsync();

            var result = new CancellationResult
            {
                Success = cancelled,
                Message = cancelled ? "Sync operation cancelled successfully" : "No running sync operation found",
                CancelledAt = DateTime.UtcNow
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel running sync");
            return StatusCode(500, new { message = "Failed to cancel sync", error = ex.Message });
        }
    }

    /// <summary>
    /// Get the last execution time for a specific job
    /// </summary>
    private async Task<DateTime?> GetLastJobExecution(string jobName)
    {
        try
        {
            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey(jobName);
            var triggers = await scheduler.GetTriggersOfJob(jobKey);

            return triggers?.Max(t => t.GetPreviousFireTimeUtc())?.DateTime;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get job metrics from stored files
    /// </summary>
    private async Task<JobMetrics> GetJobMetricsFromFiles(string jobName, int days)
    {
        var metrics = new JobMetrics
        {
            JobName = jobName,
            PeriodDays = days,
            Executions = new List<JobExecution>()
        };

        try
        {
            var dataDirectory = _configuration["DataStorage:Directory"] ?? "data";
            var metricsDirectory = Path.Combine(dataDirectory, "metrics");

            if (!Directory.Exists(metricsDirectory))
            {
                return metrics;
            }

            var cutoffDate = DateTime.UtcNow.AddDays(-days);
            var pattern = jobName.ToLowerInvariant() switch
            {
                "espnplayersyncjob" => "*player_sync_metrics_*.json",
                "espnstatssyncjob" => "*stats_sync_metrics_*.json",
                "espnhistoricaldatajob" => "*historical_sync_metrics_*.json",
                _ => "*metrics_*.json"
            };

            var files = Directory.GetFiles(metricsDirectory, pattern)
                .Where(f => System.IO.File.GetCreationTime(f) >= cutoffDate)
                .OrderByDescending(f => System.IO.File.GetCreationTime(f));

            foreach (var file in files)
            {
                try
                {
                    var lines = await System.IO.File.ReadAllLinesAsync(file);
                    foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                    {
                        var execution = JsonSerializer.Deserialize<JobExecution>(line, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });

                        if (execution != null && execution.Timestamp >= cutoffDate)
                        {
                            metrics.Executions.Add(execution);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse metrics file: {File}", file);
                }
            }

            // Calculate summary statistics
            metrics.TotalExecutions = metrics.Executions.Count;
            metrics.SuccessfulExecutions = metrics.Executions.Count(e => e.Success);
            metrics.FailedExecutions = metrics.Executions.Count(e => !e.Success);
            metrics.AverageDuration = metrics.Executions.Any()
                ? TimeSpan.FromMilliseconds(metrics.Executions.Average(e => e.Duration))
                : TimeSpan.Zero;
            metrics.LastExecution = metrics.Executions.MaxBy(e => e.Timestamp)?.Timestamp;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load job metrics for {JobName}", jobName);
        }

        return metrics;
    }

    /// <summary>
    /// Trigger manual schedule scraping for current or specific week
    /// </summary>
    /// <param name="request">Schedule scraping parameters</param>
    /// <returns>Job execution result</returns>
    [HttpPost("sync/schedule")]
    public async Task<ActionResult<JobExecutionResult>> TriggerScheduleSync([FromBody] ScheduleSyncRequest? request = null)
    {
        try
        {
            _logger.LogInformation("Manual schedule sync triggered via API");

            var scheduler = await _schedulerFactory.GetScheduler();
            var jobKey = new JobKey("EspnScheduleScrapingJob");

            // Create job data map with parameters
            var jobDataMap = new JobDataMap();

            if (request != null)
            {
                if (request.Year > 0) jobDataMap["Year"] = request.Year;
                if (request.Week > 0) jobDataMap["Week"] = request.Week;
                if (request.SeasonType > 0) jobDataMap["SeasonType"] = request.SeasonType;
            }

            // Trigger the job manually
            await scheduler.TriggerJob(jobKey, jobDataMap);

            var result = new JobExecutionResult
            {
                JobKey = jobKey.ToString(),
                TriggeredAt = DateTime.UtcNow,
                Message = $"Schedule scraping job triggered successfully" +
                         (request?.Year > 0 ? $" for Year {request.Year}, Week {request.Week}" : " for current week")
            };

            _logger.LogInformation("Schedule scraping job triggered successfully");

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to trigger schedule scraping job");
            return StatusCode(500, new { message = "Failed to trigger schedule scraping", error = ex.Message });
        }
    }

    /// <summary>
    /// Test the new ESPN Core API schedule retrieval directly
    /// </summary>
    /// <param name="year">Year (defaults to current year)</param>
    /// <param name="week">Week (defaults to 1)</param>
    /// <param name="seasonType">Season type (defaults to 2 for regular season)</param>
    /// <returns>Schedule data from ESPN Core API</returns>
    [HttpGet("test/schedule-api")]
    public async Task<ActionResult<object>> TestScheduleApi(
        [FromQuery] int? year = null,
        [FromQuery] int? week = null,
        [FromQuery] int? seasonType = null)
    {
        try
        {
            var currentYear = year ?? DateTime.Now.Year;
            var currentWeek = week ?? 1;
            var currentSeasonType = seasonType ?? 2;

            _logger.LogInformation("Testing ESPN Core API schedule retrieval for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}",
                currentYear, currentWeek, currentSeasonType);

            var schedules = await _scheduleService.GetWeeklyScheduleFromApiAsync(currentYear, currentWeek, currentSeasonType);

            var result = new
            {
                Success = true,
                Year = currentYear,
                Week = currentWeek,
                SeasonType = currentSeasonType,
                ScheduleCount = schedules.Count(),
                Schedules = schedules.Take(3).Select(s => new
                {
                    s.GameTime,
                    s.HomeTeamName,
                    s.AwayTeamName,
                    s.EspnCompetitionId,
                    s.HomeMoneyline,
                    s.AwayMoneyline,
                    s.BettingProvider
                })
            };

            _logger.LogInformation("Successfully retrieved {Count} games via ESPN Core API", schedules.Count());
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test ESPN Core API schedule retrieval");
            return StatusCode(500, new { success = false, message = "Failed to retrieve schedule via ESPN Core API", error = ex.Message });
        }
    }
}

// DTOs for API responses

public class StatsSyncRequest
{
    public int Season { get; set; }
    public int Week { get; set; }
    public SyncOptions? Options { get; set; }
}

public class HistoricalSyncRequest
{
    public string SyncType { get; set; } = "Full";
    public int Season { get; set; }
    public int? StartWeek { get; set; }
    public int? EndWeek { get; set; }
    public string? StartDate { get; set; }
    public string? EndDate { get; set; }
}

public class ScheduleSyncRequest
{
    public int Year { get; set; }
    public int Week { get; set; }
    public int SeasonType { get; set; } = 2; // Default to regular season
}

public class JobExecutionResult
{
    public string JobKey { get; set; } = string.Empty;
    public DateTime TriggeredAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class SyncStatusResponse
{
    public bool IsRunning { get; set; }
    public SyncReport? LastSyncReport { get; set; }
    public DateTime? LastPlayerSync { get; set; }
    public DateTime? LastStatsSync { get; set; }
    public DateTime? LastHistoricalSync { get; set; }
}

public class JobInfo
{
    public string JobKey { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string JobGroup { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDurable { get; set; }
    public List<TriggerInfo> Triggers { get; set; } = new();
}

public class TriggerInfo
{
    public string TriggerKey { get; set; } = string.Empty;
    public string TriggerName { get; set; } = string.Empty;
    public string TriggerGroup { get; set; } = string.Empty;
    public DateTime? NextFireTime { get; set; }
    public DateTime? PreviousFireTime { get; set; }
    public string TriggerState { get; set; } = string.Empty;
}

public class CancellationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CancelledAt { get; set; }
}

public class JobMetrics
{
    public string JobName { get; set; } = string.Empty;
    public int PeriodDays { get; set; }
    public int TotalExecutions { get; set; }
    public int SuccessfulExecutions { get; set; }
    public int FailedExecutions { get; set; }
    public TimeSpan AverageDuration { get; set; }
    public DateTime? LastExecution { get; set; }
    public List<JobExecution> Executions { get; set; } = new();
}

public class JobExecution
{
    public string JobId { get; set; } = string.Empty;
    public string JobType { get; set; } = string.Empty;
    public bool Success { get; set; }
    public double Duration { get; set; }
    public DateTime Timestamp { get; set; }
    public string? ErrorMessage { get; set; }
    public object? SyncMetrics { get; set; }
}

public class PlayerLinkRequest
{
    public long DatabasePlayerId { get; set; }
    public string EspnPlayerId { get; set; } = string.Empty;
}

public class PlayerLinkResult
{
    public bool Success { get; set; }
    public long DatabasePlayerId { get; set; }
    public string EspnPlayerId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; }
}