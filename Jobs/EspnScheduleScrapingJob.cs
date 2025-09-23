using ESPNScrape.Services.Interfaces;
using Quartz;

namespace ESPNScrape.Jobs;

/// <summary>
/// Quartz job for scheduled collection of NFL schedule data from ESPN
/// Runs every Tuesday morning to scrape the current week's schedule
/// </summary>
[DisallowConcurrentExecution]
public class EspnScheduleScrapingJob : IJob
{
    private readonly ILogger<EspnScheduleScrapingJob> _logger;
    private readonly IEspnScheduleService _scheduleService;
    private readonly IConfiguration _configuration;

    // Job execution tracking
    private static readonly Dictionary<string, DateTime> _lastExecutionTimes = new();
    private static readonly object _lockObject = new();

    public EspnScheduleScrapingJob(
        ILogger<EspnScheduleScrapingJob> logger,
        IEspnScheduleService scheduleService,
        IConfiguration configuration)
    {
        _logger = logger;
        _scheduleService = scheduleService;
        _configuration = configuration;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobId = context.FireInstanceId;
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Starting ESPN schedule scraping job {JobId} at {StartTime}", jobId, startTime);

            // Check if we're in NFL season (September to February)
            if (!IsNflSeason())
            {
                _logger.LogInformation("ESPN schedule scraping job {JobId} skipped - currently off-season", jobId);
                return;
            }

            // Get merged job data map (includes both job detail data and trigger data)
            var mergedJobData = context.MergedJobDataMap;

            // Check for manual parameters in merged job data
            var isManualTrigger = mergedJobData.ContainsKey("Year") ||
                                 mergedJobData.ContainsKey("Week");

            // Check if this job has already run recently (skip check for manual triggers)
            if (!isManualTrigger)
            {
                lock (_lockObject)
                {
                    if (_lastExecutionTimes.TryGetValue(nameof(EspnScheduleScrapingJob), out var lastExecution))
                    {
                        var timeSinceLastRun = DateTime.UtcNow - lastExecution;
                        if (timeSinceLastRun.TotalHours < 12) // Prevent running more than twice a day
                        {
                            _logger.LogInformation("ESPN schedule scraping job {JobId} skipped - last run was {TimeSinceLastRun} ago",
                                jobId, timeSinceLastRun);
                            return;
                        }
                    }
                }
            }

            // Get current NFL week or use manual parameters
            int currentYear, currentWeek, seasonType;

            if (isManualTrigger)
            {
                currentYear = mergedJobData.GetInt("Year");
                currentWeek = mergedJobData.GetInt("Week");
                seasonType = mergedJobData.GetInt("SeasonType");

                // Use current values if not provided
                if (currentYear == 0 || currentWeek == 0)
                {
                    var (autoYear, autoWeek) = GetCurrentNflWeek();
                    currentYear = currentYear > 0 ? currentYear : autoYear;
                    currentWeek = currentWeek > 0 ? currentWeek : autoWeek;
                }
                seasonType = seasonType > 0 ? seasonType : 2; // Default to regular season

                _logger.LogInformation("Manual schedule scraping triggered for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}",
                    currentYear, currentWeek, seasonType);
            }
            else
            {
                var (autoYear, autoWeek) = GetCurrentNflWeek();
                currentYear = autoYear;
                currentWeek = autoWeek;
                seasonType = 2; // Regular season

                _logger.LogInformation("Scheduled scraping for current week - Year: {Year}, Week: {Week}", currentYear, currentWeek);
            }

            // Scrape current week's schedule using ESPN Core API
            var schedules = await _scheduleService.GetWeeklyScheduleFromApiAsync(currentYear, currentWeek, seasonType, context.CancellationToken);

            if (schedules.Any())
            {
                _logger.LogInformation("Found {Count} games for Week {Week}, {Year}", schedules.Count(), currentWeek, currentYear);

                // Save to database
                await _scheduleService.SaveScheduleDataAsync(schedules, context.CancellationToken);

                _logger.LogInformation("Successfully saved schedule data for Week {Week}, {Year}, SeasonType {SeasonType}",
                    currentWeek, currentYear, seasonType);
            }
            else
            {
                _logger.LogWarning("No schedule data found for Week {Week}, {Year}, SeasonType {SeasonType}",
                    currentWeek, currentYear, seasonType);
            }

            // Update execution tracking
            lock (_lockObject)
            {
                _lastExecutionTimes[nameof(EspnScheduleScrapingJob)] = startTime;
            }

            var executionTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("ESPN schedule scraping job {JobId} completed successfully in {ExecutionTime}ms",
                jobId, executionTime.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("ESPN schedule scraping job {JobId} was cancelled", jobId);
        }
        catch (Exception ex)
        {
            var executionTime = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "ESPN schedule scraping job {JobId} failed after {ExecutionTime}ms: {Error}",
                jobId, executionTime.TotalMilliseconds, ex.Message);

            // Re-throw to let Quartz handle the failure
            throw;
        }
    }

    /// <summary>
    /// Determines if we're currently in NFL season (September through February)
    /// </summary>
    private bool IsNflSeason()
    {
        var now = DateTime.UtcNow;
        var month = now.Month;

        // NFL season runs from September through February (next year)
        return month >= 9 || month <= 2;
    }

    /// <summary>
    /// Gets the current NFL year and week based on the current date
    /// </summary>
    private (int year, int week) GetCurrentNflWeek()
    {
        var now = DateTime.UtcNow;
        var currentYear = now.Year;

        // If we're in January or February, we're still in the previous year's season
        if (now.Month <= 2)
        {
            currentYear = now.Year - 1;
        }

        // Calculate week based on season start (first Thursday in September)
        var seasonStart = GetSeasonStartDate(currentYear);
        var daysSinceStart = (now - seasonStart).Days;
        var week = Math.Max(1, (daysSinceStart / 7) + 1);

        // Cap at week 18 for regular season
        week = Math.Min(week, 18);

        return (currentYear, week);
    }

    /// <summary>
    /// Gets the start date of the NFL season (first Thursday in September)
    /// </summary>
    private DateTime GetSeasonStartDate(int year)
    {
        var firstOfSeptember = new DateTime(year, 9, 1);

        // Find first Thursday of September
        while (firstOfSeptember.DayOfWeek != DayOfWeek.Thursday)
        {
            firstOfSeptember = firstOfSeptember.AddDays(1);
        }

        return firstOfSeptember;
    }
}