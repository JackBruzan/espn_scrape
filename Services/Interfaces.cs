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

public interface IEspnHttpService
{
    Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);
    Task<string> GetRawJsonAsync(string endpoint, CancellationToken cancellationToken = default);
    Task<T> GetFromReferenceAsync<T>(string referenceUrl, CancellationToken cancellationToken = default);
}
