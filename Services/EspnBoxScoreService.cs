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

                var teamsArray = teamsElement.EnumerateArray().ToArray();
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

        public async Task<GameInfo?> ExtractGameMetadataAsync(string boxScoreJson, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Extracting game metadata from box score JSON");

                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                if (!root.TryGetProperty("header", out var headerElement))
                {
                    _logger.LogWarning("Box score JSON does not contain header information");
                    return null;
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
                return gameInfo;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract game metadata from box score JSON");
                return null;
            }
        }

        public string GetBoxScoreUrl(string gameId)
        {
            return string.Format(BoxScoreUrlPattern, gameId);
        }

        public async Task<(List<Drive> drives, List<ScoringPlay> scoringPlays)?> ParsePlayByPlayDataAsync(string boxScoreJson, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Parsing play-by-play data from box score JSON");

                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                var drives = new List<Drive>();
                var scoringPlays = new List<ScoringPlay>();

                // Extract drives
                if (root.TryGetProperty("drives", out var drivesElement))
                {
                    foreach (var driveElement in drivesElement.EnumerateArray())
                    {
                        var drive = await ParseSingleDriveAsync(driveElement, cancellationToken);
                        if (drive != null)
                        {
                            drives.Add(drive);
                        }
                    }
                }

                // Extract scoring plays
                if (root.TryGetProperty("scoringPlays", out var scoringPlaysElement))
                {
                    foreach (var playElement in scoringPlaysElement.EnumerateArray())
                    {
                        var scoringPlay = await ParseSingleScoringPlayAsync(playElement, cancellationToken);
                        if (scoringPlay != null)
                        {
                            scoringPlays.Add(scoringPlay);
                        }
                    }
                }

                _logger.LogDebug("Successfully parsed {DriveCount} drives and {ScoringPlayCount} scoring plays",
                    drives.Count, scoringPlays.Count);

                return (drives, scoringPlays);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse play-by-play data from box score JSON");
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
                    foreach (var teamElement in teamsElement.EnumerateArray())
                    {
                        var team = await ParseSingleTeamStatsAsync(teamElement, cancellationToken);
                        if (team != null)
                        {
                            teams.Add(team);
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

                // Parse play-by-play data
                var playByPlayData = await ParsePlayByPlayDataAsync(jsonData, cancellationToken);
                if (playByPlayData.HasValue)
                {
                    boxScore.Drives = playByPlayData.Value.drives;
                    boxScore.ScoringPlays = playByPlayData.Value.scoringPlays;
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
                            statistic.Value = valueElement.GetString() ?? string.Empty;
                        }
                        if (statElement.TryGetProperty("displayValue", out var displayValueElement))
                        {
                            statistic.DisplayValue = displayValueElement.GetString() ?? string.Empty;
                        }
                        statistics.Add(statistic);
                    }
                    teamBoxScore.Statistics = statistics;
                }

                // Parse line score
                if (teamElement.TryGetProperty("lineScore", out var lineScoreElement))
                {
                    var lineScores = new List<BoxScoreLineScore>();
                    var periodNumber = 1;
                    foreach (var scoreElement in lineScoreElement.EnumerateArray())
                    {
                        var lineScore = new BoxScoreLineScore();
                        lineScore.Period = periodNumber++;
                        lineScore.Value = scoreElement.GetInt32();
                        lineScores.Add(lineScore);
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
                var drive = new Drive();

                if (driveElement.TryGetProperty("id", out var idElement))
                {
                    drive.Id = idElement.GetString() ?? string.Empty;
                }

                if (driveElement.TryGetProperty("description", out var descElement))
                {
                    drive.Description = descElement.GetString() ?? string.Empty;
                }

                if (driveElement.TryGetProperty("plays", out var playsElement))
                {
                    drive.Plays = playsElement.GetInt32();
                }

                if (driveElement.TryGetProperty("yards", out var yardsElement))
                {
                    drive.Yards = yardsElement.GetInt32();
                }

                if (driveElement.TryGetProperty("timeElapsed", out var timeElement))
                {
                    drive.TimeElapsed = timeElement.GetString() ?? string.Empty;
                }

                if (driveElement.TryGetProperty("result", out var resultElement))
                {
                    drive.Result = resultElement.GetString() ?? string.Empty;
                }

                // Parse team information
                if (driveElement.TryGetProperty("team", out var teamElement))
                {
                    var team = new Team();
                    if (teamElement.TryGetProperty("id", out var teamIdElement))
                    {
                        team.Id = teamIdElement.GetString() ?? string.Empty;
                    }
                    if (teamElement.TryGetProperty("displayName", out var teamNameElement))
                    {
                        team.DisplayName = teamNameElement.GetString() ?? string.Empty;
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
                    scoringPlay.Id = idElement.GetString() ?? string.Empty;
                }

                if (playElement.TryGetProperty("text", out var textElement))
                {
                    scoringPlay.Text = textElement.GetString() ?? string.Empty;
                }

                if (playElement.TryGetProperty("type", out var typeElement))
                {
                    scoringPlay.Type = typeElement.GetString() ?? string.Empty;
                }

                if (playElement.TryGetProperty("period", out var periodElement))
                {
                    scoringPlay.Period = periodElement.GetInt32();
                }

                if (playElement.TryGetProperty("clock", out var clockElement))
                {
                    scoringPlay.Clock = clockElement.GetString() ?? string.Empty;
                }

                if (playElement.TryGetProperty("scoreValue", out var scoreElement))
                {
                    scoringPlay.ScoreValue = scoreElement.GetInt32();
                }

                // Parse team information
                if (playElement.TryGetProperty("team", out var teamElement))
                {
                    var team = new Team();
                    if (teamElement.TryGetProperty("id", out var teamIdElement))
                    {
                        team.Id = teamIdElement.GetString() ?? string.Empty;
                    }
                    if (teamElement.TryGetProperty("displayName", out var teamNameElement))
                    {
                        team.DisplayName = teamNameElement.GetString() ?? string.Empty;
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

        #endregion
    }
}