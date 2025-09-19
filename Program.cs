using ESPNScrape.Jobs;
using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.HealthChecks;
using ESPNScrape.Configuration;
using Quartz;
using Serilog;
using Polly;
using Polly.Extensions.Http;
using Microsoft.Extensions.Http;

// Configure Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .WriteTo.File("logs/espn-scrape-.txt", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    Log.Information("Starting ESPN Scrape Service");

    var builder = Host.CreateApplicationBuilder(args);

    // Add Serilog
    builder.Services.AddSerilog();

    // Configure cache settings
    builder.Services.Configure<CacheConfiguration>(builder.Configuration.GetSection("Cache"));

    // Register services
    builder.Services.AddSingleton<IImageDownloadService, ImageDownloadService>();
    builder.Services.AddSingleton<IESPNScrapingService, ESPNScrapingService>();

    // Add HTTP client for ESPN service (retry logic is handled in EspnHttpService)
    builder.Services.AddHttpClient<IEspnHttpService, EspnHttpService>();

    // Register ESPN Scoreboard Service
    builder.Services.AddScoped<IEspnScoreboardService, EspnScoreboardService>();

    // Add memory cache for response caching
    builder.Services.AddMemoryCache(options =>
    {
        options.SizeLimit = 1000; // Max number of cache entries
    });

    // Register ESPN Cache Service
    builder.Services.AddSingleton<IEspnCacheService, EspnCacheService>();

    // Register Main ESPN API Service
    builder.Services.AddScoped<IEspnApiService, EspnApiService>();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<EspnApiHealthCheck>("espn_api", tags: new[] { "espn", "api" });

    // Add Quartz
    builder.Services.AddQuartz(q =>
    {
        // Create a "key" for the job
        var jobKey = new JobKey("ESPNImageScrapingJob");

        // Register the job with the DI container
        q.AddJob<ESPNImageScrapingJob>(opts => opts
            .WithIdentity(jobKey)
            .DisallowConcurrentExecution()); // Prevent concurrent execution

        // Create a trigger for the job
        q.AddTrigger(opts => opts
            .ForJob(jobKey)
            .WithIdentity("ESPNImageScrapingJob-trigger")
            // Run every 5 seconds for testing
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(5)
                .RepeatForever()));
    });

    // Add Quartz hosted service
    builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

    var host = builder.Build();

    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
