using ESPNScrape.Models;
using ESPNScrape.Models.Espn;
using ESPNScrape.Models.Supabase;
using ESPNScrape.Models.PlayerMatching;
using ESPNScrape.Models.DataSync;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Combined interface for Data Processing services
    /// </summary>
    public interface IEspnDataProcessingService : IEspnDataMappingService, IEspnPlayerMatchingService, IEspnStatsTransformationService
    {
    }

    /// <summary>
    /// ESPN data mapping and transformation service
    /// </summary>
    public interface IEspnDataMappingService
    {
        Player MapEspnPlayerToSupabasePlayer(PlayerStats espnPlayer);
        List<Player> MapEspnPlayersToSupabasePlayers(List<PlayerStats> espnPlayers);
        string GetTeamAbbreviation(string espnTeamId);
        string GetTeamDisplayName(string espnTeamId);
        (string abbreviation, string displayName) GetTeamInfo(string espnTeamId);
    }

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

    /// <summary>
    /// Service for transforming ESPN player statistics into database format
    /// </summary>
    public interface IEspnStatsTransformationService
    {
        /// <summary>
        /// Transform a single ESPN player stats object to database format
        /// </summary>
        /// <param name="espnStats">ESPN player stats to transform</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Transformed database player stats</returns>
        Task<DatabasePlayerStats> TransformPlayerStatsAsync(PlayerStats espnStats, CancellationToken cancellationToken = default);

        /// <summary>
        /// Transform multiple ESPN player stats objects to database format in batch
        /// </summary>
        /// <param name="espnStatsList">List of ESPN player stats to transform</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of transformed database player stats</returns>
        Task<List<DatabasePlayerStats>> TransformPlayerStatsBatchAsync(List<PlayerStats> espnStatsList, CancellationToken cancellationToken = default);

        /// <summary>
        /// Validate ESPN player stats for data integrity and realistic ranges
        /// </summary>
        /// <param name="espnStats">ESPN player stats to validate</param>
        /// <returns>Validation result with any errors or warnings</returns>
        Models.DataSync.ValidationResult ValidatePlayerStats(PlayerStats espnStats);

        /// <summary>
        /// Transform ESPN player statistics list into organized stat categories
        /// </summary>
        /// <param name="statistics">List of ESPN player statistics</param>
        /// <returns>Organized stats by category (passing, rushing, receiving)</returns>
        StatCategories OrganizeStatsByCategory(List<PlayerStatistic> statistics);
    }
}