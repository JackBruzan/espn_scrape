using ESPNScrape.Models.Espn;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace ESPNScrape.Services
{
    public class EspnApiService : IEspnApiService
    {
        private readonly IEspnScoreboardService _scoreboardService;
        private readonly IEspnBoxScoreService _boxScoreService;
        private readonly IEspnCacheService _cacheService;
        private readonly IEspnHttpService _httpService;
        private readonly ILogger<EspnApiService> _logger;

        public EspnApiService(
            IEspnScoreboardService scoreboardService,
            IEspnBoxScoreService boxScoreService,
            IEspnCacheService cacheService,
            IEspnHttpService httpService,
            ILogger<EspnApiService> logger)
        {
            _scoreboardService = scoreboardService;
            _boxScoreService = boxScoreService;
            _cacheService = cacheService;
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<Season> GetSeasonAsync(int year, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetSeason", year);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching season data for year {Year}", year);

                // Get season data from scoreboard (regular season, week 1 as a starting point)
                var scoreboard = await _scoreboardService.GetScoreboardAsync(year, 1, 2, cancellationToken);
                var season = await _scoreboardService.ExtractSeasonInfoAsync(scoreboard, cancellationToken);

                _logger.LogInformation("Successfully retrieved season data for year {Year}", year);
                return season;
            }, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<Week>> GetWeeksAsync(int year, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetWeeks", year, seasonType);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching weeks for year {Year}, season type {SeasonType}", year, seasonType);

                var weeks = new List<Week>();
                var maxWeeks = GetMaxWeeksForSeasonType(seasonType);

                // Try to fetch each week to build the complete weeks list
                for (int week = 1; week <= maxWeeks; week++)
                {
                    try
                    {
                        var scoreboard = await _scoreboardService.GetScoreboardAsync(year, week, seasonType, cancellationToken);
                        var weekData = await _scoreboardService.ExtractWeekInfoAsync(scoreboard, cancellationToken);

                        if (weekData != null && weekData.WeekNumber > 0)
                        {
                            weeks.Add(weekData);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch week {Week} for year {Year}, season type {SeasonType}",
                            week, year, seasonType);
                        // Continue to next week
                    }
                }

                _logger.LogInformation("Successfully retrieved {WeekCount} weeks for year {Year}, season type {SeasonType}",
                    weeks.Count, year, seasonType);
                return weeks.AsEnumerable();
            }, cancellationToken: cancellationToken);
        }

        public async Task<Week> GetCurrentWeekAsync(CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetCurrentWeek");

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Determining current NFL week");

                var currentDate = DateTime.Now;
                var currentYear = GetNflSeasonYear(currentDate);
                var (seasonType, weekNumber) = CalculateCurrentWeek(currentDate, currentYear);

                _logger.LogDebug("Calculated current week: Year {Year}, Week {Week}, Season Type {SeasonType}",
                    currentYear, weekNumber, seasonType);

                var scoreboard = await _scoreboardService.GetScoreboardAsync(currentYear, weekNumber, seasonType, cancellationToken);
                var currentWeek = await _scoreboardService.ExtractWeekInfoAsync(scoreboard, cancellationToken);

                _logger.LogInformation("Current week: {WeekLabel} (Year: {Year}, Week: {Week}, Season Type: {SeasonType})",
                    currentWeek.Label, currentYear, weekNumber, seasonType);

                return currentWeek;
            }, TimeSpan.FromMinutes(15), cancellationToken); // Shorter cache for current week
        }

        public async Task<Week> GetWeekAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetWeek", year, weekNumber, seasonType);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching week {Week} for year {Year}, season type {SeasonType}",
                    weekNumber, year, seasonType);

                var scoreboard = await _scoreboardService.GetScoreboardAsync(year, weekNumber, seasonType, cancellationToken);
                var week = await _scoreboardService.ExtractWeekInfoAsync(scoreboard, cancellationToken);

                _logger.LogInformation("Successfully retrieved week {Week} for year {Year}", weekNumber, year);
                return week;
            }, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<GameEvent>> GetGamesAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetGames", year, weekNumber, seasonType);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching games for year {Year}, week {Week}, season type {SeasonType}",
                    year, weekNumber, seasonType);

                var scoreboard = await _scoreboardService.GetScoreboardAsync(year, weekNumber, seasonType, cancellationToken);
                var games = await _scoreboardService.ExtractEventsAsync(scoreboard, cancellationToken);

                _logger.LogInformation("Successfully retrieved {GameCount} games for year {Year}, week {Week}",
                    games.Count(), year, weekNumber);
                return games;
            }, cancellationToken: cancellationToken);
        }

        public async Task<GameEvent> GetGameAsync(string eventId, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetGame", eventId);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching game details for event ID {EventId}", eventId);

                var eventUrl = $"http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/events/{eventId}";
                var gameEvent = await _httpService.GetAsync<GameEvent>(eventUrl, cancellationToken);

                _logger.LogInformation("Successfully retrieved game details for event ID {EventId}", eventId);
                return gameEvent;
            }, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<GameEvent>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetGamesForDate", date.ToString("yyyy-MM-dd"));

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching games for date {Date}", date.ToString("yyyy-MM-dd"));

                var nflYear = GetNflSeasonYear(date);
                var (seasonType, weekNumber) = CalculateWeekFromDate(date, nflYear);

                var allGames = await GetGamesAsync(nflYear, weekNumber, seasonType, cancellationToken);
                var gamesForDate = allGames.Where(g => g.Date.Date == date.Date).ToList();

                _logger.LogInformation("Found {GameCount} games for date {Date}", gamesForDate.Count, date.ToString("yyyy-MM-dd"));
                return gamesForDate.AsEnumerable();
            }, cancellationToken: cancellationToken);
        }

        public async Task<BoxScore> GetBoxScoreAsync(string eventId, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetBoxScore", eventId);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching box score for event ID {EventId}", eventId);

                var boxScore = await _boxScoreService.GetBoxScoreDataAsync(eventId, cancellationToken);

                if (boxScore == null)
                {
                    throw new InvalidOperationException($"Box score data not found for event ID {eventId}");
                }

                _logger.LogInformation("Successfully retrieved box score for event ID {EventId}", eventId);
                return boxScore;
            }, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<PlayerStats>> GetGamePlayerStatsAsync(string eventId, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetGamePlayerStats", eventId);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching player stats for event ID {EventId}", eventId);

                // This would typically extract player stats from the box score
                var boxScore = await GetBoxScoreAsync(eventId, cancellationToken);
                var playerStats = ExtractPlayerStatsFromBoxScore(boxScore);

                _logger.LogInformation("Successfully retrieved player stats for event ID {EventId}", eventId);
                return playerStats;
            }, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<PlayerStats>> GetWeekPlayerStatsAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetWeekPlayerStats", year, weekNumber, seasonType);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching player stats for year {Year}, week {Week}, season type {SeasonType}",
                    year, weekNumber, seasonType);

                var games = await GetGamesAsync(year, weekNumber, seasonType, cancellationToken);
                var allPlayerStats = new List<PlayerStats>();

                // Process games in parallel for better performance
                var tasks = games.Select(async game =>
                {
                    try
                    {
                        return await GetGamePlayerStatsAsync(game.Id, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get player stats for game {GameId}", game.Id);
                        return Enumerable.Empty<PlayerStats>();
                    }
                });

                var results = await Task.WhenAll(tasks);
                allPlayerStats.AddRange(results.SelectMany(stats => stats));

                _logger.LogInformation("Successfully retrieved {PlayerStatsCount} player stat records for year {Year}, week {Week}",
                    allPlayerStats.Count, year, weekNumber);
                return allPlayerStats.AsEnumerable();
            }, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<PlayerStats>> GetSeasonPlayerStatsAsync(int year, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetSeasonPlayerStats", year, seasonType);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching season player stats for year {Year}, season type {SeasonType}", year, seasonType);

                var weeks = await GetWeeksAsync(year, seasonType, cancellationToken);
                var allPlayerStats = new List<PlayerStats>();

                // Process weeks in parallel for better performance
                var tasks = weeks.Select(async week =>
                {
                    try
                    {
                        return await GetWeekPlayerStatsAsync(year, week.WeekNumber, seasonType, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to get player stats for week {Week}", week.WeekNumber);
                        return Enumerable.Empty<PlayerStats>();
                    }
                });

                var results = await Task.WhenAll(tasks);
                allPlayerStats.AddRange(results.SelectMany(stats => stats));

                _logger.LogInformation("Successfully retrieved {PlayerStatsCount} season player stat records for year {Year}",
                    allPlayerStats.Count, year);
                return allPlayerStats.AsEnumerable();
            }, cancellationToken: cancellationToken);
        }

        public async Task<IEnumerable<PlayerStats>> GetAllPlayersWeekStatsAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            // This is essentially the same as GetWeekPlayerStatsAsync but with explicit "all players" semantics
            return await GetWeekPlayerStatsAsync(year, weekNumber, seasonType, cancellationToken);
        }

        public async Task<IEnumerable<Team>> GetTeamsAsync(CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetTeams");

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching NFL teams");

                var teamsUrl = "http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/teams";
                var teamsResponse = await _httpService.GetAsync<TeamsResponse>(teamsUrl, cancellationToken);

                _logger.LogInformation("Successfully retrieved {TeamCount} teams", teamsResponse.Teams.Count());
                return teamsResponse.Teams;
            }, cancellationToken: cancellationToken);
        }

        public async Task<Team> GetTeamAsync(string teamId, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetTeam", teamId);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching team details for team ID {TeamId}", teamId);

                var teamUrl = $"http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/teams/{teamId}";
                var team = await _httpService.GetAsync<Team>(teamUrl, cancellationToken);

                _logger.LogInformation("Successfully retrieved team details for team ID {TeamId}", teamId);
                return team;
            }, cancellationToken: cancellationToken);
        }

        #region Private Helper Methods

        private static int GetMaxWeeksForSeasonType(int seasonType)
        {
            return seasonType switch
            {
                1 => 4,  // Preseason
                2 => 18, // Regular season
                3 => 5,  // Postseason
                _ => 18  // Default to regular season
            };
        }

        private static int GetNflSeasonYear(DateTime date)
        {
            // NFL season typically starts in September and ends in February of the following year
            // If it's January-July, it's still considered the previous season
            return date.Month <= 7 ? date.Year - 1 : date.Year;
        }

        private static (int seasonType, int weekNumber) CalculateCurrentWeek(DateTime currentDate, int nflYear)
        {
            // This is a simplified calculation - in a real implementation, 
            // you'd want to use actual NFL schedule data
            var seasonStart = new DateTime(nflYear, 9, 1); // Approximate season start

            if (currentDate < seasonStart)
            {
                // Preseason
                var preseasonStart = new DateTime(nflYear, 8, 1);
                var weeksSincePreseason = (int)((currentDate - preseasonStart).Days / 7) + 1;
                return (1, Math.Max(1, Math.Min(4, weeksSincePreseason)));
            }

            var regularSeasonStart = new DateTime(nflYear, 9, 7); // Approximate
            if (currentDate < regularSeasonStart.AddDays(18 * 7))
            {
                // Regular season
                var weeksSinceRegular = (int)((currentDate - regularSeasonStart).Days / 7) + 1;
                return (2, Math.Max(1, Math.Min(18, weeksSinceRegular)));
            }

            // Postseason
            var postseasonStart = regularSeasonStart.AddDays(18 * 7);
            var weeksSincePostseason = (int)((currentDate - postseasonStart).Days / 7) + 1;
            return (3, Math.Max(1, Math.Min(5, weeksSincePostseason)));
        }

        private static (int seasonType, int weekNumber) CalculateWeekFromDate(DateTime date, int nflYear)
        {
            return CalculateCurrentWeek(date, nflYear);
        }

        private static IEnumerable<PlayerStats> ExtractPlayerStatsFromBoxScore(BoxScore boxScore)
        {
            // This would be implemented to extract player statistics from the box score
            // For now, return empty collection as a placeholder
            return Enumerable.Empty<PlayerStats>();
        }

        #endregion
    }

    // Helper response model for teams API
    public class TeamsResponse
    {
        public IEnumerable<Team> Teams { get; set; } = new List<Team>();
    }
}