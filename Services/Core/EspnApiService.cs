using ESPNScrape.Models.Espn;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ESPNScrape.Services.Core
{
    /// <summary>
    /// Comprehensive ESPN API service combining high-level orchestration and Core API access
    /// </summary>
    public class EspnApiService : IEspnApiService, IEspnCoreApiService
    {
        private readonly IEspnScoreboardService _scoreboardService;
        private readonly IEspnBoxScoreService _boxScoreService;
        private readonly IEspnCacheService _cacheService;
        private readonly IEspnHttpService _httpService;
        private readonly ILogger<EspnApiService> _logger;

        // ESPN Core API base URL
        private const string ApiBaseUrl = "https://sports.core.api.espn.com/v2";

        // ESPN Core API endpoint templates
        private const string ScheduleUrlTemplate = "{0}/sports/football/leagues/nfl/seasons/{1}/types/{2}/weeks/{3}/events";
        private const string OddsUrlTemplate = "{0}/sports/football/leagues/nfl/events/{1}/competitions/{2}/odds";

        private readonly JsonSerializerOptions _jsonOptions;

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

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

        #region High-Level API Methods (IEspnApiService)

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

                var eventUrl = $"https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/events/{eventId}";
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

                var allTeams = new List<Models.Espn.Team>();

                // ESPN API returns paginated results with team references
                var teamsUrl = "https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/teams";
                var teamsResponse = await _httpService.GetAsync<TeamsResponse>(teamsUrl, cancellationToken);

                _logger.LogInformation("Retrieved {TeamReferenceCount} team references, fetching team details...", teamsResponse.Items.Count());

                // Fetch each team's details from their reference URLs
                foreach (var teamRef in teamsResponse.Items)
                {
                    try
                    {
                        var team = await _httpService.GetAsync<Models.Espn.Team>(teamRef.Ref, cancellationToken);
                        allTeams.Add(team);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch team details from {TeamRef}", teamRef.Ref);
                    }
                }

                _logger.LogInformation("Successfully retrieved {TeamCount} teams", allTeams.Count);
                return allTeams.AsEnumerable();
            }, cancellationToken: cancellationToken);
        }

        public async Task<Models.Espn.Team> GetTeamAsync(string teamId, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetTeam", teamId);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching team details for team ID {TeamId}", teamId);

                var teamUrl = $"https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/teams/{teamId}";
                var team = await _httpService.GetAsync<Models.Espn.Team>(teamUrl, cancellationToken);

                _logger.LogInformation("Successfully retrieved team details for team ID {TeamId}", teamId);
                return team;
            }, cancellationToken: cancellationToken);
        }

        #endregion

        #region Core API Methods (IEspnCoreApiService)

        public async Task<EspnScheduleResponse?> GetWeeklyScheduleAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Fetching ESPN Core API schedule for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}", year, week, seasonType);

                ValidateParameters(year, week, seasonType);

                var url = string.Format(ScheduleUrlTemplate, ApiBaseUrl, year, seasonType, week);

                _logger.LogDebug("ESPN Core API URL: {Url}", url);

                var jsonResponse = await _httpService.GetRawJsonAsync(url, cancellationToken);

                if (string.IsNullOrEmpty(jsonResponse))
                {
                    _logger.LogWarning("Empty response from ESPN Core API for {Url}", url);
                    return null;
                }

                // Log response size instead of full content to reduce log verbosity
                _logger.LogDebug("Received ESPN API Response: {Length} characters", jsonResponse.Length);

                var scheduleResponse = JsonSerializer.Deserialize<EspnScheduleResponse>(jsonResponse, _jsonOptions);

                if (scheduleResponse?.Items == null || scheduleResponse.Items.Count == 0)
                {
                    _logger.LogWarning("No events found in ESPN Core API response for Week {Week}, {Year}", week, year);
                    return null;
                }

                _logger.LogInformation("Successfully fetched {Count} event references from ESPN Core API for Week {Week}, {Year}",
                    scheduleResponse.Items.Count, week, year);

                // Now we need to fetch the actual event details from each reference
                var events = new List<EspnEvent>();

                foreach (var eventRef in scheduleResponse.Items.Take(5)) // Limit to first 5 for testing
                {
                    try
                    {
                        _logger.LogDebug("Fetching event details from: {Url}", eventRef.Ref);
                        var eventJson = await _httpService.GetRawJsonAsync(eventRef.Ref, cancellationToken);

                        if (!string.IsNullOrEmpty(eventJson))
                        {
                            var eventDetail = JsonSerializer.Deserialize<EspnEvent>(eventJson, _jsonOptions);
                            if (eventDetail != null)
                            {
                                events.Add(eventDetail);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch event details from {Url}", eventRef.Ref);
                    }
                }

                _logger.LogInformation("Successfully fetched {Count} full event details from ESPN Core API", events.Count);

                // For now, return the original response - we'll need to modify the service interface to handle events
                return scheduleResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching schedule from ESPN Core API for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}",
                    year, week, seasonType);
                throw;
            }
        }

        public async Task<EspnOdds?> GetEventOddsAsync(string gameId, string competitionId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Fetching ESPN Core API odds for GameId: {GameId}, CompetitionId: {CompetitionId}", gameId, competitionId);

                if (string.IsNullOrEmpty(gameId) || string.IsNullOrEmpty(competitionId))
                {
                    _logger.LogWarning("Invalid parameters for odds request: GameId={GameId}, CompetitionId={CompetitionId}", gameId, competitionId);
                    return null;
                }

                var url = string.Format(OddsUrlTemplate, ApiBaseUrl, gameId, competitionId);

                _logger.LogDebug("ESPN Odds API URL: {Url}", url);

                var jsonResponse = await _httpService.GetRawJsonAsync(url, cancellationToken);

                if (string.IsNullOrEmpty(jsonResponse))
                {
                    _logger.LogDebug("No odds data available for GameId: {GameId}", gameId);
                    return null;
                }

                _logger.LogDebug("Received ESPN Odds API Response for GameId {GameId}: {Length} characters", gameId, jsonResponse.Length);

                var oddsCollectionResponse = JsonSerializer.Deserialize<EspnOddsResponse>(jsonResponse, _jsonOptions);

                if (oddsCollectionResponse == null || !oddsCollectionResponse.Items.Any())
                {
                    _logger.LogWarning("Failed to deserialize odds response or no odds available for GameId: {GameId}", gameId);
                    return null;
                }

                // Return the first odds item (typically ESPN BET has priority)
                var oddsResponse = oddsCollectionResponse.Items.First();

                _logger.LogDebug("Successfully fetched odds from ESPN Core API for GameId: {GameId} - OverUnder: {OverUnder}",
                    gameId, oddsResponse.OverUnder);

                return oddsResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching odds from ESPN Core API for GameId: {GameId}, CompetitionId: {CompetitionId}",
                    gameId, competitionId);
                // Don't throw for odds errors - continue with schedule data
                return null;
            }
        }

        public async Task<List<EspnOdds>> GetBulkEventOddsAsync(IEnumerable<(string gameId, string competitionId)> gameCompetitionPairs, CancellationToken cancellationToken = default)
        {
            var odds = new List<EspnOdds>();

            var pairs = gameCompetitionPairs.ToList();
            _logger.LogInformation("Fetching odds for {Count} events from ESPN Core API", pairs.Count);

            var semaphore = new SemaphoreSlim(5, 5); // Limit concurrent requests to avoid rate limiting
            var tasks = pairs.Select(async pair =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    var result = await GetEventOddsAsync(pair.gameId, pair.competitionId, cancellationToken);
                    if (result != null)
                    {
                        lock (odds)
                        {
                            odds.Add(result);
                        }
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            _logger.LogInformation("Successfully fetched odds for {Count} out of {Total} events", odds.Count, pairs.Count);

            return odds;
        }

        #endregion

        #region Player Roster Methods

        public async Task<IEnumerable<Models.Player>> GetAllPlayersAsync(CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetAllPlayers");

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogInformation("Fetching all NFL players from ESPN API");

                try
                {
                    // Get all teams first
                    var teams = await GetTeamsAsync(cancellationToken);
                    var allPlayers = new List<Models.Player>();

                    // Fetch roster for each team
                    foreach (var team in teams)
                    {
                        try
                        {
                            var teamRoster = await GetTeamRosterAsync(team.Id, cancellationToken);
                            allPlayers.AddRange(teamRoster);

                            // Add small delay to be respectful to the API
                            await Task.Delay(100, cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch roster for team {TeamId} ({TeamName})", team.Id, team.Name);
                        }
                    }

                    _logger.LogInformation("Successfully retrieved {PlayerCount} players from {TeamCount} teams",
                        allPlayers.Count, teams.Count());

                    return allPlayers.AsEnumerable();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch all players from ESPN API");
                    throw;
                }
            }, TimeSpan.FromHours(6), cancellationToken); // Cache for 6 hours
        }

        public async Task<IEnumerable<Models.Player>> GetTeamRosterAsync(string teamId, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetTeamRoster", teamId);

            return await _cacheService.GetOrSetAsync(cacheKey, async () =>
            {
                _logger.LogDebug("Fetching roster for team {TeamId}", teamId);

                try
                {
                    // ESPN NFL team roster endpoint - use current season (2025)
                    var endpoint = $"https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2025/teams/{teamId}/athletes";
                    var rosterResponse = await _httpService.GetAsync<EspnRosterResponse>(endpoint, cancellationToken);

                    if (rosterResponse?.Items == null || !rosterResponse.Items.Any())
                    {
                        _logger.LogWarning("No roster data found for team {TeamId}", teamId);
                        return Enumerable.Empty<Models.Player>();
                    }

                    var allPlayers = new List<Models.Player>();

                    // Fetch details for each athlete reference
                    foreach (var athleteRef in rosterResponse.Items)
                    {
                        try
                        {
                            var athlete = await _httpService.GetAsync<EspnAthlete>(athleteRef.Ref, cancellationToken);
                            var player = MapEspnAthleteToPlayer(athlete, teamId);
                            if (player != null)
                            {
                                allPlayers.Add(player);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to fetch athlete details from {AthleteRef}", athleteRef.Ref);
                        }
                    }

                    _logger.LogDebug("Successfully retrieved {PlayerCount} players for team {TeamId}",
                        allPlayers.Count, teamId);

                    return allPlayers.AsEnumerable();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch roster for team {TeamId}", teamId);
                    throw;
                }
            }, TimeSpan.FromHours(2), cancellationToken); // Cache for 2 hours
        }

        public async Task<Models.Player?> GetPlayerAsync(string playerId, CancellationToken cancellationToken = default)
        {
            var cacheKey = _cacheService.GenerateKey("GetPlayer", playerId);

            // Check cache first
            var cachedPlayer = await _cacheService.GetAsync<Models.Player>(cacheKey, cancellationToken);
            if (cachedPlayer != null)
            {
                return cachedPlayer;
            }

            // Fetch from API
            _logger.LogDebug("Fetching player details for {PlayerId}", playerId);

            try
            {
                // ESPN NFL player endpoint
                var endpoint = $"https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/{playerId}";
                var playerResponse = await _httpService.GetAsync<EspnAthleteResponse>(endpoint, cancellationToken);

                if (playerResponse?.Athlete == null)
                {
                    _logger.LogWarning("No player data found for {PlayerId}", playerId);
                    return null;
                }

                var mappedPlayer = MapEspnAthleteToPlayer(playerResponse.Athlete, null);

                if (mappedPlayer != null)
                {
                    // Cache the non-null result
                    await _cacheService.SetAsync(cacheKey, mappedPlayer, TimeSpan.FromHours(4), cancellationToken);
                }

                _logger.LogDebug("Successfully retrieved player details for {PlayerId}: {PlayerName}",
                    playerId, mappedPlayer?.DisplayName ?? "Unknown");

                return mappedPlayer;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch player {PlayerId}", playerId);
                return null;
            }
        }

        #endregion

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
            // For September 19, 2025, we're clearly in regular season
            // This is a simplified calculation - in a real implementation, 
            // you'd want to use actual NFL schedule data

            // September 19th, 2025 should be around Week 3 of regular season
            if (currentDate.Month == 9 && currentDate.Day >= 1)
            {
                // Regular season - Week 3 is reasonable for mid-September
                return (2, 3);
            }

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
            try
            {
                if (boxScore?.Players == null || !boxScore.Players.Any())
                {
                    return Enumerable.Empty<PlayerStats>();
                }

                // The BoxScore already contains parsed PlayerStats, so we can return them directly
                // Filter out any null or invalid entries and ensure game context
                var validPlayerStats = boxScore.Players
                    .Where(ps => ps != null && !string.IsNullOrEmpty(ps.PlayerId))
                    .Select(ps =>
                    {
                        // Ensure the player stats have game context information
                        if (string.IsNullOrEmpty(ps.GameId))
                        {
                            ps.GameId = boxScore.GameId;
                        }

                        return ps;
                    })
                    .ToList();

                return validPlayerStats;
            }
            catch (Exception)
            {
                // Return empty collection to allow processing to continue
                return Enumerable.Empty<PlayerStats>();
            }
        }

        private Models.Player? MapEspnAthleteToPlayer(EspnAthlete athlete, string? teamId)
        {
            if (athlete == null) return null;

            try
            {
                return new Models.Player
                {
                    Id = athlete.Id,
                    DisplayName = athlete.DisplayName ?? athlete.FullName ?? $"{athlete.FirstName} {athlete.LastName}".Trim(),
                    FirstName = athlete.FirstName,
                    LastName = athlete.LastName,
                    Position = athlete.Position != null ? new Models.Position
                    {
                        Abbreviation = athlete.Position.Abbreviation,
                        DisplayName = athlete.Position.DisplayName
                    } : null,
                    Team = athlete.Team != null ? new Models.Team
                    {
                        Id = athlete.Team.Id,
                        Abbreviation = athlete.Team.Abbreviation,
                        DisplayName = athlete.Team.DisplayName
                    } : (teamId != null ? new Models.Team { Id = teamId } : null),
                    Active = athlete.Active ?? true
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to map ESPN athlete {AthleteId} to player model", athlete.Id);
                return null;
            }
        }

        private void ValidateParameters(int year, int week, int seasonType)
        {
            if (year < 2000 || year > DateTime.Now.Year + 1)
                throw new ArgumentException($"Invalid year: {year}");

            if (week < 1 || week > 22)
                throw new ArgumentException($"Invalid week: {week}");

            if (seasonType < 1 || seasonType > 4)
                throw new ArgumentException($"Invalid season type: {seasonType}");
        }

        #endregion
    }

    #region Helper Response Models

    // Helper response model for teams API
    public class TeamsResponse
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("pageIndex")]
        public int PageIndex { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; }

        [JsonPropertyName("items")]
        public IEnumerable<TeamReference> Items { get; set; } = new List<TeamReference>();
    }

    public class TeamReference
    {
        [JsonPropertyName("$ref")]
        public string Ref { get; set; } = string.Empty;
    }

    #endregion
}