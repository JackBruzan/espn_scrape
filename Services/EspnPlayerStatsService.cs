using ESPNScrape.Models.Espn;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ESPNScrape.Services
{
    public class EspnPlayerStatsService : IEspnPlayerStatsService
    {
        private readonly IEspnHttpService _httpService;
        private readonly ILogger<EspnPlayerStatsService> _logger;

        // ESPN box score URL template
        private const string BoxScoreUrlTemplate = "https://www.espn.com/nfl/boxscore/_/gameId/{eventId}";

        // Regex patterns for player name normalization
        private static readonly Regex NameCleanupRegex = new(@"[^\w\s'-]", RegexOptions.Compiled);
        private static readonly Regex MultipleSpacesRegex = new(@"\s+", RegexOptions.Compiled);

        // Position mapping for stat categorization
        private static readonly Dictionary<string, string[]> PositionStatCategories = new()
        {
            { "QB", new[] { "passing", "rushing" } },
            { "RB", new[] { "rushing", "receiving" } },
            { "FB", new[] { "rushing", "receiving" } },
            { "WR", new[] { "receiving", "rushing" } },
            { "TE", new[] { "receiving", "rushing" } },
            { "K", new[] { "kicking" } },
            { "P", new[] { "punting" } },
            { "DEF", new[] { "defensive", "interceptions", "fumbles" } }
        };

        public EspnPlayerStatsService(IEspnHttpService httpService, ILogger<EspnPlayerStatsService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<IEnumerable<PlayerStats>> ExtractGamePlayerStatsAsync(string eventId, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Extracting player statistics for game {EventId}", eventId);

                if (string.IsNullOrEmpty(eventId))
                {
                    throw new ArgumentException("Event ID cannot be null or empty", nameof(eventId));
                }

                // Get box score data from ESPN
                var boxScoreUrl = BoxScoreUrlTemplate.Replace("{eventId}", eventId);
                var boxScoreHtml = await _httpService.GetRawJsonAsync(boxScoreUrl, cancellationToken);

                // Extract embedded JSON from HTML
                var boxScoreJson = ExtractBoxScoreJson(boxScoreHtml);
                if (string.IsNullOrEmpty(boxScoreJson))
                {
                    _logger.LogWarning("No box score data found for game {EventId}", eventId);
                    return Enumerable.Empty<PlayerStats>();
                }

                // Parse game information from the JSON
                var gameInfo = ExtractGameInfo(boxScoreJson, eventId);

                // Extract player statistics
                var playerStats = await ParsePlayerStatsFromJsonAsync(boxScoreJson, gameInfo, cancellationToken);

                _logger.LogInformation("Successfully extracted {PlayerCount} player statistics for game {EventId}",
                    playerStats.Count(), eventId);

                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract player statistics for game {EventId}", eventId);
                throw;
            }
        }

        public async Task<IEnumerable<PlayerStats>> ParsePlayerStatsFromJsonAsync(string boxScoreJson, GameEvent gameInfo, CancellationToken cancellationToken = default)
        {
            try
            {
                var playerStatsList = new List<PlayerStats>();

                if (string.IsNullOrEmpty(boxScoreJson))
                {
                    return playerStatsList;
                }

                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                // Navigate to box score data
                if (!root.TryGetProperty("boxscore", out var boxScoreElement))
                {
                    _logger.LogWarning("No box score element found in JSON data");
                    return playerStatsList;
                }

                // Extract team statistics
                if (boxScoreElement.TryGetProperty("teams", out var teamsElement))
                {
                    foreach (var teamElement in teamsElement.EnumerateArray())
                    {
                        var teamStats = await ExtractTeamPlayerStatsAsync(teamElement, gameInfo, cancellationToken);
                        playerStatsList.AddRange(teamStats);
                    }
                }

                _logger.LogDebug("Parsed {PlayerCount} player statistics from JSON data", playerStatsList.Count);

                return playerStatsList;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse player statistics from JSON data");
                throw new InvalidOperationException("Invalid JSON format in box score data", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse player statistics from JSON data");
                throw;
            }
        }

        public async Task<IEnumerable<string>> ExtractPlayerIdsAsync(string boxScoreJson, CancellationToken cancellationToken = default)
        {
            try
            {
                var playerIds = new HashSet<string>();

                if (string.IsNullOrEmpty(boxScoreJson))
                {
                    return playerIds;
                }

                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                // Extract player IDs from various sections
                ExtractPlayerIdsFromElement(root, playerIds);

                _logger.LogDebug("Extracted {PlayerIdCount} unique player IDs from box score data", playerIds.Count);

                return playerIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract player IDs from box score JSON");
                throw;
            }
        }

        public async Task<PlayerStats> MapEspnPlayerDataAsync(dynamic espnPlayerData, PlayerPosition position, GameEvent gameContext, CancellationToken cancellationToken = default)
        {
            try
            {
                var playerStats = new PlayerStats
                {
                    GameId = gameContext.Id,
                    Season = gameContext.Season?.Year ?? DateTime.Now.Year,
                    Week = gameContext.Week?.WeekNumber ?? 1,
                    SeasonType = gameContext.Season?.SeasonType ?? 2,
                    Position = position
                };

                // Map basic player information
                if (espnPlayerData != null)
                {
                    playerStats.PlayerId = espnPlayerData.id?.ToString() ?? string.Empty;
                    playerStats.DisplayName = espnPlayerData.displayName?.ToString() ?? string.Empty;
                    playerStats.ShortName = espnPlayerData.shortName?.ToString() ?? string.Empty;
                    playerStats.Jersey = espnPlayerData.jersey?.ToString() ?? string.Empty;

                    // Map team information
                    if (espnPlayerData.team != null)
                    {
                        playerStats.Team = new Team
                        {
                            Id = espnPlayerData.team.id?.ToString() ?? string.Empty,
                            DisplayName = espnPlayerData.team.displayName?.ToString() ?? string.Empty,
                            Name = espnPlayerData.team.name?.ToString() ?? string.Empty,
                            Abbreviation = espnPlayerData.team.abbreviation?.ToString() ?? string.Empty
                        };
                    }

                    // Map statistics based on position
                    if (espnPlayerData.statistics != null)
                    {
                        var relevantStatCategories = GetRelevantStatCategories(position.Abbreviation);
                        playerStats.Statistics = MapStatistics(espnPlayerData.statistics, relevantStatCategories);
                    }
                }

                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to map ESPN player data for position {Position}", position.Abbreviation);
                throw;
            }
        }

        public async Task<string> NormalizePlayerNameAsync(string playerName, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(playerName))
            {
                return string.Empty;
            }

            try
            {
                // Remove special characters except hyphens and apostrophes
                var cleaned = NameCleanupRegex.Replace(playerName, " ");

                // Replace multiple spaces with single space
                cleaned = MultipleSpacesRegex.Replace(cleaned, " ");

                // Trim and title case
                cleaned = cleaned.Trim();

                if (!string.IsNullOrEmpty(cleaned))
                {
                    // Convert to title case (first letter of each word capitalized)
                    // Handle hyphenated names and apostrophes specially
                    var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < words.Length; i++)
                    {
                        words[i] = CapitalizeWord(words[i]);
                    }
                    cleaned = string.Join(" ", words);
                }

                return cleaned;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to normalize player name '{PlayerName}', returning original", playerName);
                return playerName;
            }
        }

        private string CapitalizeWord(string word)
        {
            if (string.IsNullOrEmpty(word))
                return word;

            // Handle hyphenated words (e.g., "brady-jones" -> "Brady-Jones")
            if (word.Contains('-'))
            {
                var parts = word.Split('-');
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i] = CapitalizeSimpleWord(parts[i]);
                }
                return string.Join("-", parts);
            }

            // Handle apostrophes (e.g., "o'brady" -> "O'Brady")
            if (word.Contains('\''))
            {
                var parts = word.Split('\'');
                for (int i = 0; i < parts.Length; i++)
                {
                    parts[i] = CapitalizeSimpleWord(parts[i]);
                }
                return string.Join("'", parts);
            }

            return CapitalizeSimpleWord(word);
        }

        private string CapitalizeSimpleWord(string word)
        {
            if (string.IsNullOrEmpty(word))
                return word;

            return char.ToUpper(word[0]) + word[1..].ToLower();
        }

        public async Task<IEnumerable<PlayerStats>> ExtractTeamStatsAsync(string boxScoreJson, string teamId, CancellationToken cancellationToken = default)
        {
            try
            {
                var playerStatsList = new List<PlayerStats>();

                if (string.IsNullOrEmpty(boxScoreJson) || string.IsNullOrEmpty(teamId))
                {
                    return playerStatsList;
                }

                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                // Find specific team data
                if (root.TryGetProperty("boxscore", out var boxScoreElement) &&
                    boxScoreElement.TryGetProperty("teams", out var teamsElement))
                {
                    foreach (var teamElement in teamsElement.EnumerateArray())
                    {
                        if (teamElement.TryGetProperty("team", out var teamInfo) &&
                            teamInfo.TryGetProperty("id", out var teamIdElement) &&
                            teamIdElement.GetString() == teamId)
                        {
                            var gameInfo = ExtractGameInfo(boxScoreJson, string.Empty);
                            var teamStats = await ExtractTeamPlayerStatsAsync(teamElement, gameInfo, cancellationToken);
                            playerStatsList.AddRange(teamStats);
                            break;
                        }
                    }
                }

                return playerStatsList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract team statistics for team {TeamId}", teamId);
                throw;
            }
        }

        public async Task<PlayerStats> HandleMissingDataAsync(dynamic playerData, PlayerPosition position, CancellationToken cancellationToken = default)
        {
            try
            {
                var playerStats = new PlayerStats
                {
                    Position = position,
                    Statistics = new List<PlayerStatistic>()
                };

                // Set basic information with defaults
                if (playerData != null)
                {
                    playerStats.PlayerId = playerData.id?.ToString() ?? Guid.NewGuid().ToString();
                    playerStats.DisplayName = playerData.displayName?.ToString() ?? "Unknown Player";
                    playerStats.ShortName = playerData.shortName?.ToString() ?? "Unknown";
                    playerStats.Jersey = playerData.jersey?.ToString() ?? "0";
                }
                else
                {
                    // Complete missing data scenario
                    playerStats.PlayerId = Guid.NewGuid().ToString();
                    playerStats.DisplayName = "Unknown Player";
                    playerStats.ShortName = "Unknown";
                    playerStats.Jersey = "0";
                }

                // Add default statistics for the position
                var defaultStatCategories = GetRelevantStatCategories(position.Abbreviation);
                foreach (var category in defaultStatCategories)
                {
                    playerStats.Statistics.Add(new PlayerStatistic
                    {
                        Category = category,
                        Name = $"{category}_default",
                        DisplayName = $"Default {category}",
                        Value = 0,
                        DisplayValue = "0"
                    });
                }

                _logger.LogDebug("Created default player stats for position {Position} with {StatCount} default statistics",
                    position.Abbreviation, playerStats.Statistics.Count);

                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to handle missing player data for position {Position}", position.Abbreviation);
                throw;
            }
        }

        public async Task<bool> ValidatePlayerStatsAsync(PlayerStats playerStats, CancellationToken cancellationToken = default)
        {
            try
            {
                if (playerStats == null)
                {
                    return false;
                }

                // Validate required fields
                if (string.IsNullOrEmpty(playerStats.PlayerId) ||
                    string.IsNullOrEmpty(playerStats.DisplayName) ||
                    playerStats.Position == null)
                {
                    _logger.LogWarning("Player stats missing required fields: PlayerId={PlayerId}, DisplayName={DisplayName}, Position={Position}",
                        playerStats.PlayerId, playerStats.DisplayName, playerStats.Position?.Abbreviation);
                    return false;
                }

                // Validate statistics values
                if (playerStats.Statistics != null)
                {
                    foreach (var stat in playerStats.Statistics)
                    {
                        // Check for reasonable stat values (no negative stats except for sacks taken, etc.)
                        if (stat.Value < 0 && !IsNegativeStatAllowed(stat.Name))
                        {
                            _logger.LogWarning("Invalid negative statistic {StatName}={Value} for player {PlayerId}",
                                stat.Name, stat.Value, playerStats.PlayerId);
                            return false;
                        }

                        // Check for extremely high values that might indicate parsing errors
                        if (stat.Value > 1000 && !IsHighValueStatAllowed(stat.Name))
                        {
                            _logger.LogWarning("Suspiciously high statistic {StatName}={Value} for player {PlayerId}",
                                stat.Name, stat.Value, playerStats.PlayerId);
                            return false;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate player statistics for player {PlayerId}", playerStats?.PlayerId);
                return false;
            }
        }

        #region Private Helper Methods

        private string ExtractBoxScoreJson(string htmlContent)
        {
            try
            {
                // ESPN embeds box score data in window.__espnfitt__ variable
                var regex = new Regex(@"window\['__espnfitt__'\]\s*=\s*({.*?});", RegexOptions.Singleline);
                var match = regex.Match(htmlContent);

                if (match.Success)
                {
                    return match.Groups[1].Value;
                }

                // Alternative pattern for different ESPN page formats
                var altRegex = new Regex(@"window\.__espnfitt__\s*=\s*({.*?});", RegexOptions.Singleline);
                var altMatch = altRegex.Match(htmlContent);

                if (altMatch.Success)
                {
                    return altMatch.Groups[1].Value;
                }

                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract box score JSON from HTML content");
                return string.Empty;
            }
        }

        private GameEvent ExtractGameInfo(string boxScoreJson, string eventId)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(boxScoreJson);
                var root = jsonDoc.RootElement;

                var gameInfo = new GameEvent { Id = eventId };

                // Extract basic game information
                if (root.TryGetProperty("gamepackageJSON", out var gamePackage) &&
                    gamePackage.TryGetProperty("header", out var header))
                {
                    if (header.TryGetProperty("season", out var seasonElement))
                    {
                        gameInfo.Season = JsonSerializer.Deserialize<Season>(seasonElement.GetRawText());
                    }

                    if (header.TryGetProperty("week", out var weekElement))
                    {
                        gameInfo.Week = JsonSerializer.Deserialize<Week>(weekElement.GetRawText());
                    }
                }

                return gameInfo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract game info from box score JSON, using defaults");
                return new GameEvent { Id = eventId };
            }
        }

        private async Task<IEnumerable<PlayerStats>> ExtractTeamPlayerStatsAsync(JsonElement teamElement, GameEvent gameInfo, CancellationToken cancellationToken)
        {
            var playerStatsList = new List<PlayerStats>();

            try
            {
                // Extract team information
                Team team = new Team();
                if (teamElement.TryGetProperty("team", out var teamInfo))
                {
                    team.Id = teamInfo.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
                    team.DisplayName = teamInfo.TryGetProperty("displayName", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
                    team.Abbreviation = teamInfo.TryGetProperty("abbreviation", out var abbrevElement) ? abbrevElement.GetString() ?? string.Empty : string.Empty;
                }

                // Extract player statistics
                if (teamElement.TryGetProperty("statistics", out var statisticsElement))
                {
                    foreach (var statCategoryElement in statisticsElement.EnumerateArray())
                    {
                        if (statCategoryElement.TryGetProperty("athletes", out var athletesElement))
                        {
                            foreach (var athleteElement in athletesElement.EnumerateArray())
                            {
                                var playerStats = await ParsePlayerFromAthleteElementAsync(athleteElement, team, gameInfo, cancellationToken);
                                if (playerStats != null && await ValidatePlayerStatsAsync(playerStats, cancellationToken))
                                {
                                    playerStatsList.Add(playerStats);
                                }
                            }
                        }
                    }
                }

                return playerStatsList;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract team player statistics for team {TeamId}",
                    teamElement.TryGetProperty("team", out var teamProp) && teamProp.TryGetProperty("id", out var idProp) ? idProp.GetString() : "unknown");
                return playerStatsList;
            }
        }

        private async Task<PlayerStats?> ParsePlayerFromAthleteElementAsync(JsonElement athleteElement, Team team, GameEvent gameInfo, CancellationToken cancellationToken)
        {
            try
            {
                var playerStats = new PlayerStats
                {
                    GameId = gameInfo.Id,
                    Season = gameInfo.Season?.Year ?? DateTime.Now.Year,
                    Week = gameInfo.Week?.WeekNumber ?? 1,
                    SeasonType = gameInfo.Season?.SeasonType ?? 2,
                    Team = team,
                    Statistics = new List<PlayerStatistic>()
                };

                // Extract basic player information
                if (athleteElement.TryGetProperty("athlete", out var athleteInfo))
                {
                    playerStats.PlayerId = athleteInfo.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
                    playerStats.DisplayName = athleteInfo.TryGetProperty("displayName", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
                    playerStats.ShortName = athleteInfo.TryGetProperty("shortName", out var shortElement) ? shortElement.GetString() ?? string.Empty : string.Empty;
                    playerStats.Jersey = athleteInfo.TryGetProperty("jersey", out var jerseyElement) ? jerseyElement.GetString() ?? string.Empty : string.Empty;

                    // Extract position information
                    if (athleteInfo.TryGetProperty("position", out var positionElement))
                    {
                        playerStats.Position = JsonSerializer.Deserialize<PlayerPosition>(positionElement.GetRawText()) ?? new PlayerPosition();
                    }
                }

                // Extract statistics
                if (athleteElement.TryGetProperty("stats", out var statsElement))
                {
                    var statistics = ParseStatisticsFromElement(statsElement);
                    playerStats.Statistics.AddRange(statistics);
                }

                // Normalize player name
                if (!string.IsNullOrEmpty(playerStats.DisplayName))
                {
                    playerStats.DisplayName = await NormalizePlayerNameAsync(playerStats.DisplayName, cancellationToken);
                }

                return playerStats;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse player from athlete element");
                return null;
            }
        }

        private List<PlayerStatistic> ParseStatisticsFromElement(JsonElement statsElement)
        {
            var statistics = new List<PlayerStatistic>();

            try
            {
                foreach (var statProperty in statsElement.EnumerateObject())
                {
                    var statName = statProperty.Name;
                    var statValue = statProperty.Value;

                    if (statValue.ValueKind == JsonValueKind.Number)
                    {
                        statistics.Add(new PlayerStatistic
                        {
                            Name = statName,
                            DisplayName = FormatStatDisplayName(statName),
                            Category = DetermineStatCategory(statName),
                            Value = statValue.GetDecimal(),
                            DisplayValue = statValue.ToString()
                        });
                    }
                    else if (statValue.ValueKind == JsonValueKind.String)
                    {
                        if (decimal.TryParse(statValue.GetString(), out var numericValue))
                        {
                            statistics.Add(new PlayerStatistic
                            {
                                Name = statName,
                                DisplayName = FormatStatDisplayName(statName),
                                Category = DetermineStatCategory(statName),
                                Value = numericValue,
                                DisplayValue = statValue.GetString() ?? "0"
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse some statistics from element");
            }

            return statistics;
        }

        private void ExtractPlayerIdsFromElement(JsonElement element, HashSet<string> playerIds)
        {
            try
            {
                if (element.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in element.EnumerateObject())
                    {
                        if (property.Name == "id" && property.Value.ValueKind == JsonValueKind.String)
                        {
                            var id = property.Value.GetString();
                            if (!string.IsNullOrEmpty(id) && id.All(char.IsDigit))
                            {
                                playerIds.Add(id);
                            }
                        }
                        else if (property.Value.ValueKind == JsonValueKind.Object || property.Value.ValueKind == JsonValueKind.Array)
                        {
                            ExtractPlayerIdsFromElement(property.Value, playerIds);
                        }
                    }
                }
                else if (element.ValueKind == JsonValueKind.Array)
                {
                    foreach (var arrayElement in element.EnumerateArray())
                    {
                        ExtractPlayerIdsFromElement(arrayElement, playerIds);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to extract player IDs from JSON element");
            }
        }

        private string[] GetRelevantStatCategories(string positionAbbreviation)
        {
            if (PositionStatCategories.TryGetValue(positionAbbreviation, out var categories))
            {
                return categories;
            }

            // Default categories for unknown positions
            return new[] { "general" };
        }

        private List<PlayerStatistic> MapStatistics(dynamic statistics, string[] relevantCategories)
        {
            var mappedStats = new List<PlayerStatistic>();

            try
            {
                // This would be more complex in a real implementation
                // For now, just create placeholder statistics
                foreach (var category in relevantCategories)
                {
                    mappedStats.Add(new PlayerStatistic
                    {
                        Category = category,
                        Name = $"{category}_total",
                        DisplayName = $"Total {category}",
                        Value = 0,
                        DisplayValue = "0"
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to map statistics for categories: {Categories}", string.Join(", ", relevantCategories));
            }

            return mappedStats;
        }

        private string FormatStatDisplayName(string statName)
        {
            // Convert camelCase or snake_case to display format
            var formatted = statName.Replace("_", " ");

            // Add spaces before capital letters
            formatted = Regex.Replace(formatted, "([a-z])([A-Z])", "$1 $2");

            // Title case
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(formatted.ToLower());
        }

        private string DetermineStatCategory(string statName)
        {
            var lowerStatName = statName.ToLower();

            if (lowerStatName.Contains("pass") || lowerStatName.Contains("completion") || lowerStatName.Contains("attempt"))
                return "passing";
            if (lowerStatName.Contains("rush") || lowerStatName.Contains("carr"))
                return "rushing";
            if (lowerStatName.Contains("rec") || lowerStatName.Contains("target"))
                return "receiving";
            if (lowerStatName.Contains("sack") || lowerStatName.Contains("tackle") || lowerStatName.Contains("int"))
                return "defensive";
            if (lowerStatName.Contains("kick") || lowerStatName.Contains("fg") || lowerStatName.Contains("xp"))
                return "kicking";
            if (lowerStatName.Contains("punt"))
                return "punting";

            return "general";
        }

        private bool IsNegativeStatAllowed(string statName)
        {
            var allowedNegativeStats = new[] { "sackYards", "tackleForLoss", "netYards" };
            return allowedNegativeStats.Any(allowed => statName.Contains(allowed, StringComparison.OrdinalIgnoreCase));
        }

        private bool IsHighValueStatAllowed(string statName)
        {
            var allowedHighValueStats = new[] { "yards", "passingYards", "rushingYards", "receivingYards" };
            return allowedHighValueStats.Any(allowed => statName.Contains(allowed, StringComparison.OrdinalIgnoreCase));
        }

        #endregion
    }
}