using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Service interface for accessing ESPN box score data
    /// </summary>
    public interface IEspnBoxScoreService
    {
        /// <summary>
        /// Get complete box score data for a completed game
        /// </summary>
        /// <param name="gameId">ESPN game ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete box score data</returns>
        Task<BoxScore?> GetBoxScoreDataAsync(string gameId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get box score data for a live/in-progress game
        /// </summary>
        /// <param name="gameId">ESPN game ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Partial box score data for live game</returns>
        Task<BoxScore?> GetLiveBoxScoreDataAsync(string gameId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Parse team-level statistics from ESPN box score data
        /// </summary>
        /// <param name="boxScoreJson">Raw JSON box score data from ESPN</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Parsed team statistics</returns>
        Task<(TeamBoxScore homeTeam, TeamBoxScore awayTeam)?> ParseTeamStatsAsync(string boxScoreJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Extract game metadata including weather, attendance, and officials
        /// </summary>
        /// <param name="boxScoreJson">Raw JSON box score data from ESPN</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Game metadata information</returns>
        Task<GameInfo?> ExtractGameMetadataAsync(string boxScoreJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get box score URL for a specific game
        /// </summary>
        /// <param name="gameId">ESPN game ID</param>
        /// <returns>ESPN box score URL</returns>
        string GetBoxScoreUrl(string gameId);

        /// <summary>
        /// <summary>
        /// Parse player statistics from boxscore.players section
        /// </summary>
        /// <param name="boxScoreJson">Raw JSON box score data from ESPN</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of player statistics</returns>
        Task<List<PlayerStats>> ParsePlayerStatsAsync(string boxScoreJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get team offensive statistics from box score
        /// </summary>
        /// <param name="gameId">ESPN game ID</param>
        /// <param name="teamId">ESPN team ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Team offensive statistics</returns>
        Task<Dictionary<string, object>?> GetTeamOffensiveStatsAsync(string gameId, string teamId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get team defensive statistics from box score
        /// </summary>
        /// <param name="gameId">ESPN game ID</param>
        /// <param name="teamId">ESPN team ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Team defensive statistics</returns>
        Task<Dictionary<string, object>?> GetTeamDefensiveStatsAsync(string gameId, string teamId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Determine if a game is currently live/in-progress
        /// </summary>
        /// <param name="gameId">ESPN game ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if game is live, false if completed or not started</returns>
        Task<bool> IsGameLiveAsync(string gameId, CancellationToken cancellationToken = default);
    }
}