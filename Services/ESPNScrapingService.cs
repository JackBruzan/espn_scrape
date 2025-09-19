using ESPNScrape.Models;
using System.Text.Json;

namespace ESPNScrape.Services;

public class ESPNScrapingService : IESPNScrapingService
{
    private readonly ILogger<ESPNScrapingService> _logger;
    private readonly IImageDownloadService _imageDownloadService;
    private readonly HttpClient _httpClient;

    // ESPN API endpoint for active NFL players
    private const string ActivePlayersUrl = "https://sports.core.api.espn.com/v3/sports/football/nfl/athletes?limit=20000&active=true";

    // ESPN CDN base URL for player headshots
    private const string PlayerImageBaseUrl = "https://a.espncdn.com/combiner/i?img=/i/headshots/nfl/players/full/{0}.png";

    public ESPNScrapingService(ILogger<ESPNScrapingService> logger, IImageDownloadService imageDownloadService)
    {
        _logger = logger;
        _imageDownloadService = imageDownloadService;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
    }

    public async Task ScrapeImagesAsync()
    {
        _logger.LogInformation("Starting ESPN player image scraping process");

        try
        {
            // Step 1: Get all active NFL players
            var players = await GetActivePlayersAsync();
            _logger.LogInformation("Retrieved {Count} active NFL players", players.Count);

            if (players.Count == 0)
            {
                _logger.LogWarning("No active players found");
                return;
            }

            // Step 2: Download player headshots
            var downloadedImages = 0;
            var failedDownloads = 0;

            foreach (var player in players.Where(p => p.Active))
            {
                try
                {
                    var imageUrl = string.Format(PlayerImageBaseUrl, player.Id);
                    var fileName = GeneratePlayerFileName(player);
                    var directory = GetPlayerDirectory(player);

                    var success = await _imageDownloadService.DownloadImageAsync(imageUrl, fileName, directory);

                    if (success)
                    {
                        downloadedImages++;
                        _logger.LogDebug("Downloaded headshot for player: {PlayerName} ({Team}) (ID: {PlayerId})",
                            player.DisplayName, player.Team?.Abbreviation ?? "NO_TEAM", player.Id);
                    }
                    else
                    {
                        failedDownloads++;
                        _logger.LogWarning("Failed to download headshot for player: {PlayerName} ({Team}) (ID: {PlayerId})",
                            player.DisplayName, player.Team?.Abbreviation ?? "NO_TEAM", player.Id);
                    }

                    // Add small delay between downloads to be respectful
                    await Task.Delay(100);
                }
                catch (Exception ex)
                {
                    failedDownloads++;
                    _logger.LogError(ex, "Error downloading headshot for player: {PlayerName} ({Team}) (ID: {PlayerId})",
                        player.DisplayName, player.Team?.Abbreviation ?? "NO_TEAM", player.Id);
                }
            }

            _logger.LogInformation("ESPN player image scraping completed. Total players: {Total}, Downloaded: {Downloaded}, Failed: {Failed}",
                players.Count, downloadedImages, failedDownloads);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during ESPN player image scraping process");
            throw;
        }
    }

    private async Task<List<Player>> GetActivePlayersAsync()
    {
        try
        {
            _logger.LogInformation("Fetching active NFL players from ESPN API");

            var response = await _httpClient.GetStringAsync(ActivePlayersUrl);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var playerResponse = JsonSerializer.Deserialize<PlayerResponse>(response, options);

            if (playerResponse?.Items == null)
            {
                _logger.LogWarning("No player data found in API response");
                return new List<Player>();
            }

            // Simple filtering - get real active players, skip system entries
            var validPlayers = playerResponse.Items
                .Where(p => !string.IsNullOrEmpty(p.Id) &&
                           !string.IsNullOrEmpty(p.DisplayName) &&
                           !p.DisplayName.StartsWith("[") && // Skip system entries like [35], [Downed], etc.
                           p.Active) // Only get active players
                .ToList();

            _logger.LogInformation("Found {ValidCount} valid players out of {TotalCount} total",
                validPlayers.Count, playerResponse.Items.Count);

            return validPlayers;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP error while fetching active players from ESPN API");
            throw;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Error parsing JSON response from ESPN API");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while fetching active players");
            throw;
        }
    }

    private string GeneratePlayerFileName(Player player)
    {
        // Create filename: PlayerId_PlayerName.png
        var sanitizedName = SanitizeFileName(player.DisplayName);
        return $"{player.Id}_{sanitizedName}.png";
    }

    private string GetPlayerDirectory(Player player)
    {
        // Organize by team if available, otherwise use a general folder
        var teamFolder = !string.IsNullOrEmpty(player.Team?.Abbreviation)
            ? player.Team.Abbreviation.ToUpper()
            : "NO_TEAM";

        return Path.Combine("downloads", "nfl_players", teamFolder);
    }
    private string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return "unknown";

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());

        // Replace spaces with underscores and limit length
        sanitized = sanitized.Replace(" ", "_");
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
