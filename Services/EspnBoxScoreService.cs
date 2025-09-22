using ESPNScrape.Models.Espn;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Service for accessing ESPN box score data
    /// </summary>
    public class EspnBoxScoreService : IEspnBoxScoreService
    {
        private readonly IEspnHttpService _httpService;
        private readonly ILogger<EspnBoxScoreService> _logger;

        private const string BoxScoreUrlPattern = "https://www.espn.com/nfl/boxscore/_/gameId/{0}";
        private const string ApiBoxScorePattern = "https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event={0}";

        public EspnBoxScoreService(IEspnHttpService httpService, ILogger<EspnBoxScoreService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

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

                if (boxScore != null)
                {
                    _logger.LogDebug("Successfully retrieved live box score data for game {GameId}", gameId);
                }

                return boxScore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get live box score data for game {GameId}", gameId);
                return null;
            }
        }

        public async Task<(TeamBoxScore homeTeam, TeamBoxScore awayTeam)?> ParseTeamStatsAsync(string boxScoreJson, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Parsing team statistics from box score JSON");

                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("boxscore", out var boxscoreElement) ||
                    !boxscoreElement.TryGetProperty("teams", out var teamsElement))
                {
                    _logger.LogWarning("Box score JSON does not contain expected team data structure");
                    return null;
                }

                var teamsArray = teamsElement.ValueKind == JsonValueKind.Array
                    ? teamsElement.EnumerateArray().ToArray()
                    : Array.Empty<JsonElement>();
                if (teamsArray.Length != 2)
                {
                    _logger.LogWarning("Expected 2 teams in box score, found {Count}", teamsArray.Length);
                    return null;
                }

                var awayTeam = await ParseSingleTeamStatsAsync(teamsArray[0], cancellationToken);
                var homeTeam = await ParseSingleTeamStatsAsync(teamsArray[1], cancellationToken);

                if (awayTeam != null && homeTeam != null)
                {
                    _logger.LogDebug("Successfully parsed team statistics");
                    return (homeTeam, awayTeam);
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse team statistics from box score JSON");
                return null;
            }
        }

        public Task<GameInfo?> ExtractGameMetadataAsync(string boxScoreJson, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Extracting game metadata from box score JSON");

                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("header", out var headerElement))
                {
                    _logger.LogWarning("Box score JSON does not contain header information");
                    return Task.FromResult<GameInfo?>(null);
                }

                var gameInfo = new GameInfo();

                // Extract attendance
                if (headerElement.TryGetProperty("attendance", out var attendanceElement))
                {
                    gameInfo.Attendance = attendanceElement.GetInt32();
                }

                // Extract officials
                if (headerElement.TryGetProperty("officials", out var officialsElement))
                {
                    var officials = new List<Official>();
                    if (officialsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var officialElement in officialsElement.EnumerateArray())
                        {
                            var official = new Official();
                            if (officialElement.TryGetProperty("displayName", out var nameElement))
                            {
                                official.DisplayName = nameElement.GetString() ?? string.Empty;
                            }
                            if (officialElement.TryGetProperty("position", out var positionElement))
                            {
                                official.Position = positionElement.GetString() ?? string.Empty;
                            }
                            officials.Add(official);
                        }
                    }
                    gameInfo.Officials = officials;
                }

                // Extract weather
                if (headerElement.TryGetProperty("weather", out var weatherElement))
                {
                    var weather = new Weather();
                    if (weatherElement.TryGetProperty("temperature", out var tempElement))
                    {
                        weather.Temperature = tempElement.GetInt32();
                    }
                    if (weatherElement.TryGetProperty("conditions", out var conditionsElement))
                    {
                        weather.Conditions = conditionsElement.GetString() ?? string.Empty;
                    }
                    if (weatherElement.TryGetProperty("windSpeed", out var windElement))
                    {
                        weather.WindSpeed = windElement.GetString() ?? string.Empty;
                    }
                    if (weatherElement.TryGetProperty("humidity", out var humidityElement))
                    {
                        weather.Humidity = humidityElement.GetString() ?? string.Empty;
                    }
                    gameInfo.Weather = weather;
                }

                // Extract venue
                if (headerElement.TryGetProperty("venue", out var venueElement))
                {
                    var venue = new Venue();
                    if (venueElement.TryGetProperty("fullName", out var venueNameElement))
                    {
                        venue.FullName = venueNameElement.GetString() ?? string.Empty;
                    }
                    if (venueElement.TryGetProperty("address", out var addressElement))
                    {
                        var address = new VenueAddress();
                        if (addressElement.TryGetProperty("city", out var cityElement))
                        {
                            address.City = cityElement.GetString() ?? string.Empty;
                        }
                        if (addressElement.TryGetProperty("state", out var stateElement))
                        {
                            address.State = stateElement.GetString() ?? string.Empty;
                        }
                        venue.Address = address;
                    }
                    gameInfo.Venue = venue;
                }

                _logger.LogDebug("Successfully extracted game metadata");
                return Task.FromResult<GameInfo?>(gameInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract game metadata from box score JSON");
                return Task.FromResult<GameInfo?>(null);
            }
        }

        public string GetBoxScoreUrl(string gameId)
        {
            return string.Format(BoxScoreUrlPattern, gameId);
        }

        public async Task<List<PlayerStats>> ParsePlayerStatsAsync(string boxScoreJson, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // For async signature consistency

            try
            {
                _logger.LogDebug("Parsing player statistics from boxscore JSON");

                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                var allPlayerStats = new List<PlayerStats>();

                // Extract game metadata from root level
                var gameId = string.Empty;
                var season = 0;
                var week = 0;

                // Try to get game ID from header.competitions[0].id
                if (root.TryGetProperty("header", out var headerElement))
                {
                    // Get game ID from header.id
                    if (headerElement.TryGetProperty("id", out var gameIdElement))
                    {
                        gameId = gameIdElement.GetString() ?? string.Empty;
                    }

                    // Get season from header.season.year
                    if (headerElement.TryGetProperty("season", out var seasonElement) &&
                        seasonElement.TryGetProperty("year", out var yearElement))
                    {
                        season = yearElement.GetInt32();
                    }

                    // Get week from header.week
                    if (headerElement.TryGetProperty("week", out var weekElement))
                    {
                        week = weekElement.GetInt32();
                    }
                }

                _logger.LogDebug("Extracted game metadata: GameId={GameId}, Season={Season}, Week={Week}",
                    gameId, season, week);

                // Parse from boxscore.players structure
                if (!root.TryGetProperty("boxscore", out var boxscoreElement) ||
                    !boxscoreElement.TryGetProperty("players", out var playersElement))
                {
                    _logger.LogWarning("Box score JSON does not contain expected players data structure");
                    return allPlayerStats;
                }

                if (playersElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogWarning("Players element is not an array: {ValueKind}", playersElement.ValueKind);
                    return allPlayerStats;
                }

                // Loop through each team's players
                foreach (var teamPlayersElement in playersElement.EnumerateArray())
                {
                    // Get team information
                    var team = new Team();
                    if (teamPlayersElement.TryGetProperty("team", out var teamElement))
                    {
                        if (teamElement.TryGetProperty("id", out var teamIdElement))
                        {
                            team.Id = teamIdElement.GetString() ?? string.Empty;
                        }
                        if (teamElement.TryGetProperty("displayName", out var teamNameElement))
                        {
                            team.DisplayName = teamNameElement.GetString() ?? string.Empty;
                        }
                        if (teamElement.TryGetProperty("abbreviation", out var teamAbbrevElement))
                        {
                            team.Abbreviation = teamAbbrevElement.GetString() ?? string.Empty;
                        }
                    }

                    // Parse statistics groups (passing, rushing, receiving, etc.)
                    if (teamPlayersElement.TryGetProperty("statistics", out var statisticsElement) &&
                        statisticsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var statGroupElement in statisticsElement.EnumerateArray())
                        {
                            // Get stat group info (passing, rushing, etc.)
                            var statGroupName = string.Empty;
                            var statKeys = new List<string>();
                            var statLabels = new List<string>();
                            var statDescriptions = new List<string>();

                            if (statGroupElement.TryGetProperty("name", out var nameElement))
                            {
                                statGroupName = nameElement.GetString() ?? string.Empty;
                            }

                            if (statGroupElement.TryGetProperty("keys", out var keysElement))
                            {
                                if (keysElement.ValueKind == JsonValueKind.Array)
                                {
                                    statKeys = keysElement.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();
                                }
                                else if (keysElement.ValueKind == JsonValueKind.String)
                                {
                                    statKeys = keysElement.GetString()?.Split(' ').ToList() ?? new List<string>();
                                }
                            }

                            if (statGroupElement.TryGetProperty("labels", out var labelsElement))
                            {
                                if (labelsElement.ValueKind == JsonValueKind.Array)
                                {
                                    statLabels = labelsElement.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();
                                }
                                else if (labelsElement.ValueKind == JsonValueKind.String)
                                {
                                    statLabels = labelsElement.GetString()?.Split(' ').ToList() ?? new List<string>();
                                }
                            }

                            if (statGroupElement.TryGetProperty("descriptions", out var descriptionsElement))
                            {
                                if (descriptionsElement.ValueKind == JsonValueKind.Array)
                                {
                                    statDescriptions = descriptionsElement.EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList();
                                }
                                else if (descriptionsElement.ValueKind == JsonValueKind.String)
                                {
                                    statDescriptions = descriptionsElement.GetString()?.Split(' ').ToList() ?? new List<string>();
                                }
                            }

                            // Parse individual players in this stat group
                            if (statGroupElement.TryGetProperty("athletes", out var athletesElement) &&
                                athletesElement.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var athleteElement in athletesElement.EnumerateArray())
                                {
                                    var playerStats = await ParseSinglePlayerStatsAsync(
                                        athleteElement, team, statGroupName, statKeys, statLabels, statDescriptions,
                                        gameId, season, week, cancellationToken);

                                    if (playerStats != null)
                                    {
                                        allPlayerStats.Add(playerStats);
                                    }
                                }
                            }
                        }
                    }
                }

                _logger.LogInformation("Successfully parsed {PlayerCount} player statistics from boxscore", allPlayerStats.Count);
                return allPlayerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse player statistics from box score JSON");
                return new List<PlayerStats>();
            }
        }

        private async Task<PlayerStats?> ParseSinglePlayerStatsAsync(
            JsonElement athleteElement,
            Team team,
            string statGroupName,
            List<string> statKeys,
            List<string> statLabels,
            List<string> statDescriptions,
            string gameId,
            int season,
            int week,
            CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; // For async signature consistency

            try
            {
                // Get player info
                var playerId = string.Empty;
                var playerName = string.Empty;

                if (athleteElement.TryGetProperty("athlete", out var athleteInfoElement))
                {
                    if (athleteInfoElement.TryGetProperty("id", out var idElement))
                    {
                        playerId = idElement.GetString() ?? string.Empty;
                    }
                    if (athleteInfoElement.TryGetProperty("displayName", out var nameElement))
                    {
                        playerName = nameElement.GetString() ?? string.Empty;
                    }
                }

                // Get stats array
                if (!athleteElement.TryGetProperty("stats", out var statsElement) ||
                    statsElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                // Create PlayerStats object
                var playerStats = new PlayerStats
                {
                    PlayerId = playerId,
                    DisplayName = playerName,
                    Team = team,
                    GameId = gameId,
                    Season = season,
                    Week = week,
                    Statistics = new List<PlayerStatistic>()
                };

                // Map stats using keys and stat values
                var statsArray = statsElement.EnumerateArray().ToArray();
                for (int i = 0; i < statKeys.Count && i < statsArray.Length; i++)
                {
                    var statKey = statKeys[i];
                    var statValue = statsArray[i].GetString() ?? "0";

                    // Create PlayerStatistic object
                    var playerStatistic = new PlayerStatistic
                    {
                        Name = statKey,
                        DisplayName = i < statLabels.Count ? statLabels[i] : statKey,
                        ShortDisplayName = statKey,
                        Description = i < statDescriptions.Count ? statDescriptions[i] : "",
                        Abbreviation = statKey,
                        DisplayValue = statValue,
                        Category = statGroupName
                    };

                    // Try to parse numeric value
                    if (decimal.TryParse(statValue, out var numericValue))
                    {
                        playerStatistic.Value = numericValue;
                    }

                    playerStats.Statistics.Add(playerStatistic);
                }

                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse individual player stats for team {TeamId}", team.Id);
                return null;
            }
        }

        public async Task<Dictionary<string, object>?> GetTeamOffensiveStatsAsync(string gameId, string teamId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting offensive statistics for team {TeamId} in game {GameId}", teamId, gameId);

                var boxScore = await GetBoxScoreDataAsync(gameId, cancellationToken);
                if (boxScore == null)
                {
                    return null;
                }

                var teamStats = boxScore.Teams.FirstOrDefault(t => t.Team.Id == teamId);
                if (teamStats == null)
                {
                    _logger.LogWarning("Team {TeamId} not found in box score for game {GameId}", teamId, gameId);
                    return null;
                }

                var offensiveStats = new Dictionary<string, object>
                {
                    ["totalYards"] = GetStatisticValue(teamStats.Statistics, "totalYards", 0),
                    ["rushingYards"] = GetStatisticValue(teamStats.Statistics, "rushingYards", 0),
                    ["passingYards"] = GetStatisticValue(teamStats.Statistics, "passingYards", 0),
                    ["firstDowns"] = GetStatisticValue(teamStats.Statistics, "firstDowns", 0),
                    ["thirdDownConversions"] = GetStatisticValue(teamStats.Statistics, "thirdDownConversions", "0/0"),
                    ["fourthDownConversions"] = GetStatisticValue(teamStats.Statistics, "fourthDownConversions", "0/0"),
                    ["redZoneConversions"] = GetStatisticValue(teamStats.Statistics, "redZoneConversions", "0/0"),
                    ["penalties"] = GetStatisticValue(teamStats.Statistics, "penalties", "0-0"),
                    ["turnovers"] = GetStatisticValue(teamStats.Statistics, "turnovers", 0),
                    ["timeOfPossession"] = GetStatisticValue(teamStats.Statistics, "timeOfPossession", "00:00")
                };

                _logger.LogDebug("Successfully retrieved offensive statistics for team {TeamId}", teamId);
                return offensiveStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get offensive statistics for team {TeamId} in game {GameId}", teamId, gameId);
                return null;
            }
        }

        public async Task<Dictionary<string, object>?> GetTeamDefensiveStatsAsync(string gameId, string teamId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Getting defensive statistics for team {TeamId} in game {GameId}", teamId, gameId);

                var boxScore = await GetBoxScoreDataAsync(gameId, cancellationToken);
                if (boxScore == null)
                {
                    return null;
                }

                var teamStats = boxScore.Teams.FirstOrDefault(t => t.Team.Id == teamId);
                if (teamStats == null)
                {
                    _logger.LogWarning("Team {TeamId} not found in box score for game {GameId}", teamId, gameId);
                    return null;
                }

                var defensiveStats = new Dictionary<string, object>
                {
                    ["sacks"] = GetStatisticValue(teamStats.Statistics, "sacks", "0-0"),
                    ["interceptions"] = GetStatisticValue(teamStats.Statistics, "interceptions", 0),
                    ["fumbleRecoveries"] = GetStatisticValue(teamStats.Statistics, "fumbleRecoveries", 0),
                    ["safeties"] = GetStatisticValue(teamStats.Statistics, "safeties", 0),
                    ["pointsAllowed"] = GetStatisticValue(teamStats.Statistics, "pointsAllowed", 0),
                    ["yardsAllowed"] = GetStatisticValue(teamStats.Statistics, "yardsAllowed", 0),
                    ["rushingYardsAllowed"] = GetStatisticValue(teamStats.Statistics, "rushingYardsAllowed", 0),
                    ["passingYardsAllowed"] = GetStatisticValue(teamStats.Statistics, "passingYardsAllowed", 0)
                };

                _logger.LogDebug("Successfully retrieved defensive statistics for team {TeamId}", teamId);
                return defensiveStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get defensive statistics for team {TeamId} in game {GameId}", teamId, gameId);
                return null;
            }
        }

        public async Task<bool> IsGameLiveAsync(string gameId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Checking if game {GameId} is live", gameId);

                var apiUrl = string.Format(ApiBoxScorePattern, gameId);
                var jsonData = await _httpService.GetRawJsonAsync(apiUrl, cancellationToken);

                var jsonDoc = JsonDocument.Parse(jsonData);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("header", out var headerElement) &&
                    headerElement.TryGetProperty("competition", out var competitionElement) &&
                    competitionElement.TryGetProperty("status", out var statusElement) &&
                    statusElement.TryGetProperty("type", out var typeElement) &&
                    typeElement.TryGetProperty("state", out var stateElement))
                {
                    var state = stateElement.GetString();
                    var isLive = state == "in" || state == "live";

                    _logger.LogDebug("Game {GameId} live status: {IsLive} (state: {State})", gameId, isLive, state);
                    return isLive;
                }

                _logger.LogWarning("Could not determine live status for game {GameId}", gameId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check live status for game {GameId}", gameId);
                return false;
            }
        }

        #region Private Helper Methods

        private async Task<BoxScore?> ParseBoxScoreFromJsonAsync(string jsonData, CancellationToken cancellationToken)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(jsonData);
                var root = jsonDoc.RootElement;

                var boxScore = new BoxScore();

                // Parse teams data
                if (root.TryGetProperty("boxscore", out var boxscoreElement) &&
                    boxscoreElement.TryGetProperty("teams", out var teamsElement))
                {
                    var teams = new List<TeamBoxScore>();
                    if (teamsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var teamElement in teamsElement.EnumerateArray())
                        {
                            var team = await ParseSingleTeamStatsAsync(teamElement, cancellationToken);
                            if (team != null)
                            {
                                teams.Add(team);
                            }
                        }
                    }
                    boxScore.Teams = teams;
                }

                // Parse game info
                var gameInfo = await ExtractGameMetadataAsync(jsonData, cancellationToken);
                if (gameInfo != null)
                {
                    boxScore.GameInfo = gameInfo;
                }

                // Parse player statistics (final game stats only)
                var playerStats = await ParsePlayerStatsAsync(jsonData, cancellationToken);
                if (playerStats.Count > 0)
                {
                    boxScore.Players = playerStats;
                    _logger.LogInformation("Successfully parsed {PlayerCount} player statistics from boxscore", playerStats.Count);
                }
                else
                {
                    _logger.LogWarning("No player statistics found in boxscore data");
                    boxScore.Players = new List<PlayerStats>();
                }

                return boxScore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse box score from JSON data");
                return null;
            }
        }

        private async Task<TeamBoxScore?> ParseSingleTeamStatsAsync(JsonElement teamElement, CancellationToken cancellationToken)
        {
            await Task.CompletedTask; // For async signature consistency

            try
            {
                var teamBoxScore = new TeamBoxScore();

                // Parse team information
                if (teamElement.TryGetProperty("team", out var teamInfoElement))
                {
                    var team = new Team();
                    if (teamInfoElement.TryGetProperty("id", out var idElement))
                    {
                        team.Id = idElement.GetString() ?? string.Empty;
                    }
                    if (teamInfoElement.TryGetProperty("displayName", out var nameElement))
                    {
                        team.DisplayName = nameElement.GetString() ?? string.Empty;
                    }
                    if (teamInfoElement.TryGetProperty("abbreviation", out var abbrevElement))
                    {
                        team.Abbreviation = abbrevElement.GetString() ?? string.Empty;
                    }
                    teamBoxScore.Team = team;
                }

                // Parse statistics
                if (teamElement.TryGetProperty("statistics", out var statsElement))
                {
                    var statistics = new List<BoxScoreTeamStatistic>();
                    if (statsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var statElement in statsElement.EnumerateArray())
                        {
                            var statistic = new BoxScoreTeamStatistic();
                            if (statElement.TryGetProperty("name", out var nameElement))
                            {
                                statistic.Name = nameElement.GetString() ?? string.Empty;
                            }
                            if (statElement.TryGetProperty("displayName", out var displayNameElement))
                            {
                                statistic.DisplayName = displayNameElement.GetString() ?? string.Empty;
                            }
                            if (statElement.TryGetProperty("value", out var valueElement))
                            {
                                statistic.Value = valueElement.ValueKind switch
                                {
                                    JsonValueKind.String => valueElement.GetString() ?? string.Empty,
                                    JsonValueKind.Number => valueElement.GetRawText(),
                                    _ => valueElement.ToString()
                                };
                            }
                            if (statElement.TryGetProperty("displayValue", out var displayValueElement))
                            {
                                statistic.DisplayValue = displayValueElement.ValueKind switch
                                {
                                    JsonValueKind.String => displayValueElement.GetString() ?? string.Empty,
                                    JsonValueKind.Number => displayValueElement.GetRawText(),
                                    _ => displayValueElement.ToString()
                                };
                            }
                            statistics.Add(statistic);
                        }
                    }
                    teamBoxScore.Statistics = statistics;
                }

                // Parse line score
                if (teamElement.TryGetProperty("lineScore", out var lineScoreElement))
                {
                    var lineScores = new List<BoxScoreLineScore>();
                    var periodNumber = 1;
                    if (lineScoreElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var scoreElement in lineScoreElement.EnumerateArray())
                        {
                            var lineScore = new BoxScoreLineScore();
                            lineScore.Period = periodNumber++;
                            lineScore.Value = scoreElement.GetInt32();
                            lineScores.Add(lineScore);
                        }
                    }
                    teamBoxScore.LineScores = lineScores;
                }

                return teamBoxScore;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse single team stats");
                return null;
            }
        }

        private async Task<Drive?> ParseSingleDriveAsync(JsonElement driveElement, CancellationToken cancellationToken)
        {
            await Task.CompletedTask; // For async signature consistency

            try
            {
                _logger.LogDebug("ParseSingleDriveAsync: element type {ValueKind}", driveElement.ValueKind);
                var drive = new Drive();

                if (driveElement.TryGetProperty("id", out var idElement))
                {
                    drive.Id = idElement.ValueKind == JsonValueKind.String
                        ? idElement.GetString() ?? string.Empty
                        : idElement.ToString();
                }

                if (driveElement.TryGetProperty("description", out var descElement))
                {
                    drive.Description = descElement.ValueKind == JsonValueKind.String
                        ? descElement.GetString() ?? string.Empty
                        : descElement.ToString();
                }

                if (driveElement.TryGetProperty("plays", out var playsElement))
                {
                    drive.Plays = playsElement.ValueKind == JsonValueKind.Array
                        ? playsElement.GetArrayLength()
                        : (playsElement.ValueKind == JsonValueKind.Number ? playsElement.GetInt32() : 0);
                }

                if (driveElement.TryGetProperty("yards", out var yardsElement))
                {
                    drive.Yards = yardsElement.ValueKind == JsonValueKind.Number
                        ? yardsElement.GetInt32()
                        : 0;
                }

                if (driveElement.TryGetProperty("timeElapsed", out var timeElement))
                {
                    drive.TimeElapsed = timeElement.ValueKind == JsonValueKind.String
                        ? timeElement.GetString() ?? string.Empty
                        : timeElement.ToString();
                }

                if (driveElement.TryGetProperty("result", out var resultElement))
                {
                    drive.Result = resultElement.ValueKind == JsonValueKind.String
                        ? resultElement.GetString() ?? string.Empty
                        : resultElement.ToString();
                }

                // Parse team information
                if (driveElement.TryGetProperty("team", out var teamElement))
                {
                    var team = new Team();
                    if (teamElement.TryGetProperty("id", out var teamIdElement))
                    {
                        team.Id = teamIdElement.ValueKind == JsonValueKind.String
                            ? teamIdElement.GetString() ?? string.Empty
                            : teamIdElement.ToString();
                    }
                    if (teamElement.TryGetProperty("displayName", out var teamNameElement))
                    {
                        team.DisplayName = teamNameElement.ValueKind == JsonValueKind.String
                            ? teamNameElement.GetString() ?? string.Empty
                            : teamNameElement.ToString();
                    }
                    drive.Team = team;
                }

                return drive;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse single drive");
                return null;
            }
        }

        private async Task<ScoringPlay?> ParseSingleScoringPlayAsync(JsonElement playElement, CancellationToken cancellationToken)
        {
            await Task.CompletedTask; // For async signature consistency

            try
            {
                var scoringPlay = new ScoringPlay();

                if (playElement.TryGetProperty("id", out var idElement))
                {
                    scoringPlay.Id = idElement.ValueKind == JsonValueKind.String
                        ? idElement.GetString() ?? string.Empty
                        : idElement.ToString();
                }

                if (playElement.TryGetProperty("text", out var textElement))
                {
                    scoringPlay.Text = textElement.ValueKind == JsonValueKind.String
                        ? textElement.GetString() ?? string.Empty
                        : textElement.ToString();
                }

                if (playElement.TryGetProperty("type", out var typeElement))
                {
                    scoringPlay.Type = typeElement.ValueKind == JsonValueKind.String
                        ? typeElement.GetString() ?? string.Empty
                        : typeElement.ToString();
                }

                if (playElement.TryGetProperty("period", out var periodElement))
                {
                    scoringPlay.Period = periodElement.ValueKind == JsonValueKind.Number
                        ? periodElement.GetInt32()
                        : 0;
                }

                if (playElement.TryGetProperty("clock", out var clockElement))
                {
                    scoringPlay.Clock = clockElement.ValueKind == JsonValueKind.String
                        ? clockElement.GetString() ?? string.Empty
                        : clockElement.ToString();
                }

                if (playElement.TryGetProperty("scoreValue", out var scoreElement))
                {
                    scoringPlay.ScoreValue = scoreElement.ValueKind == JsonValueKind.Number
                        ? scoreElement.GetInt32()
                        : 0;
                }

                // Parse team information
                if (playElement.TryGetProperty("team", out var teamElement))
                {
                    var team = new Team();
                    if (teamElement.TryGetProperty("id", out var teamIdElement))
                    {
                        team.Id = teamIdElement.ValueKind == JsonValueKind.String
                            ? teamIdElement.GetString() ?? string.Empty
                            : teamIdElement.ToString();
                    }
                    if (teamElement.TryGetProperty("displayName", out var teamNameElement))
                    {
                        team.DisplayName = teamNameElement.ValueKind == JsonValueKind.String
                            ? teamNameElement.GetString() ?? string.Empty
                            : teamNameElement.ToString();
                    }
                    scoringPlay.Team = team;
                }

                return scoringPlay;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse single scoring play");
                return null;
            }
        }

        /// <summary>
        /// Helper method to get a statistic value from the list of team statistics
        /// </summary>
        private object GetStatisticValue(List<BoxScoreTeamStatistic> statistics, string name, object defaultValue)
        {
            var stat = statistics.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (stat == null)
            {
                return defaultValue;
            }

            // Try to parse as int if default is int
            if (defaultValue is int && int.TryParse(stat.Value, out var intValue))
            {
                return intValue;
            }

            // Return string value
            return string.IsNullOrEmpty(stat.Value) ? defaultValue : stat.Value;
        }

        #region New ESPN Structure Parsing

        private async Task<Drive?> ParseNewDriveStructureAsync(JsonElement driveElement, CancellationToken cancellationToken)
        {
            await Task.CompletedTask; // For async signature consistency

            try
            {
                var drive = new Drive();

                // Parse drive ID
                if (driveElement.TryGetProperty("id", out var idElement))
                {
                    drive.Id = idElement.ValueKind == JsonValueKind.String
                        ? idElement.GetString() ?? string.Empty
                        : idElement.ToString();
                }

                // Parse drive description
                if (driveElement.TryGetProperty("description", out var descElement))
                {
                    drive.Description = descElement.ValueKind == JsonValueKind.String
                        ? descElement.GetString() ?? string.Empty
                        : descElement.ToString();
                }

                // Parse team - for now we'll create a minimal team object
                if (driveElement.TryGetProperty("teamName", out var teamNameElement))
                {
                    drive.Team = new Team { Name = teamNameElement.GetString() ?? string.Empty };
                }

                // Parse plays count from the plays array
                if (driveElement.TryGetProperty("plays", out var playsElement) &&
                    playsElement.ValueKind == JsonValueKind.Array)
                {
                    drive.Plays = playsElement.GetArrayLength();
                }

                // Try to extract yards and time from description
                // Description format is typically: "11 plays, 70 yards, 6:46"
                var description = drive.Description;
                if (!string.IsNullOrEmpty(description))
                {
                    var parts = description.Split(',');
                    foreach (var part in parts)
                    {
                        var trimmed = part.Trim();
                        if (trimmed.Contains("yard"))
                        {
                            var yardsPart = trimmed.Split(' ')[0];
                            if (int.TryParse(yardsPart, out var yards))
                            {
                                drive.Yards = yards;
                            }
                        }
                        else if (trimmed.Contains(":"))
                        {
                            drive.TimeElapsed = trimmed;
                        }
                    }
                }

                _logger.LogDebug("Parsed drive {DriveId} with {PlayCount} plays, {Yards} yards",
                    drive.Id, drive.Plays, drive.Yards);
                return drive;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse new drive structure");
                return null;
            }
        }

        private async Task<ScoringPlay?> ParseNewScoringPlayStructureAsync(JsonElement playElement, CancellationToken cancellationToken)
        {
            await Task.CompletedTask; // For async signature consistency

            try
            {
                var scoringPlay = new ScoringPlay();

                if (playElement.TryGetProperty("id", out var idElement))
                {
                    scoringPlay.Id = idElement.ValueKind == JsonValueKind.String
                        ? idElement.GetString() ?? string.Empty
                        : idElement.ToString();
                }

                if (playElement.TryGetProperty("description", out var descElement))
                {
                    scoringPlay.Text = descElement.ValueKind == JsonValueKind.String
                        ? descElement.GetString() ?? string.Empty
                        : descElement.ToString();
                }

                if (playElement.TryGetProperty("playType", out var typeElement))
                {
                    scoringPlay.Type = typeElement.ValueKind == JsonValueKind.String
                        ? typeElement.GetString() ?? string.Empty
                        : typeElement.ToString();
                }

                return scoringPlay;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse new scoring play structure");
                return null;
            }
        }

        #endregion

        #endregion
    }
}