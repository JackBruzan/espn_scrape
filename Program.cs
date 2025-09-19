using ESPNScrape.Jobs;
using ESPNScrape.Services;
using Quartz;
using Serilog;

namespace ESPNScrape;

public class Program
{
    public static async Task Main(string[] args)
    {
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

            // Register services
            builder.Services.AddSingleton<IImageDownloadService, ImageDownloadService>();
            builder.Services.AddSingleton<IESPNScrapingService, ESPNScrapingService>();

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
    }
}
