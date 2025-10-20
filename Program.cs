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
    builder.Services.AddSingleton<ISupabaseDatabaseService, SupabaseDatabaseService>();

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

    // Register combined ESPN Data Retrieval Service (includes Scoreboard, BoxScore, Schedule) - CHANGED TO SINGLETON FOR QUARTZ COMPATIBILITY
    builder.Services.AddSingleton<EspnDataRetrievalService>();
    builder.Services.AddSingleton<IEspnScoreboardService>(provider => provider.GetRequiredService<EspnDataRetrievalService>());
    builder.Services.AddSingleton<IEspnBoxScoreService>(provider => provider.GetRequiredService<EspnDataRetrievalService>());
    builder.Services.AddSingleton<IEspnScheduleService>(provider => provider.GetRequiredService<EspnDataRetrievalService>());

    // Add memory cache for response caching
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1000; // Max number of cache entries
    });



    // Register Data Operations Services - CHANGED TO SINGLETON FOR QUARTZ COMPATIBILITY
    builder.Services.AddSingleton<IEspnPlayerStatsService, EspnPlayerStatsService>();
    builder.Services.AddSingleton<IEspnBulkOperationsService, EspnBulkOperationsService>();
    builder.Services.AddSingleton<IEspnDataSyncService, EspnDataSyncService>();

    // Register Data Processing Services - CHANGED TO SINGLETON FOR QUARTZ COMPATIBILITY  
    builder.Services.AddSingleton<IEspnPlayerMatchingService, EspnPlayerMatchingService>();
    builder.Services.AddSingleton<IEspnStatsTransformationService, EspnStatsTransformationService>();

    // Register combined infrastructure service (replaces logging, metrics, alerting)
    builder.Services.AddSingleton<EspnInfrastructureService>();
    builder.Services.AddSingleton<ESPNScrape.Services.Interfaces.IEspnLoggingService>(provider => provider.GetRequiredService<EspnInfrastructureService>());
    builder.Services.AddSingleton<ESPNScrape.Services.Interfaces.IEspnMetricsService>(provider => provider.GetRequiredService<EspnInfrastructureService>());
    builder.Services.AddSingleton<ESPNScrape.Services.Interfaces.IEspnAlertingService>(provider => provider.GetRequiredService<EspnInfrastructureService>());

    // Register combined ESPN API Service (includes Core API functionality) - CHANGED TO SINGLETON FOR QUARTZ COMPATIBILITY
    builder.Services.AddSingleton<EspnApiService>();
    builder.Services.AddSingleton<IEspnApiService>(provider => provider.GetRequiredService<EspnApiService>());
    builder.Services.AddSingleton<IEspnCoreApiService>(provider => provider.GetRequiredService<EspnApiService>());

    // Register ESPN Data Mapping Service (combined mapping functionality) - CHANGED TO SINGLETON FOR QUARTZ COMPATIBILITY
    builder.Services.AddSingleton<EspnDataMappingService>();

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
        // Stats sync job - every second for testing
        q.AddJob<EspnStatsSyncJob>(j => j
            .WithIdentity("EspnStatsSyncJob")
            .StoreDurably());

        q.AddTrigger(t => t
            .WithIdentity("EspnStatsSyncJob-trigger")
            .ForJob("EspnStatsSyncJob")
            .WithSimpleSchedule(s => s
                .WithIntervalInSeconds(1)
                .RepeatForever())
            .StartNow());

        // API scraping job - every 15 minutes
        q.AddJob<EspnApiScrapingJob>(j => j
            .WithIdentity("EspnApiScrapingJob")
            .StoreDurably());

        q.AddTrigger(t => t
            .WithIdentity("EspnApiScrapingJob-trigger")
            .ForJob("EspnApiScrapingJob")
            .WithCronSchedule("0 */15 * * * ?")
            .WithDescription("ESPN API scraping - Every 15 minutes"));

        // Player sync job - daily at 2:30 AM EST
        q.AddJob<EspnPlayerSyncJob>(j => j
            .WithIdentity("EspnPlayerSyncJob")
            .StoreDurably());

        q.AddTrigger(t => t
            .WithIdentity("EspnPlayerSyncJob-trigger")
            .ForJob("EspnPlayerSyncJob")
            .WithCronSchedule("0 30 7 * * ?")
            .WithDescription("ESPN Player sync - Daily at 2:30 AM EST"));

        // Image scraping job - daily at 3:00 AM EST
        q.AddJob<ESPNImageScrapingJob>(j => j
            .WithIdentity("EspnImageScrapingJob")
            .StoreDurably());

        q.AddTrigger(t => t
            .WithIdentity("EspnImageScrapingJob-trigger")
            .ForJob("EspnImageScrapingJob")
            .WithCronSchedule("0 0 8 * * ?")
            .WithDescription("ESPN Image scraping - Daily at 3:00 AM EST"));

        // Historical data job - weekly on Sundays at 4:00 AM EST
        q.AddJob<EspnHistoricalDataJob>(j => j
            .WithIdentity("EspnHistoricalDataJob")
            .StoreDurably());

        q.AddTrigger(t => t
            .WithIdentity("EspnHistoricalDataJob-trigger")
            .ForJob("EspnHistoricalDataJob")
            .WithCronSchedule("0 0 9 ? * SUN")
            .WithDescription("ESPN Historical data sync - Weekly on Sunday at 4:00 AM EST"));

        // Schedule scraping job - daily at 1:00 AM EST
        q.AddJob<EspnScheduleScrapingJob>(j => j
            .WithIdentity("EspnScheduleScrapingJob")
            .StoreDurably());

        q.AddTrigger(t => t
            .WithIdentity("EspnScheduleScrapingJob-trigger")
            .ForJob("EspnScheduleScrapingJob")
            .WithCronSchedule("0 0 6 * * ?")
            .WithDescription("ESPN Schedule scraping - Daily at 1:00 AM EST"));
    });

    // Add Quartz hosted service
    builder.Services.AddQuartzHostedService(options =>
    {
        options.WaitForJobsToComplete = true;
        options.AwaitApplicationStarted = true;
    });

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
