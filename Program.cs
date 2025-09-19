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

    // Configure resilience settings
    builder.Services.Configure<ResilienceConfiguration>(builder.Configuration.GetSection("Resilience"));

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

    // Register Main ESPN API Service
    builder.Services.AddScoped<IEspnApiService, EspnApiService>();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddCheck<EspnApiHealthCheck>("espn_api", tags: new[] { "espn", "api" });

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
