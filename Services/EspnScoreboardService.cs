using ESPNScrape.Models.Espn;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ESPNScrape.Services
{
    public class EspnScoreboardService : IEspnScoreboardService
    {
        private readonly IEspnHttpService _httpService;
        private readonly ILogger<EspnScoreboardService> _logger;

        // ESPN URL templates
        private const string ScoreboardUrlTemplate = "https://www.espn.com/nfl/scoreboard/_/week/{0}/year/{1}/seasontype/{2}";

        // Regex for extracting embedded JSON from ESPN HTML
        private static readonly Regex JsonExtractionRegex = new(@"window\['__espnfitt__'\]\s*=\s*({.*?});",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public EspnScoreboardService(IEspnHttpService httpService, ILogger<EspnScoreboardService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

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

        public Task<IEnumerable<GameEvent>> ExtractEventsAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default)
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

                _logger.LogDebug("Extracted week information for week {WeekNumber}", scoreboard.Week.WeekNumber);
                return Task.FromResult(scoreboard.Week);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract week information from scoreboard");
                throw;
            }
        }

        public Task<IEnumerable<string>> GetEventReferencesAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default)
        {
            try
            {
                var references = new List<string>();

                if (scoreboard?.Events != null)
                {
                    foreach (var gameEvent in scoreboard.Events)
                    {
                        if (!string.IsNullOrEmpty(gameEvent.Id))
                        {
                            // Generate ESPN API reference URL for the event
                            var reference = $"http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/events/{gameEvent.Id}";
                            references.Add(reference);
                        }
                    }
                }

                _logger.LogDebug("Generated {ReferenceCount} event references", references.Count);
                return Task.FromResult<IEnumerable<string>>(references);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate event references from scoreboard");
                throw;
            }
        }

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

            if (sbDataElement.TryGetProperty("events", out var eventsElement) && eventsElement.ValueKind == JsonValueKind.Array)
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

            return events;
        }

        private Season ParseSeason(JsonElement sbDataElement)
        {
            if (sbDataElement.TryGetProperty("leagues", out var leaguesElement) &&
                leaguesElement.ValueKind == JsonValueKind.Array)
            {
                var firstLeague = leaguesElement.EnumerateArray().FirstOrDefault();
                if (firstLeague.ValueKind != JsonValueKind.Undefined &&
                    firstLeague.TryGetProperty("season", out var seasonElement))
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
            }

            return new Season();
        }

        private Week ParseWeek(JsonElement sbDataElement)
        {
            if (sbDataElement.TryGetProperty("week", out var weekElement))
            {
                try
                {
                    return JsonSerializer.Deserialize<Week>(weekElement.GetRawText()) ?? new Week();
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse week information from ESPN response");
                }
            }

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
    }
}