using ESPNScrape.Jobs;
using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.Services.Core;
using ESPNScrape.Services.DataRetrieval;
using ESPNScrape.Services.DataProcessing;
using ESPNScrape.Services.DataOperations;
using ESPNScrape.Services.Infrastructure;

using ESPNScrape.HealthChecks;
using ESPNScrape.Configuration;
using ESPNScrape.Models.PlayerMatching;
using ESPNScrape.Models.DataSync;
using Quartz;
using Serilog;


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

    // Register database service  
    builder.Services.AddScoped<ISupabaseDatabaseService, SupabaseDatabaseService>();

    // Register combined scraping service (includes image download)
    builder.Services.AddSingleton<ESPNScrape.Services.DataOperations.EspnScrapingService>();
    builder.Services.AddSingleton<IESPNScrapingService>(provider => provider.GetRequiredService<ESPNScrape.Services.DataOperations.EspnScrapingService>());
    builder.Services.AddSingleton<IImageDownloadService>(provider => provider.GetRequiredService<ESPNScrape.Services.DataOperations.EspnScrapingService>());

    // Register infrastructure services
    builder.Services.AddSingleton<EspnRateLimitService>();
    builder.Services.AddSingleton<IEspnRateLimitService>(provider => provider.GetRequiredService<EspnRateLimitService>());

    builder.Services.AddSingleton<EspnHttpService>();
    builder.Services.AddSingleton<IEspnHttpService>(provider => provider.GetRequiredService<EspnHttpService>());

    builder.Services.AddSingleton<EspnCacheService>();
    builder.Services.AddSingleton<IEspnCacheService>(provider => provider.GetRequiredService<EspnCacheService>());

    // Add HTTP client for ESPN service with enhanced resilience
    builder.Services.AddHttpClient<EspnHttpService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30); // Default timeout, will be overridden by resilience config
    });

    // Register combined ESPN Data Retrieval Service (includes Scoreboard, BoxScore, Schedule)
    builder.Services.AddScoped<EspnDataRetrievalService>();
    builder.Services.AddScoped<IEspnScoreboardService>(provider => provider.GetRequiredService<EspnDataRetrievalService>());
    builder.Services.AddScoped<IEspnBoxScoreService>(provider => provider.GetRequiredService<EspnDataRetrievalService>());
    builder.Services.AddScoped<IEspnScheduleService>(provider => provider.GetRequiredService<EspnDataRetrievalService>());

    // Add memory cache for response caching
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1000; // Max number of cache entries
    });



    // Register Data Operations Services
    builder.Services.AddScoped<IEspnPlayerStatsService, EspnPlayerStatsService>();
    builder.Services.AddScoped<IEspnBulkOperationsService, EspnBulkOperationsService>();
    builder.Services.AddScoped<IEspnDataSyncService, EspnDataSyncService>();

    // Register Data Processing Services  
    builder.Services.AddScoped<IEspnPlayerMatchingService, EspnPlayerMatchingService>();
    builder.Services.AddScoped<IEspnStatsTransformationService, EspnStatsTransformationService>();

    // Register combined infrastructure service (replaces logging, metrics, alerting)
    builder.Services.AddSingleton<EspnInfrastructureService>();
    builder.Services.AddSingleton<IEspnLoggingService>(provider => provider.GetRequiredService<EspnInfrastructureService>());
    builder.Services.AddSingleton<IEspnMetricsService>(provider => provider.GetRequiredService<EspnInfrastructureService>());
    builder.Services.AddSingleton<IEspnAlertingService>(provider => provider.GetRequiredService<EspnInfrastructureService>());

    // Register combined ESPN API Service (includes Core API functionality)
    builder.Services.AddScoped<EspnApiService>();
    builder.Services.AddScoped<IEspnApiService>(provider => provider.GetRequiredService<EspnApiService>());
    builder.Services.AddScoped<IEspnCoreApiService>(provider => provider.GetRequiredService<EspnApiService>());

    // Register ESPN Data Mapping Service (combined mapping functionality)
    builder.Services.AddScoped<EspnDataMappingService>();

    // Add ASP.NET Core services for diagnostic endpoints
    builder.Services.AddControllers();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<EspnApiHealthCheck>("espn_api", tags: new[] { "espn", "api" })
        .AddCheck<EspnIntegrationHealthCheck>("espn_integration", tags: new[] { "espn", "integration", "monitoring" });

    // Alert monitoring is now handled by the EspnInfrastructureService

    // Add Quartz
    builder.Services.AddQuartz(q =>
    {
        // Create a "key" for the ESPN Player Sync job
        var playerSyncJobKey = new JobKey("EspnPlayerSyncJob");

        // Register the ESPN Player Sync job with the DI container
        q.AddJob<EspnPlayerSyncJob>(opts => opts
            .WithIdentity(playerSyncJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably()); // Allow job to exist without triggers during off-season

        // Player sync daily at 2:30 AM Eastern (7:30 AM UTC)
        q.AddTrigger(opts => opts
            .ForJob(playerSyncJobKey)
            .WithIdentity("EspnPlayerSyncJob-trigger")
            .WithCronSchedule("0 30 7 * * ?") // Daily at 7:30 AM UTC (2:30 AM EST)
            .WithDescription("ESPN Player sync - Daily at 2:30 AM EST"));

        // Create a "key" for the ESPN Image Scraping job  
        var imageScrapeJobKey = new JobKey("EspnImageScrapingJob");

        // Register the ESPN Image Scraping job with the DI container
        q.AddJob<ESPNImageScrapingJob>(opts => opts
            .WithIdentity(imageScrapeJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably()); // Allow job to exist without triggers during off-season

        // Image sync daily at 3:00 AM Eastern (8:00 AM UTC) - after player sync
        q.AddTrigger(opts => opts
            .ForJob(imageScrapeJobKey)
            .WithIdentity("EspnImageScrapingJob-trigger")
            .WithCronSchedule("0 0 8 * * ?") // Daily at 8:00 AM UTC (3:00 AM EST)
            .WithDescription("ESPN Image scraping - Daily at 3:00 AM EST"));

        // Create a "key" for the ESPN API Scraping job
        var apiScrapeJobKey = new JobKey("EspnApiScrapingJob");

        // Register the ESPN API Scraping job with the DI container
        q.AddJob<EspnApiScrapingJob>(opts => opts
            .WithIdentity(apiScrapeJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably()); // Allow job to exist without triggers during off-season

        // API scraping every 15 minutes during season
        q.AddTrigger(opts => opts
            .ForJob(apiScrapeJobKey)
            .WithIdentity("EspnApiScrapingJob-trigger")
            .WithCronSchedule("0 */15 * * * ?") // Every 15 minutes
            .WithDescription("ESPN API scraping - Every 15 minutes"));

        // Create a "key" for the ESPN Stats Sync job
        var statsSyncJobKey = new JobKey("EspnStatsSyncJob");

        // Register the ESPN Stats Sync job with the DI container
        q.AddJob<EspnStatsSyncJob>(opts => opts
            .WithIdentity(statsSyncJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably()); // Allow job to exist without triggers during off-season

        // Stats sync hourly during season
        q.AddTrigger(opts => opts
            .ForJob(statsSyncJobKey)
            .WithIdentity("EspnStatsSyncJob-trigger")
            .WithCronSchedule("0 0 * * * ?") // Every hour
            .WithDescription("ESPN Stats sync - Hourly"));

        // Create a "key" for the ESPN Historical Data job
        var historicalDataJobKey = new JobKey("EspnHistoricalDataJob");

        // Register the ESPN Historical Data job with the DI container
        q.AddJob<EspnHistoricalDataJob>(opts => opts
            .WithIdentity(historicalDataJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably()); // Allow job to exist without triggers during off-season

        // Historical data sync weekly on Sundays at 4:00 AM Eastern (9:00 AM UTC)
        q.AddTrigger(opts => opts
            .ForJob(historicalDataJobKey)
            .WithIdentity("EspnHistoricalDataJob-trigger")
            .WithCronSchedule("0 0 9 ? * SUN") // Weekly on Sunday at 9:00 AM UTC (4:00 AM EST)
            .WithDescription("ESPN Historical data sync - Weekly on Sunday at 4:00 AM EST"));

        // Create a "key" for the ESPN Schedule Scraping job
        var scheduleScrapeJobKey = new JobKey("EspnScheduleScrapingJob");

        // Register the ESPN Schedule Scraping job with the DI container
        q.AddJob<EspnScheduleScrapingJob>(opts => opts
            .WithIdentity(scheduleScrapeJobKey)
            .DisallowConcurrentExecution() // Prevent concurrent execution
            .StoreDurably()); // Allow job to exist without triggers during off-season

        // Schedule scraping daily at 1:00 AM Eastern (6:00 AM UTC)
        q.AddTrigger(opts => opts
            .ForJob(scheduleScrapeJobKey)
            .WithIdentity("EspnScheduleScrapingJob-trigger")
            .WithCronSchedule("0 0 6 * * ?") // Daily at 6:00 AM UTC (1:00 AM EST)
            .WithDescription("ESPN Schedule scraping - Daily at 1:00 AM EST"));
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
