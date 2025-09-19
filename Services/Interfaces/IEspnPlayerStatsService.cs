using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Service responsible for extracting player statistics from ESPN game data
    /// </summary>
    public interface IEspnPlayerStatsService
    {
        /// <summary>
        /// Extracts player statistics from a specific game's box score data
        /// </summary>
        /// <param name="eventId">ESPN event identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of player statistics for the game</returns>
        Task<IEnumerable<PlayerStats>> ExtractGamePlayerStatsAsync(string eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts player statistics from raw ESPN box score JSON data
        /// </summary>
        /// <param name="boxScoreJson">Raw ESPN box score JSON response</param>
        /// <param name="gameInfo">Game metadata for context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of parsed player statistics</returns>
        Task<IEnumerable<PlayerStats>> ParsePlayerStatsFromJsonAsync(string boxScoreJson, GameEvent gameInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts ESPN player IDs from box score data for correlation
        /// </summary>
        /// <param name="boxScoreJson">Raw ESPN box score JSON response</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of ESPN player IDs found in the data</returns>
        Task<IEnumerable<string>> ExtractPlayerIdsAsync(string boxScoreJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Maps ESPN player data to PlayerStats model based on position
        /// </summary>
        /// <param name="espnPlayerData">Raw ESPN player data</param>
        /// <param name="position">Player position for stat categorization</param>
        /// <param name="gameContext">Game context information</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Mapped PlayerStats object</returns>
        Task<PlayerStats> MapEspnPlayerDataAsync(dynamic espnPlayerData, PlayerPosition position, GameEvent gameContext, CancellationToken cancellationToken = default);

        /// <summary>
        /// Normalizes player names for consistent data matching
        /// </summary>
        /// <param name="playerName">Raw player name from ESPN</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Normalized player name</returns>
        Task<string> NormalizePlayerNameAsync(string playerName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts team statistics from box score data
        /// </summary>
        /// <param name="boxScoreJson">Raw ESPN box score JSON response</param>
        /// <param name="teamId">Specific team ID to extract stats for</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Team-level statistics</returns>
        Task<IEnumerable<PlayerStats>> ExtractTeamStatsAsync(string boxScoreJson, string teamId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles missing or incomplete statistical data gracefully
        /// </summary>
        /// <param name="playerData">Incomplete player data</param>
        /// <param name="position">Player position for default values</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>PlayerStats with appropriate defaults for missing data</returns>
        Task<PlayerStats> HandleMissingDataAsync(dynamic playerData, PlayerPosition position, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates extracted player statistics for data quality
        /// </summary>
        /// <param name="playerStats">Player statistics to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if statistics are valid, false otherwise</returns>
        Task<bool> ValidatePlayerStatsAsync(PlayerStats playerStats, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extracts player statistics from multiple games in parallel
        /// </summary>
        /// <param name="eventIds">Collection of ESPN event IDs</param>
        /// <param name="maxConcurrency">Maximum number of concurrent operations</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of player statistics from all games</returns>
        Task<IEnumerable<PlayerStats>> ExtractBulkGamePlayerStatsAsync(IEnumerable<string> eventIds, int maxConcurrency = 5, CancellationToken cancellationToken = default);

        /// <summary>
        /// Processes large JSON responses using streaming for memory efficiency
        /// </summary>
        /// <param name="boxScoreJsonStream">Stream of box score JSON data</param>
        /// <param name="gameInfo">Game metadata for context</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Async enumerable of player statistics</returns>
        IAsyncEnumerable<PlayerStats> StreamParsePlayerStatsAsync(Stream boxScoreJsonStream, GameEvent gameInfo, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validates multiple player statistics records in parallel
        /// </summary>
        /// <param name="playerStatsCollection">Collection of player statistics to validate</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Dictionary of player stats with their validation results</returns>
        Task<Dictionary<PlayerStats, bool>> ValidateBulkPlayerStatsAsync(IEnumerable<PlayerStats> playerStatsCollection, CancellationToken cancellationToken = default);
    }
}