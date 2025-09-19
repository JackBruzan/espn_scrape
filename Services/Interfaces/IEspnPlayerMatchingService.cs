using ESPNScrape.Models.PlayerMatching;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Service for intelligently matching ESPN players with existing database players
    /// </summary>
    public interface IEspnPlayerMatchingService
    {
        /// <summary>
        /// Find the best matching database player for a given ESPN player
        /// </summary>
        /// <param name="espnPlayer">ESPN player to match</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Player match result with confidence scoring</returns>
        Task<PlayerMatchResult> FindMatchingPlayerAsync(Models.Player espnPlayer, CancellationToken cancellationToken = default);

        /// <summary>
        /// Find matches for multiple ESPN players in batch
        /// </summary>
        /// <param name="espnPlayers">List of ESPN players to match</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of match results</returns>
        Task<List<PlayerMatchResult>> FindMatchingPlayersAsync(List<Models.Player> espnPlayers, CancellationToken cancellationToken = default);

        /// <summary>
        /// Manually link an ESPN player to a database player
        /// </summary>
        /// <param name="databasePlayerId">Database player ID</param>
        /// <param name="espnPlayerId">ESPN player ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if link was successful</returns>
        Task<bool> LinkPlayerAsync(long databasePlayerId, string espnPlayerId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get list of ESPN players that couldn't be matched automatically
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of unmatched players requiring manual review</returns>
        Task<List<UnmatchedPlayer>> GetUnmatchedPlayersAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Get match statistics and performance metrics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Matching statistics</returns>
        Task<MatchingStatistics> GetMatchingStatisticsAsync(CancellationToken cancellationToken = default);
    }
}