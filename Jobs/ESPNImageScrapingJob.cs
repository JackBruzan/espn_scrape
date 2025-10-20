using ESPNScrape.Services;
using ESPNScrape.Services.Interfaces;
using Quartz;

namespace ESPNScrape.Jobs;

public class ESPNImageScrapingJob : IJob
{
    private readonly ILogger<ESPNImageScrapingJob> _logger;
    private readonly IESPNScrapingService _scrapingService;

    public ESPNImageScrapingJob(ILogger<ESPNImageScrapingJob> logger, IESPNScrapingService scrapingService)
    {
        _logger = logger;
        _scrapingService = scrapingService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            _logger.LogInformation("Starting ESPN image scraping job at {Time}", DateTime.UtcNow);

            await _scrapingService.ScrapeImagesAsync();

            _logger.LogInformation("ESPN image scraping job completed successfully at {Time}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during ESPN image scraping job execution");
            throw; // Re-throw to let Quartz handle retry logic
        }
    }
}
