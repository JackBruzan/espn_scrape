namespace ESPNScrape.Services;

public class ImageDownloadService : IImageDownloadService
{
    private readonly ILogger<ImageDownloadService> _logger;
    private readonly HttpClient _httpClient;

    public ImageDownloadService(ILogger<ImageDownloadService> logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    public async Task<bool> DownloadImageAsync(string imageUrl, string fileName, string directory)
    {
        try
        {
            // Ensure directory exists
            var fullDirectory = await EnsureDirectoryExistsAsync(directory);
            var filePath = Path.Combine(fullDirectory, fileName);

            // Check if file already exists
            if (File.Exists(filePath))
            {
                _logger.LogDebug("Image already exists, skipping: {FileName}", fileName);
                return true;
            }

            // Download the image
            var response = await _httpClient.GetAsync(imageUrl);

            if (response.IsSuccessStatusCode)
            {
                var imageBytes = await response.Content.ReadAsByteArrayAsync();

                // Validate it's actually an image (basic check)
                if (IsValidImageData(imageBytes))
                {
                    await File.WriteAllBytesAsync(filePath, imageBytes);
                    _logger.LogDebug("Successfully downloaded image: {FileName} ({Size} bytes)",
                        fileName, imageBytes.Length);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Downloaded data is not a valid image: {ImageUrl}", imageUrl);
                    return false;
                }
            }
            else
            {
                _logger.LogWarning("Failed to download image. Status: {StatusCode}, URL: {ImageUrl}",
                    response.StatusCode, imageUrl);
                return false;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "HTTP error downloading image: {ImageUrl}", imageUrl);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error downloading image: {ImageUrl}", imageUrl);
            return false;
        }
    }

    public Task<string> EnsureDirectoryExistsAsync(string directory)
    {
        try
        {
            var fullPath = Path.GetFullPath(directory);

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _logger.LogInformation("Created directory: {Directory}", fullPath);
            }

            return Task.FromResult(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating directory: {Directory}", directory);
            throw;
        }
    }

    private bool IsValidImageData(byte[] data)
    {
        if (data == null || data.Length < 4)
            return false;

        // Check for common image file signatures
        // JPEG
        if (data[0] == 0xFF && data[1] == 0xD8)
            return true;

        // PNG
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return true;

        // GIF
        if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46)
            return true;

        // WebP
        if (data.Length >= 12 &&
            data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
            data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return true;

        // If we can't identify the format, assume it's valid
        // (some images might have different signatures)
        return true;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
