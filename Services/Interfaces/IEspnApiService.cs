using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Primary interface for ESPN NFL data access operations
    /// </summary>
    public interface IEspnApiService
    {
        /// <summary>
        /// Retrieves season information for the specified year
        /// </summary>
        /// <param name="year">The season year (e.g., 2024)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Season data including weeks and configuration</returns>
        Task<Season> GetSeasonAsync(int year, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all weeks for a specific season
        /// </summary>
        /// <param name="year">The season year</param>
        /// <param name="seasonType">Season type: 1=Preseason, 2=Regular, 3=Postseason</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of week data</returns>
        Task<IEnumerable<Week>> GetWeeksAsync(int year, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current active week for the current season
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current week information</returns>
        Task<Week> GetCurrentWeekAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves specific week data
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="weekNumber">Week number within the season</param>
        /// <param name="seasonType">Season type: 1=Preseason, 2=Regular, 3=Postseason</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Week data with events</returns>
        Task<Week> GetWeekAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all games/events for a specific week
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="weekNumber">Week number</param>
        /// <param name="seasonType">Season type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of events for the week</returns>
        Task<IEnumerable<GameEvent>> GetGamesAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a specific game by event ID
        /// </summary>
        /// <param name="eventId">ESPN event identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete event data</returns>
        Task<GameEvent> GetGameAsync(string eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all games for a specific date
        /// </summary>
        /// <param name="date">Target date for games</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of events on the specified date</returns>
        Task<IEnumerable<GameEvent>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves box score statistics for a specific game
        /// </summary>
        /// <param name="eventId">ESPN event identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete box score with team and player statistics</returns>
        Task<BoxScore> GetBoxScoreAsync(string eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets player statistics for a specific game
        /// </summary>
        /// <param name="eventId">ESPN event identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of player statistics for the game</returns>
        Task<IEnumerable<PlayerStats>> GetGamePlayerStatsAsync(string eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all player statistics for a specific week
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="weekNumber">Week number</param>
        /// <param name="seasonType">Season type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of all player statistics for the week</returns>
        Task<IEnumerable<PlayerStats>> GetWeekPlayerStatsAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets comprehensive player statistics for an entire season
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="seasonType">Season type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of aggregated season player statistics</returns>
        Task<IEnumerable<PlayerStats>> GetSeasonPlayerStatsAsync(int year, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk operation to get all players' statistics for a specific week across all games
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="weekNumber">Week number</param>
        /// <param name="seasonType">Season type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete collection of player statistics for all games in the week</returns>
        Task<IEnumerable<PlayerStats>> GetAllPlayersWeekStatsAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all NFL teams with current information
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of NFL teams</returns>
        Task<IEnumerable<Team>> GetTeamsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information for a specific team
        /// </summary>
        /// <param name="teamId">ESPN team identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete team information</returns>
        Task<Team> GetTeamAsync(string teamId, CancellationToken cancellationToken = default);
    }
}