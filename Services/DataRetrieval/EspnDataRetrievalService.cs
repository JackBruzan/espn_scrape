using ESPNScrape.Models;
using ESPNScrape.Models.Espn;
using ESPNScrape.Models.Supabase;
using ESPNScrape.Services.Interfaces;
using ESPNScrape.Services.DataProcessing;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ESPNScrape.Services.DataRetrieval
{
    /// <summary>
    /// Comprehensive service for retrieving and parsing ESPN data including scoreboards, box scores, and schedules
    /// </summary>
    public class EspnDataRetrievalService : IEspnScoreboardService, IEspnBoxScoreService, IEspnScheduleService
    {
        private readonly IEspnHttpService _httpService;
        private readonly ISupabaseDatabaseService _databaseService;
        private readonly IEspnCoreApiService _coreApiService;
        private readonly EspnDataMappingService _mappingService;
        private readonly ILogger<EspnDataRetrievalService> _logger;

        // ESPN URL templates
        private const string ScoreboardUrlTemplate = "https://www.espn.com/nfl/scoreboard/_/week/{0}/year/{1}/seasontype/{2}";
        private const string ScheduleUrlTemplate = "https://www.espn.com/nfl/schedule/_/week/{0}/year/{1}/seasontype/{2}";
        private const string BoxScoreUrlPattern = "https://www.espn.com/nfl/boxscore/_/gameId/{0}";
        private const string ApiBoxScorePattern = "https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={0}";

        // Regex patterns
        private static readonly Regex JsonExtractionRegex = new(@"window\['__espnfitt__'\]\s*=\s*({.*?});", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex BettingLineRegex = new(@"Line:\s*([A-Z]{2,3})\s*([-+]?\d+\.?\d*)", RegexOptions.Compiled);
        private static readonly Regex OverUnderRegex = new(@"O/U:\s*(\d+\.?\d*)", RegexOptions.Compiled);

        public EspnDataRetrievalService(
            IEspnHttpService httpService,
            ISupabaseDatabaseService databaseService,
            IEspnCoreApiService coreApiService,
            EspnDataMappingService mappingService,
            ILogger<EspnDataRetrievalService> logger)
        {
            _httpService = httpService;
            _databaseService = databaseService;
            _coreApiService = coreApiService;
            _mappingService = mappingService;
            _logger = logger;
        }

        #region Scoreboard Service Methods (IEspnScoreboardService)

        public async Task<ScoreboardData> GetScoreboardAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Fetching scoreboard for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}",
                    year, week, seasonType);

                ValidateParameters(year, week, seasonType);

                var url = string.Format(ScoreboardUrlTemplate, week, year, seasonType);
                var htmlContent = await _httpService.GetRawJsonAsync(url, cancellationToken);

                var jsonContent = ExtractEmbeddedJson(htmlContent);
                var scoreboardData = ParseScoreboardData(jsonContent);

                // Set week information from request parameters since ESPN structure doesn't provide it directly
                if (scoreboardData.Week.WeekNumber == 0)
                {
                    scoreboardData.Week.WeekNumber = week;
                    scoreboardData.Week.SeasonType = seasonType;
                    scoreboardData.Week.Year = year;
                    scoreboardData.Week.Text = $"Week {week}";
                    scoreboardData.Week.Label = $"Week {week}";
                    scoreboardData.Week.IsActive = true;
                }

                _logger.LogInformation("Successfully retrieved scoreboard data for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}. Found {EventCount} events",
                    year, week, seasonType, scoreboardData.Events.Count());

                return scoreboardData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve scoreboard for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}",
                    year, week, seasonType);
                throw;
            }
        }

        public Task<IEnumerable<GameEvent>> ExtractEventsAsync(ScoreboardData? scoreboard, CancellationToken cancellationToken = default)
        {
            try
            {
                if (scoreboard?.Events == null)
                {
                    _logger.LogWarning("No events found in scoreboard data");
                    return Task.FromResult(Enumerable.Empty<GameEvent>());
                }

                var events = scoreboard.Events.ToList();
                _logger.LogDebug("Extracted {EventCount} events from scoreboard", events.Count);

                return Task.FromResult<IEnumerable<GameEvent>>(events);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract events from scoreboard data");
                throw;
            }
        }

        public Task<Season> ExtractSeasonInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default)
        {
            try
            {
                if (scoreboard?.Season == null)
                {
                    _logger.LogWarning("No season information found in scoreboard data");
                    return Task.FromResult(new Season());
                }

                _logger.LogDebug("Extracted season information for year {Year}", scoreboard.Season.Year);
                return Task.FromResult(scoreboard.Season);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract season information from scoreboard");
                throw;
            }
        }

        public Task<Week> ExtractWeekInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default)
        {
            try
            {
                if (scoreboard?.Week == null)
                {
                    _logger.LogWarning("No week information found in scoreboard data");
                    return Task.FromResult(new Week());
                }

                _logger.LogDebug("Extracted week information: {WeekLabel}", scoreboard.Week.Label);
                return Task.FromResult(scoreboard.Week);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract week information from scoreboard");
                throw;
            }
        }

        #endregion

        #region Box Score Service Methods (IEspnBoxScoreService)

        public async Task<BoxScore?> GetBoxScoreDataAsync(string gameId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Fetching box score data for completed game {GameId}", gameId);

                var apiUrl = string.Format(ApiBoxScorePattern, gameId);
                var jsonData = await _httpService.GetRawJsonAsync(apiUrl, cancellationToken);

                var boxScore = await ParseBoxScoreFromJsonAsync(jsonData, cancellationToken);

                _logger.LogDebug("Successfully retrieved box score data for game {GameId}", gameId);
                return boxScore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get box score data for game {GameId}", gameId);
                return null;
            }
        }

        public async Task<BoxScore?> GetLiveBoxScoreDataAsync(string gameId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Fetching live box score data for game {GameId}", gameId);

                // For live games, we use the same API but expect partial data
                var apiUrl = string.Format(ApiBoxScorePattern, gameId);
                var jsonData = await _httpService.GetRawJsonAsync(apiUrl, cancellationToken);

                var boxScore = await ParseBoxScoreFromJsonAsync(jsonData, cancellationToken);

                _logger.LogDebug("Successfully retrieved live box score data for game {GameId}", gameId);
                return boxScore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get live box score data for game {GameId}", gameId);
                return null;
            }
        }

        #endregion

        #region Schedule Service Methods (IEspnScheduleService)

        public async Task<IEnumerable<Schedule>> GetWeeklyScheduleAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Fetching schedule for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}", year, week, seasonType);

                ValidateParameters(year, week, seasonType);

                var url = string.Format(ScheduleUrlTemplate, week, year, seasonType);
                var htmlContent = await _httpService.GetRawJsonAsync(url, cancellationToken);

                var schedules = ParseScheduleFromHtml(htmlContent, year, week, seasonType);

                _logger.LogInformation("Successfully parsed {Count} games for Week {Week}, {Year}",
                    schedules.Count(), week, year);

                return schedules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching schedule for Week {Week}, Year {Year}, SeasonType {SeasonType}",
                    week, year, seasonType);
                throw;
            }
        }

        public async Task<IEnumerable<Schedule>> GetWeeklyScheduleFromApiAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Fetching schedule from ESPN Core API for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}", year, week, seasonType);

                ValidateParameters(year, week, seasonType);

                // Get schedule data from ESPN Core API
                var espnResponse = await _coreApiService.GetWeeklyScheduleAsync(year, week, seasonType, cancellationToken);

                if (espnResponse?.Items == null || !espnResponse.Items.Any())
                {
                    _logger.LogWarning("No event references found from ESPN Core API for Week {Week}, {Year}", week, year);
                    return Enumerable.Empty<Schedule>();
                }

                _logger.LogInformation("ESPN API returned {Count} event references", espnResponse.Items.Count);

                // Convert ESPN events to Schedule objects
                var schedules = new List<Schedule>();

                foreach (var eventRef in espnResponse.Items)
                {
                    try
                    {
                        _logger.LogDebug("Fetching event details from: {Url}", eventRef.Ref);
                        var eventJson = await _httpService.GetRawJsonAsync(eventRef.Ref, cancellationToken);

                        if (!string.IsNullOrEmpty(eventJson))
                        {
                            var eventDetail = _mappingService.ConvertEspnEventToSchedule(eventJson, year, week, seasonType);
                            if (eventDetail != null)
                            {
                                schedules.Add(eventDetail);
                                _logger.LogDebug("Converted event to schedule: {AwayTeam} @ {HomeTeam}",
                                    eventDetail.AwayTeamName, eventDetail.HomeTeamName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to fetch event details from {Url}", eventRef.Ref);
                    }
                }

                // Try to get odds data for the schedules
                await ApplyOddsDataToSchedules(schedules, cancellationToken);

                _logger.LogInformation("Successfully processed {Count} schedules from ESPN Core API for Week {Week}, {Year}",
                    schedules.Count, week, year);

                return schedules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching schedule from ESPN Core API for Week {Week}, Year {Year}, SeasonType {SeasonType}",
                    week, year, seasonType);
                throw;
            }
        }

        public Schedule ParseSingleScheduleFromHtml(string gameHtml, int year, int week, int seasonType)
        {
            var schedule = new Schedule
            {
                Year = year,
                Week = week,
                SeasonType = seasonType
            };

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(gameHtml);

                // Extract team information
                var teamCells = doc.DocumentNode.SelectNodes(".//td");
                if (teamCells != null && teamCells.Count >= 2)
                {
                    var awayTeamCell = teamCells[0];
                    var homeTeamCell = teamCells[1];

                    schedule.AwayTeamName = awayTeamCell.InnerText?.Trim() ?? "";
                    schedule.HomeTeamName = homeTeamCell.InnerText?.Trim() ?? "";

                    _logger.LogDebug("Parsed team names: Away='{AwayTeam}', Home='{HomeTeam}'",
                        schedule.AwayTeamName, schedule.HomeTeamName);
                }

                // Extract game time
                var timeCell = doc.DocumentNode.SelectSingleNode(".//td[contains(@class, 'game-time') or position()=3]");
                if (timeCell != null)
                {
                    var timeText = timeCell.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(timeText))
                    {
                        schedule.GameTime = ParseGameTime(timeText, year, week);
                    }
                }

                // Extract betting information
                var bettingCell = doc.DocumentNode.SelectSingleNode(".//td[last()]");
                if (bettingCell != null)
                {
                    var bettingText = bettingCell.InnerText?.Trim();
                    if (!string.IsNullOrEmpty(bettingText))
                    {
                        ParseBettingInfo(bettingText, schedule);
                    }
                }

                // Calculate implied points
                schedule.CalculateImpliedPoints();

                // Generate ESPN game ID
                schedule.EspnGameId = GenerateGameId(schedule);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing schedule from HTML for Week {Week}, Year {Year}", week, year);
            }

            return schedule;
        }

        public async Task SaveScheduleDataAsync(IEnumerable<Schedule> schedules, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogInformation("Saving {Count} schedule records to database", schedules.Count());

                foreach (var schedule in schedules)
                {
                    // Resolve team names to team IDs using proper ESPN->DB mapping
                    if (!string.IsNullOrEmpty(schedule.HomeTeamName))
                    {
                        // First try the ESPN mapping service for abbreviations
                        var homeAbbreviation = _mappingService.ConvertEspnTeamAbbreviation(schedule.HomeTeamName);

                        // If the mapping service returns "UNK" or null, try converting from full team name
                        if (homeAbbreviation == "UNK" || string.IsNullOrEmpty(homeAbbreviation))
                        {
                            homeAbbreviation = ConvertTeamNameToAbbreviation(schedule.HomeTeamName);
                            // Apply the mapping to the converted abbreviation as well
                            homeAbbreviation = _mappingService.ConvertEspnTeamAbbreviation(homeAbbreviation) ?? homeAbbreviation;
                        }

                        schedule.HomeTeamId = await _databaseService.FindTeamIdByAbbreviationAsync(homeAbbreviation, cancellationToken);
                    }

                    if (!string.IsNullOrEmpty(schedule.AwayTeamName))
                    {
                        // First try the ESPN mapping service for abbreviations  
                        var awayAbbreviation = _mappingService.ConvertEspnTeamAbbreviation(schedule.AwayTeamName);

                        // If the mapping service returns "UNK" or null, try converting from full team name
                        if (awayAbbreviation == "UNK" || string.IsNullOrEmpty(awayAbbreviation))
                        {
                            awayAbbreviation = ConvertTeamNameToAbbreviation(schedule.AwayTeamName);
                            // Apply the mapping to the converted abbreviation as well
                            awayAbbreviation = _mappingService.ConvertEspnTeamAbbreviation(awayAbbreviation) ?? awayAbbreviation;
                        }

                        schedule.AwayTeamId = await _databaseService.FindTeamIdByAbbreviationAsync(awayAbbreviation, cancellationToken);
                    }

                    var scheduleRecord = new ScheduleRecord
                    {
                        espn_game_id = schedule.EspnGameId,
                        home_team_id = schedule.HomeTeamId,
                        away_team_id = schedule.AwayTeamId,
                        game_time = schedule.GameTime,
                        week = schedule.Week,
                        year = schedule.Year,
                        season_type = schedule.SeasonType,
                        betting_line = schedule.BettingLine,
                        over_under = schedule.OverUnder,
                        home_implied_points = schedule.HomeImpliedPoints,
                        away_implied_points = schedule.AwayImpliedPoints,
                        created_at = DateTime.UtcNow,
                        updated_at = DateTime.UtcNow
                    };

                    await _databaseService.SaveScheduleAsync(scheduleRecord, cancellationToken);
                }

                _logger.LogInformation("Successfully saved schedule data to database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving schedule data to database");
                throw;
            }
        }

        #endregion

        #region Private Helper Methods - Scoreboard

        private string ExtractEmbeddedJson(string htmlContent)
        {
            var match = JsonExtractionRegex.Match(htmlContent);
            if (!match.Success)
            {
                throw new InvalidOperationException("Could not extract embedded JSON from ESPN scoreboard HTML");
            }
            return match.Groups[1].Value;
        }

        private ScoreboardData ParseScoreboardData(string jsonContent)
        {
            try
            {
                using var jsonDocument = JsonDocument.Parse(jsonContent);
                var root = jsonDocument.RootElement;

                // Navigate to the scoreboard data
                if (!root.TryGetProperty("page", out var pageElement) ||
                    !pageElement.TryGetProperty("content", out var contentElement) ||
                    !contentElement.TryGetProperty("scoreboard", out var sbDataElement))
                {
                    throw new InvalidOperationException("Invalid ESPN JSON structure - could not find scoreboard data");
                }

                var scoreboardData = new ScoreboardData
                {
                    Events = ParseEvents(sbDataElement),
                    Season = ParseSeason(sbDataElement),
                    Week = ParseWeek(sbDataElement),
                    Leagues = ParseLeagues(sbDataElement)
                };

                return scoreboardData;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse ESPN JSON response");
                throw new InvalidOperationException("Failed to parse ESPN JSON response", ex);
            }
        }

        private IEnumerable<GameEvent> ParseEvents(JsonElement sbDataElement)
        {
            var events = new List<GameEvent>();

            // ESPN uses 'evts' instead of 'events' for the games array
            if (sbDataElement.TryGetProperty("evts", out var eventsElement) && eventsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var eventElement in eventsElement.EnumerateArray())
                {
                    try
                    {
                        var gameEvent = JsonSerializer.Deserialize<GameEvent>(eventElement.GetRawText());
                        if (gameEvent != null)
                        {
                            events.Add(gameEvent);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse individual event from ESPN response");
                    }
                }
            }
            else
            {
                _logger.LogWarning("No 'evts' property found in ESPN scoreboard data");
            }

            return events;
        }

        private Season ParseSeason(JsonElement sbDataElement)
        {
            // ESPN uses 'season' directly in scoreboard, not within 'leagues' array
            if (sbDataElement.TryGetProperty("season", out var seasonElement))
            {
                try
                {
                    return JsonSerializer.Deserialize<Season>(seasonElement.GetRawText()) ?? new Season();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse season information from ESPN response");
                }
            }

            return new Season();
        }

        private Week ParseWeek(JsonElement sbDataElement)
        {
            // ESPN calendar structure doesn't provide simple week access
            // For now, create a basic Week object - the actual week number
            // should be derived from the request parameters in GetScoreboardAsync
            return new Week();
        }

        private IEnumerable<League> ParseLeagues(JsonElement sbDataElement)
        {
            var leagues = new List<League>();

            if (sbDataElement.TryGetProperty("leagues", out var leaguesElement) &&
                leaguesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var leagueElement in leaguesElement.EnumerateArray())
                {
                    try
                    {
                        var league = JsonSerializer.Deserialize<League>(leagueElement.GetRawText());
                        if (league != null)
                        {
                            leagues.Add(league);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse individual league from ESPN response");
                    }
                }
            }

            return leagues;
        }

        #endregion

        #region Private Helper Methods - Box Score

        private Task<BoxScore?> ParseBoxScoreFromJsonAsync(string jsonData, CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrEmpty(jsonData))
                {
                    _logger.LogWarning("Empty JSON data received for box score parsing");
                    return Task.FromResult<BoxScore?>(null);
                }

                using var document = JsonDocument.Parse(jsonData);
                var root = document.RootElement;

                var boxScore = new BoxScore();

                // Extract basic game information
                if (root.TryGetProperty("header", out var header))
                {
                    if (header.TryGetProperty("id", out var gameId))
                    {
                        boxScore.GameId = gameId.GetString() ?? string.Empty;
                    }

                    if (header.TryGetProperty("competitions", out var competitions) &&
                        competitions.ValueKind == JsonValueKind.Array)
                    {
                        var competition = competitions.EnumerateArray().FirstOrDefault();
                        if (competition.ValueKind != JsonValueKind.Undefined)
                        {
                            ParseCompetitionData(competition, boxScore);
                        }
                    }
                }

                // Extract player statistics
                if (root.TryGetProperty("boxscore", out var boxscoreElement))
                {
                    boxScore.Players = ParsePlayerStats(boxscoreElement).ToList();
                }
                else if (root.TryGetProperty("rosters", out var rostersElement))
                {
                    // Alternative structure - some ESPN APIs use 'rosters'
                    boxScore.Players = ParsePlayerStatsFromRosters(rostersElement).ToList();
                }

                _logger.LogDebug("Parsed box score with {PlayerCount} player stat records", boxScore.Players?.Count() ?? 0);

                return Task.FromResult<BoxScore?>(boxScore);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse box score JSON data");
                return Task.FromResult<BoxScore?>(null);
            }
        }

        private void ParseCompetitionData(JsonElement competition, BoxScore boxScore)
        {
            if (competition.TryGetProperty("competitors", out var competitors) &&
                competitors.ValueKind == JsonValueKind.Array)
            {
                foreach (var competitor in competitors.EnumerateArray())
                {
                    if (competitor.TryGetProperty("team", out var team))
                    {
                        var teamAbbr = team.TryGetProperty("abbreviation", out var abbr) ? abbr.GetString() : "";
                        var teamId = team.TryGetProperty("id", out var id) ? id.GetString() : "";
                        var teamName = team.TryGetProperty("displayName", out var name) ? name.GetString() : "";
                        var isHome = competitor.TryGetProperty("homeAway", out var homeAway) &&
                                   homeAway.GetString() == "home";

                        var teamBoxScore = new TeamBoxScore
                        {
                            Team = new Models.Espn.Team
                            {
                                Id = teamId ?? "",
                                Abbreviation = teamAbbr ?? "",
                                DisplayName = teamName ?? ""
                            }
                        };

                        boxScore.Teams.Add(teamBoxScore);
                    }
                }
            }

            // Extract game status and score
            if (competition.TryGetProperty("status", out var status))
            {
                if (status.TryGetProperty("type", out var statusType))
                {
                    if (statusType.TryGetProperty("name", out var statusName))
                    {
                        // Game status would be stored in GameInfo if needed
                        // boxScore.GameInfo could be updated with status information
                    }
                }
            }
        }

        private IEnumerable<PlayerStats> ParsePlayerStats(JsonElement boxscoreElement)
        {
            var playerStats = new List<PlayerStats>();

            if (boxscoreElement.TryGetProperty("teams", out var teams) &&
                teams.ValueKind == JsonValueKind.Array)
            {
                foreach (var team in teams.EnumerateArray())
                {
                    if (team.TryGetProperty("team", out var teamInfo))
                    {
                        var teamAbbr = teamInfo.TryGetProperty("abbreviation", out var abbr) ? abbr.GetString() : "";

                        if (team.TryGetProperty("statistics", out var statistics) &&
                            statistics.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var statCategory in statistics.EnumerateArray())
                            {
                                var categoryStats = ParseStatCategory(statCategory, teamAbbr ?? "UNKNOWN");
                                playerStats.AddRange(categoryStats);
                            }
                        }
                    }
                }
            }

            return playerStats;
        }

        private IEnumerable<PlayerStats> ParsePlayerStatsFromRosters(JsonElement rostersElement)
        {
            var playerStats = new List<PlayerStats>();
            // Implementation for alternative roster-based structure
            // This would depend on the specific JSON structure ESPN provides
            return playerStats;
        }

        private IEnumerable<PlayerStats> ParseStatCategory(JsonElement statCategory, string teamAbbr)
        {
            var playerStats = new List<PlayerStats>();

            if (statCategory.TryGetProperty("name", out var categoryName) &&
                statCategory.TryGetProperty("athletes", out var athletes) &&
                athletes.ValueKind == JsonValueKind.Array)
            {
                var category = categoryName.GetString() ?? "";

                foreach (var athlete in athletes.EnumerateArray())
                {
                    var playerStat = ParseIndividualPlayerStat(athlete, category, teamAbbr);
                    if (playerStat != null)
                    {
                        playerStats.Add(playerStat);
                    }
                }
            }

            return playerStats;
        }

        private PlayerStats? ParseIndividualPlayerStat(JsonElement athlete, string category, string teamAbbr)
        {
            try
            {
                var playerStats = new PlayerStats
                {
                    Team = new Models.Espn.Team { Abbreviation = teamAbbr }
                };

                if (athlete.TryGetProperty("athlete", out var athleteInfo))
                {
                    if (athleteInfo.TryGetProperty("id", out var playerId))
                    {
                        playerStats.PlayerId = playerId.GetString() ?? "";
                    }

                    if (athleteInfo.TryGetProperty("displayName", out var playerName))
                    {
                        playerStats.DisplayName = playerName.GetString() ?? "";
                    }

                    if (athleteInfo.TryGetProperty("position", out var position))
                    {
                        if (position.TryGetProperty("abbreviation", out var posAbbr))
                        {
                            playerStats.Position = new PlayerPosition { Abbreviation = posAbbr.GetString() ?? "" };
                        }
                    }
                }

                if (athlete.TryGetProperty("stats", out var stats) &&
                    stats.ValueKind == JsonValueKind.Array)
                {
                    var statValues = new List<string>();
                    foreach (var stat in stats.EnumerateArray())
                    {
                        statValues.Add(stat.GetString() ?? "0");
                    }
                    // Convert stat values to PlayerStatistic objects
                    for (int i = 0; i < statValues.Count; i++)
                    {
                        if (decimal.TryParse(statValues[i], out var numValue))
                        {
                            playerStats.Statistics.Add(new PlayerStatistic
                            {
                                Name = $"stat_{i}",
                                Value = numValue,
                                DisplayValue = statValues[i]
                            });
                        }
                    }
                }

                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse individual player stat for category {Category}", category);
                return null;
            }
        }

        #endregion

        #region Private Helper Methods - Schedule

        private async Task ApplyOddsDataToSchedules(IEnumerable<Schedule> schedules, CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogDebug("Applying odds data to {Count} schedules", schedules.Count());

                var gameCompetitionPairs = schedules
                    .Where(s => !string.IsNullOrEmpty(s.EspnGameId) && !string.IsNullOrEmpty(s.EspnCompetitionId))
                    .Select(s => (s.EspnGameId, s.EspnCompetitionId))
                    .ToList();

                if (!gameCompetitionPairs.Any())
                {
                    _logger.LogWarning("No valid game/competition pairs found for odds data");
                    return;
                }

                var oddsData = await _coreApiService.GetBulkEventOddsAsync(gameCompetitionPairs, cancellationToken);

                foreach (var schedule in schedules)
                {
                    // TODO: Fix odds matching logic - EspnOdds model needs GameId property or use different matching approach
                    var matchingOdds = oddsData.FirstOrDefault(); // Temporary placeholder
                    if (matchingOdds != null)
                    {
                        _mappingService.ApplyOddsToSchedule(schedule, matchingOdds);
                        _logger.LogDebug("Applied odds to game {GameId}: Spread={Spread}, O/U={OverUnder}",
                            schedule.EspnGameId, matchingOdds.Spread, matchingOdds.OverUnder);
                    }
                }

                _logger.LogInformation("Applied odds data to {Count} games", schedules.Count());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying odds data to schedules");
                // Don't throw - continue with schedule data even if odds fail
            }
        }

        private IEnumerable<Schedule> ParseScheduleFromHtml(string htmlContent, int year, int week, int seasonType)
        {
            var schedules = new List<Schedule>();

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                // Look for table rows or schedule entries in the ESPN page
                var pageText = doc.DocumentNode.InnerText;

                // Split by lines and look for game patterns
                var lines = pageText.Split('\n', StringSplitOptions.RemoveEmptyEntries);

                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();

                    // Look for patterns like "Team @ Team" or "Team at Team"
                    if (trimmedLine.Contains(" @ ") || trimmedLine.Contains(" at "))
                    {
                        var schedule = ParseScheduleFromLine(trimmedLine, year, week, seasonType);
                        if (schedule != null && !string.IsNullOrEmpty(schedule.HomeTeamName) && !string.IsNullOrEmpty(schedule.AwayTeamName))
                        {
                            schedules.Add(schedule);
                        }
                    }

                    // Also look for betting line patterns in the same or nearby lines
                    if (trimmedLine.Contains("Line:") && trimmedLine.Contains("O/U:"))
                    {
                        // Try to associate betting info with the last parsed schedule
                        if (schedules.Any())
                        {
                            var lastSchedule = schedules.Last();
                            ParseBettingInfo(trimmedLine, lastSchedule);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing schedule HTML content");
            }

            return schedules;
        }

        private Schedule? ParseScheduleFromLine(string line, int year, int week, int seasonType)
        {
            try
            {
                // Parse patterns like "Seattle @ Arizona" or "Minnesota at Pittsburgh"
                var atMatch = Regex.Match(line, @"([A-Za-z\s]+)\s+@\s+([A-Za-z\s]+)");
                if (!atMatch.Success)
                {
                    atMatch = Regex.Match(line, @"([A-Za-z\s]+)\s+at\s+([A-Za-z\s]+)");
                }

                if (atMatch.Success)
                {
                    var awayTeam = atMatch.Groups[1].Value.Trim();
                    var homeTeam = atMatch.Groups[2].Value.Trim();

                    _logger.LogDebug("Raw team names from ESPN: Away='{AwayTeam}', Home='{HomeTeam}'", awayTeam, homeTeam);

                    var schedule = new Schedule
                    {
                        AwayTeamName = awayTeam,
                        HomeTeamName = homeTeam,
                        Year = year,
                        Week = week,
                        SeasonType = seasonType,
                        GameTime = DateTime.UtcNow // Default, will be updated if time found
                    };

                    _logger.LogDebug("Parsed game from line: {AwayTeam} @ {HomeTeam}", awayTeam, homeTeam);

                    // Generate game ID
                    schedule.EspnGameId = GenerateGameId(schedule);

                    return schedule;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing schedule line: {Line}", line);
            }

            return null;
        }

        private void ParseBettingInfo(string bettingText, Schedule schedule)
        {
            // Parse betting line (e.g., "Line: SEA -1.5")
            var lineMatch = BettingLineRegex.Match(bettingText);
            if (lineMatch.Success)
            {
                var team = lineMatch.Groups[1].Value;
                var line = decimal.Parse(lineMatch.Groups[2].Value);

                // Determine if home or away team is favored
                var isHomeFavored = schedule.HomeTeamName.Contains(team, StringComparison.OrdinalIgnoreCase);
                schedule.BettingLine = isHomeFavored ? line : -line;
            }

            // Parse over/under (e.g., "O/U: 43.5")
            var ouMatch = OverUnderRegex.Match(bettingText);
            if (ouMatch.Success && decimal.TryParse(ouMatch.Groups[1].Value, out var overUnder))
            {
                schedule.OverUnder = overUnder;
            }
        }

        private DateTime ParseGameTime(string timeText, int year, int week)
        {
            try
            {
                // Parse time format like "7:15 PM" or "12:00 PM"
                if (DateTime.TryParseExact(timeText, new[] { "h:mm tt", "hh:mm tt" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
                {
                    // Calculate game date based on week
                    var weekStartDate = GetWeekStartDate(year, week);
                    return weekStartDate.Add(time.TimeOfDay);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing game time: {TimeText}", timeText);
            }

            return DateTime.UtcNow; // Fallback
        }

        private DateTime GetWeekStartDate(int year, int week)
        {
            // NFL season typically starts first week of September
            var seasonStart = new DateTime(year, 9, 1);

            // Find first Thursday of September (typical NFL season start)
            while (seasonStart.DayOfWeek != DayOfWeek.Thursday)
            {
                seasonStart = seasonStart.AddDays(1);
            }

            // Add weeks to get to the requested week
            return seasonStart.AddDays((week - 1) * 7);
        }

        private string GenerateGameId(Schedule schedule)
        {
            var awayTeam = schedule.AwayTeamId?.ToString() ?? ConvertTeamNameToAbbreviation(schedule.AwayTeamName);
            var homeTeam = schedule.HomeTeamId?.ToString() ?? ConvertTeamNameToAbbreviation(schedule.HomeTeamName);

            return $"{schedule.Year}_{schedule.Week}_{schedule.SeasonType}_{awayTeam}_{homeTeam}"
                .Replace(" ", "_")
                .ToUpperInvariant();
        }

        #endregion

        #region Private Helper Methods - Team Mapping

        /// <summary>
        /// Maps ESPN team abbreviations to database team full names
        /// </summary>
        private static string MapEspnTeamToFullName(string espnAbbreviation)
        {
            return espnAbbreviation?.ToUpper() switch
            {
                "ARI" => "Arizona Cardinals",
                "ATL" => "Atlanta Falcons",
                "BAL" => "Baltimore Ravens",
                "BUF" => "Buffalo Bills",
                "CAR" => "Carolina Panthers",
                "CHI" => "Chicago Bears",
                "CIN" => "Cincinnati Bengals",
                "CLE" => "Cleveland Browns",
                "DAL" => "Dallas Cowboys",
                "DEN" => "Denver Broncos",
                "DET" => "Detroit Lions",
                "GB" => "Green Bay Packers",
                "HOU" => "Houston Texans",
                "IND" => "Indianapolis Colts",
                "JAX" => "Jacksonville Jaguars",
                "KC" => "Kansas City Chiefs",
                "LAC" => "Los Angeles Chargers",
                "LAR" => "Los Angeles Rams",
                "LV" => "Las Vegas Raiders",
                "MIA" => "Miami Dolphins",
                "MIN" => "Minnesota Vikings",
                "NE" => "New England Patriots",
                "NO" => "New Orleans Saints",
                "NYG" => "New York Giants",
                "NYJ" => "New York Jets",
                "PHI" => "Philadelphia Eagles",
                "PIT" => "Pittsburgh Steelers",
                "SEA" => "Seattle Seahawks",
                "SF" => "San Francisco 49ers",
                "TB" => "Tampa Bay Buccaneers",
                "TEN" => "Tennessee Titans",
                "WAS" => "Washington Commanders",
                _ => espnAbbreviation ?? string.Empty
            };
        }

        /// <summary>
        /// Converts full team names to standard NFL abbreviations that match the database
        /// </summary>
        private string ConvertTeamNameToAbbreviation(string teamName)
        {
            var result = teamName.ToUpperInvariant().Trim() switch
            {
                "ARIZONA" or "ARIZONA CARDINALS" => "ARI",
                "ATLANTA" or "ATLANTA FALCONS" => "ATL",
                "BALTIMORE" or "BALTIMORE RAVENS" => "BAL",
                "BUFFALO" or "BUFFALO BILLS" => "BUF",
                "CAROLINA" or "CAROLINA PANTHERS" => "CAR",
                "CHICAGO" or "CHICAGO BEARS" => "CHI",
                "CINCINNATI" or "CINCINNATI BENGALS" => "CIN",
                "CLEVELAND" or "CLEVELAND BROWNS" => "CLE",
                "DALLAS" or "DALLAS COWBOYS" => "DAL",
                "DENVER" or "DENVER BRONCOS" => "DEN",
                "DETROIT" or "DETROIT LIONS" => "DET",
                "GREEN BAY" or "GREEN BAY PACKERS" => "GB", // Will be mapped to GNB by ConvertEspnTeamAbbreviation 
                "HOUSTON" or "HOUSTON TEXANS" => "HOU",
                "INDIANAPOLIS" or "INDIANAPOLIS COLTS" => "IND",
                "JACKSONVILLE" or "JACKSONVILLE JAGUARS" => "JAX",
                "KANSAS CITY" or "KANSAS CITY CHIEFS" => "KC", // Will be mapped to KAN by ConvertEspnTeamAbbreviation
                "LAS VEGAS" or "LAS VEGAS RAIDERS" => "LV", // Will be mapped to LVR by ConvertEspnTeamAbbreviation
                "LOS ANGELES CHARGERS" or "LA CHARGERS" => "LAC",
                "LOS ANGELES RAMS" or "LA RAMS" => "LAR",
                "LOS ANGELES" => "LAR", // Default to Rams for ambiguous LA
                "MIAMI" or "MIAMI DOLPHINS" => "MIA",
                "MINNESOTA" or "MINNESOTA VIKINGS" => "MIN",
                "NEW ENGLAND" or "NEW ENGLAND PATRIOTS" => "NE", // Will be mapped to NWE by ConvertEspnTeamAbbreviation
                "NEW ORLEANS" or "NEW ORLEANS SAINTS" => "NO", // Will be mapped to NOR by ConvertEspnTeamAbbreviation
                "NEW YORK GIANTS" or "NY GIANTS" => "NYG",
                "NEW YORK JETS" or "NY JETS" => "NYJ",
                "NEW YORK" => "NYG", // Default to Giants for ambiguous NY
                "PHILADELPHIA" or "PHILADELPHIA EAGLES" => "PHI",
                "PITTSBURGH" or "PITTSBURGH STEELERS" => "PIT",
                "SAN FRANCISCO" or "SAN FRANCISCO 49ERS" => "SF", // Will be mapped to SFO by ConvertEspnTeamAbbreviation
                "SEATTLE" or "SEATTLE SEAHAWKS" => "SEA",
                "TAMPA BAY" or "TAMPA BAY BUCCANEERS" => "TB", // Will be mapped to TAM by ConvertEspnTeamAbbreviation
                "TENNESSEE" or "TENNESSEE TITANS" => "TEN",
                "WASHINGTON" or "WASHINGTON COMMANDERS" => "WAS",
                _ => teamName.Length <= 3 ? teamName.ToUpperInvariant() : "UNK"
            };

            if (result == "UNK")
            {
                _logger.LogWarning("Could not map team name '{TeamName}' to abbreviation", teamName);
            }

            return result;
        }

        #endregion

        #region Private Helper Methods - Validation

        private void ValidateParameters(int year, int week, int seasonType)
        {
            if (year < 2000 || year > DateTime.Now.Year + 1)
            {
                throw new ArgumentException($"Invalid year: {year}. Must be between 2000 and {DateTime.Now.Year + 1}", nameof(year));
            }

            if (seasonType < 1 || seasonType > 3)
            {
                throw new ArgumentException($"Invalid season type: {seasonType}. Must be 1 (Preseason), 2 (Regular), or 3 (Postseason)", nameof(seasonType));
            }

            // Week validation based on season type
            switch (seasonType)
            {
                case 1: // Preseason
                    if (week < 1 || week > 4)
                        throw new ArgumentException($"Invalid week for preseason: {week}. Must be between 1 and 4", nameof(week));
                    break;
                case 2: // Regular season
                    if (week < 1 || week > 18)
                        throw new ArgumentException($"Invalid week for regular season: {week}. Must be between 1 and 18", nameof(week));
                    break;
                case 3: // Postseason
                    if (week < 1 || week > 5)
                        throw new ArgumentException($"Invalid week for postseason: {week}. Must be between 1 and 5", nameof(week));
                    break;
            }
        }

        #endregion

        #region Missing Interface Implementation Methods

        /// <summary>
        /// Get event references from scoreboard data
        /// </summary>
        public async Task<IEnumerable<string>> GetEventReferencesAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Placeholder to make it async

            var eventRefs = new List<string>();
            if (scoreboard.Events != null)
            {
                eventRefs.AddRange(scoreboard.Events.Select(e => e.Id));
            }
            return eventRefs;
        }

        /// <summary>
        /// Parse team statistics from box score JSON
        /// </summary>
        public async Task<(TeamBoxScore homeTeam, TeamBoxScore awayTeam)?> ParseTeamStatsAsync(string boxScoreJson, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Placeholder to make it async

            try
            {
                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                // This would need to be implemented based on the actual JSON structure
                // For now, return placeholder data
                var homeTeam = new TeamBoxScore { Team = new Models.Espn.Team { Id = "home" } };
                var awayTeam = new TeamBoxScore { Team = new Models.Espn.Team { Id = "away" } };

                return (homeTeam, awayTeam);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Extract game metadata from box score JSON
        /// </summary>
        public async Task<GameInfo?> ExtractGameMetadataAsync(string boxScoreJson, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Placeholder to make it async

            try
            {
                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                // This would need to be implemented based on the actual JSON structure
                // For now, return placeholder data
                return new GameInfo
                {
                    Attendance = null,
                    Venue = new Venue()
                };
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get box score URL for a game
        /// </summary>
        public string GetBoxScoreUrl(string gameId)
        {
            return string.Format(BoxScoreUrlPattern, gameId);
        }

        /// <summary>
        /// Parse player statistics from box score JSON
        /// </summary>
        public async Task<List<PlayerStats>> ParsePlayerStatsAsync(string boxScoreJson, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Placeholder to make it async

            var playerStats = new List<PlayerStats>();

            try
            {
                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                // This would need to be implemented based on the actual JSON structure
                // For now, return empty list

                return playerStats;
            }
            catch
            {
                return playerStats;
            }
        }

        /// <summary>
        /// Get team offensive statistics
        /// </summary>
        public async Task<Dictionary<string, object>?> GetTeamOffensiveStatsAsync(string gameId, string teamId, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Placeholder to make it async

            // This would need to be implemented to fetch and parse team offensive stats
            // For now, return placeholder data
            return new Dictionary<string, object>
            {
                ["TeamId"] = teamId,
                ["GameId"] = gameId,
                ["TotalYards"] = 0,
                ["PassingYards"] = 0,
                ["RushingYards"] = 0
            };
        }

        /// <summary>
        /// Get team defensive statistics
        /// </summary>
        public async Task<Dictionary<string, object>?> GetTeamDefensiveStatsAsync(string gameId, string teamId, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // Placeholder to make it async

            // This would need to be implemented to fetch and parse team defensive stats
            // For now, return placeholder data
            return new Dictionary<string, object>
            {
                ["TeamId"] = teamId,
                ["GameId"] = gameId,
                ["Sacks"] = 0,
                ["Interceptions"] = 0,
                ["ForcedFumbles"] = 0
            };
        }

        /// <summary>
        /// Check if game is currently live
        /// </summary>
        public async Task<bool> IsGameLiveAsync(string gameId, CancellationToken cancellationToken = default)
        {
            try
            {
                var boxScoreData = await GetBoxScoreDataAsync(gameId, cancellationToken);

                // Check game status to determine if live
                // This would need to be implemented based on the actual game status structure
                // For now, return false (game is not live)
                return false;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}