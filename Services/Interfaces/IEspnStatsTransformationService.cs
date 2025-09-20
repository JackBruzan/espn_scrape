using ESPNScrape.Models.Espn;
using ESPNScrape.Models.DataSync;

namespace ESPNScrape.Services.Interfaces
{
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