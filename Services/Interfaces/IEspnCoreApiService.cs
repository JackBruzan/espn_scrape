using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Interface for ESPN Core API service for schedule and odds data
    /// </summary>
    public interface IEspnCoreApiService
    {
        /// <summary>
        /// Gets the weekly schedule from ESPN Core API
        /// </summary>
        Task<EspnScheduleResponse?> GetWeeklyScheduleAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets odds for a specific event from ESPN Core API
        /// </summary>
        Task<EspnOdds?> GetEventOddsAsync(string gameId, string competitionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets odds for multiple events in bulk
        /// </summary>
        Task<List<EspnOdds>> GetBulkEventOddsAsync(IEnumerable<(string gameId, string competitionId)> gameCompetitionPairs, CancellationToken cancellationToken = default);
    }
}