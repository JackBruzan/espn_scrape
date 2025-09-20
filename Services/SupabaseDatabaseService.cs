namespace ESPNScrape.Services;

public interface ISupabaseDatabaseService
{
    Task<long?> FindPlayerByEspnIdAsync(string espnId, CancellationToken cancellationToken = default);
    Task<(long? PlayerId, string? Name)> FindMatchingPlayerAsync(Models.Player espnPlayer, CancellationToken cancellationToken = default);
    Task<bool> AddPlayerAsync(Models.Player espnPlayer, CancellationToken cancellationToken = default);
    Task<bool> UpdatePlayerAsync(long playerId, Models.Player espnPlayer, CancellationToken cancellationToken = default);
    Task<long?> FindTeamIdByAbbreviationAsync(string abbreviation, CancellationToken cancellationToken = default);
    Task<long?> FindPositionIdByNameAsync(string positionName, CancellationToken cancellationToken = default);
}

public class SupabaseDatabaseService : ISupabaseDatabaseService
{
    private readonly ILogger<SupabaseDatabaseService> _logger;

    public SupabaseDatabaseService(ILogger<SupabaseDatabaseService> logger)
    {
        _logger = logger;
        _logger.LogInformation("SupabaseDatabaseService CONSTRUCTOR CALLED!");
    }

    public Task<long?> FindPlayerByEspnIdAsync(string espnId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("FindPlayerByEspnIdAsync called for ESPN ID: {EspnId}", espnId);
        return Task.FromResult<long?>(null);
    }

    public Task<(long? PlayerId, string? Name)> FindMatchingPlayerAsync(Models.Player espnPlayer, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("FindMatchingPlayerAsync called for {FirstName} {LastName}", espnPlayer.FirstName, espnPlayer.LastName);
        return Task.FromResult<(long?, string?)>((null, null));
    }

    public Task<bool> AddPlayerAsync(Models.Player espnPlayer, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("AddPlayerAsync called for {FirstName} {LastName}", espnPlayer.FirstName, espnPlayer.LastName);
        return Task.FromResult(false);
    }

    public Task<bool> UpdatePlayerAsync(long playerId, Models.Player espnPlayer, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("UpdatePlayerAsync called for player {PlayerId}", playerId);
        return Task.FromResult(false);
    }

    public Task<long?> FindTeamIdByAbbreviationAsync(string abbreviation, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<long?>(null);
    }

    public Task<long?> FindPositionIdByNameAsync(string positionName, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<long?>(null);
    }
}
