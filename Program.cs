using ESPNScrape.Jobs;
using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.HealthChecks;
using ESPNScrape.Configuration;
using ESPNScrape.Models.PlayerMatching;
using ESPNScrape.Models.DataSync;
using Quartz;
using Serilog;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Http;
using Microsoft.AspNetCore.Mvc;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/espn-scrape-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting ESPN Scrape Service");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Configure cache settings
    builder.Services.Configure<CacheConfiguration>(builder.Configuration.GetSection("Cache"));

    // Configure resilience settings
    builder.Services.Configure<ResilienceConfiguration>(builder.Configuration.GetSection("Resilience"));

    // Configure bulk operations settings
    builder.Services.Configure<BulkOperationsConfiguration>(builder.Configuration.GetSection("BulkOperations"));

    // Configure logging settings
    builder.Services.Configure<LoggingConfiguration>(builder.Configuration.GetSection("Logging"));

    // Configure player matching settings
    builder.Services.Configure<PlayerMatchingOptions>(builder.Configuration.GetSection("PlayerMatching"));

    // Configure data sync settings
    builder.Services.Configure<SyncOptions>(builder.Configuration.GetSection("DataSync"));

    // Register services
    builder.Services.AddSingleton<IImageDownloadService, ImageDownloadService>();
    builder.Services.AddSingleton<IESPNScrapingService, ESPNScrapingService>();

    // Register rate limiting service
    builder.Services.AddSingleton<IEspnRateLimitService, EspnRateLimitService>();

    // Add HTTP client for ESPN service with enhanced resilience
    builder.Services.AddHttpClient<IEspnHttpService, EspnHttpService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30); // Default timeout, will be overridden by resilience config
    });

    // Register ESPN Scoreboard Service
    builder.Services.AddScoped<IEspnScoreboardService, EspnScoreboardService>();

    // Add memory cache for response caching
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1000; // Max number of cache entries
    });

    // Register ESPN Cache Service
    builder.Services.AddSingleton<IEspnCacheService, EspnCacheService>();

    // Register ESPN Player Statistics Service
    builder.Services.AddScoped<IEspnPlayerStatsService, EspnPlayerStatsService>();

    // Register ESPN Box Score Service
    builder.Services.AddScoped<IEspnBoxScoreService, EspnBoxScoreService>();

    // Register ESPN Bulk Operations Service
    builder.Services.AddScoped<IEspnBulkOperationsService, EspnBulkOperationsService>();

    // Register ESPN Player Matching Service
    builder.Services.AddScoped<IEspnPlayerMatchingService, EspnPlayerMatchingService>();

    // Register ESPN Stats Transformation Service
    builder.Services.AddScoped<IEspnStatsTransformationService, EspnStatsTransformationService>();

    // Register ESPN Data Sync Service
    builder.Services.AddScoped<IEspnDataSyncService, EspnDataSyncService>();

    // Register logging and monitoring services
    builder.Services.AddSingleton<IEspnLoggingService, EspnLoggingService>();
    builder.Services.AddSingleton<IEspnMetricsService, EspnMetricsService>();
    builder.Services.AddSingleton<IEspnAlertingService, EspnAlertingService>();

    // Register Main ESPN API Service
    builder.Services.AddScoped<IEspnApiService, EspnApiService>();

    // Add ASP.NET Core services for diagnostic endpoints
    builder.Services.AddControllers();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<EspnApiHealthCheck>("espn_api", tags: new[] { "espn", "api" });

    // Add alert monitoring background service
    builder.Services.AddHostedService<AlertMonitoringService>();

    // Add Quartz
    builder.Services.AddQuartz(q =>
    {
        // Create a "key" for the ESPN Image scraping job (legacy)
        var imageJobKey = new JobKey("ESPNImageScrapingJob");

        // Register the image scraping job with the DI container
        q.AddJob<ESPNImageScrapingJob>(opts => opts
            .WithIdentity(imageJobKey)
            .DisallowConcurrentExecution()); // Prevent concurrent execution

        // Create a trigger for the image scraping job (disabled by default)
        q.AddTrigger(opts => opts
            .ForJob(imageJobKey)
            .WithIdentity("ESPNImageScrapingJob-trigger")
            .WithSimpleSchedule(x => x
                .WithIntervalInHours(24) // Run daily instead of every 5 seconds
                .RepeatForever())
            .StartAt(DateTimeOffset.UtcNow.AddHours(1))); // Start in 1 hour

        // Create a "key" for the new ESPN API scraping job
        var apiJobKey = new JobKey("EspnApiScrapingJob");

        // Register the ESPN API scraping job with the DI container
        q.AddJob<EspnApiScrapingJob>(opts => opts
            .WithIdentity(apiJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably()); // Allow job to exist without triggers during off-season

        // Create a trigger for the ESPN API scraping job
        // During NFL season: Run twice per week (Wednesday and Sunday evenings)
        // Wednesday: Collect mid-week data and prepare for upcoming games
        // Sunday: Collect completed weekend games
        q.AddTrigger(opts => opts
            .ForJob(apiJobKey)
            .WithIdentity("EspnApiScrapingJob-wednesday-trigger")
            .WithCronSchedule("0 0 21 ? * WED *") // Every Wednesday at 9 PM UTC
            .WithDescription("ESPN API data collection - Wednesday"));

        q.AddTrigger(opts => opts
            .ForJob(apiJobKey)
            .WithIdentity("EspnApiScrapingJob-sunday-trigger")
            .WithCronSchedule("0 0 23 ? * SUN *") // Every Sunday at 11 PM UTC
            .WithDescription("ESPN API data collection - Sunday"));

        // Optional: Add a manual trigger for testing (runs every 30 minutes during development)
        if (builder.Environment.IsDevelopment())
        {
            q.AddTrigger(opts => opts
                .ForJob(apiJobKey)
                .WithIdentity("EspnApiScrapingJob-dev-trigger")
                .WithSimpleSchedule(x => x
                    .WithIntervalInMinutes(30)
                    .RepeatForever())
                .WithDescription("ESPN API data collection - Development"));
        }

        // ===== TICKET-006: ESPN Integration Scheduled Jobs =====

        // Create a "key" for the ESPN Player Sync job
        var playerSyncJobKey = new JobKey("EspnPlayerSyncJob");

        // Register the ESPN Player Sync job with the DI container
        q.AddJob<EspnPlayerSyncJob>(opts => opts
            .WithIdentity(playerSyncJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably()); // Allow job to exist without triggers during off-season

        // Create a trigger for the ESPN Player Sync job (Daily at 3 AM EST)
        q.AddTrigger(opts => opts
            .ForJob(playerSyncJobKey)
            .WithIdentity("EspnPlayerSyncJob-daily-trigger")
            .WithCronSchedule("0 0 8 * * ? *") // Daily at 8 AM UTC (3 AM EST)
            .WithDescription("ESPN Player roster synchronization - Daily"));

        // Create a "key" for the ESPN Stats Sync job
        var statsSyncJobKey = new JobKey("EspnStatsSyncJob");

        // Register the ESPN Stats Sync job with the DI container
        q.AddJob<EspnStatsSyncJob>(opts => opts
            .WithIdentity(statsSyncJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably()); // Allow job to exist without triggers during off-season

        // Create a trigger for the ESPN Stats Sync job (Every Tuesday at 4 AM EST)
        q.AddTrigger(opts => opts
            .ForJob(statsSyncJobKey)
            .WithIdentity("EspnStatsSyncJob-weekly-trigger")
            .WithCronSchedule("0 0 9 ? * TUE *") // Every Tuesday at 9 AM UTC (4 AM EST)
            .WithDescription("ESPN Player statistics synchronization - Weekly"));

        // Create a "key" for the ESPN Historical Data job (manual trigger only)
        var historicalJobKey = new JobKey("EspnHistoricalDataJob");

        // Register the ESPN Historical Data job with the DI container (no automatic triggers)
        q.AddJob<EspnHistoricalDataJob>(opts => opts
            .WithIdentity(historicalJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably() // Allow job to exist without triggers for manual execution
            .WithDescription("ESPN Historical data backfill - Manual trigger only"));

        // Optional: Add development triggers for the integration jobs
        if (builder.Environment.IsDevelopment())
        {
            // Player sync every hour in development
            q.AddTrigger(opts => opts
                .ForJob(playerSyncJobKey)
                .WithIdentity("EspnPlayerSyncJob-dev-trigger")
                .WithSimpleSchedule(x => x
                    .WithIntervalInHours(1)
                    .RepeatForever())
                .WithDescription("ESPN Player sync - Development"));

            // Stats sync every 2 hours in development
            q.AddTrigger(opts => opts
                .ForJob(statsSyncJobKey)
                .WithIdentity("EspnStatsSyncJob-dev-trigger")
                .WithSimpleSchedule(x => x
                    .WithIntervalInHours(2)
                    .RepeatForever())
                .WithDescription("ESPN Stats sync - Development"));
        }
    });

    // Add Quartz hosted service
    builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

    var app = builder.Build();

    // Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.UseDeveloperExceptionPage();
    }

    app.UseRouting();
    app.MapControllers();
    app.MapHealthChecks("/health");

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
