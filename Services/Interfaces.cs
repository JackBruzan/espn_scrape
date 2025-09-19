namespace ESPNScrape.Services;

public interface IESPNScrapingService
{
    Task ScrapeImagesAsync();
}

public interface IImageDownloadService
{
    Task<bool> DownloadImageAsync(string imageUrl, string fileName, string directory);
    Task<string> EnsureDirectoryExistsAsync(string directory);
}
