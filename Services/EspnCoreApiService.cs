using ESPNScrape.Models.Espn;
using ESPNScrape.Services.Interfaces;
using System.Text.Json;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Service for calling ESPN's Core API endpoints for schedule and odds data
    /// </summary>
    public class EspnCoreApiService : IEspnCoreApiService
    {
        private readonly IEspnHttpService _httpService;
        private readonly ILogger<EspnCoreApiService> _logger;

        // ESPN Core API base URL
        private const string ApiBaseUrl = "https://sports.core.api.espn.com/v2";

        // ESPN schedule API endpoint template
        private const string ScheduleUrlTemplate = "{0}/sports/football/leagues/nfl/seasons/{1}/types/{2}/weeks/{3}/events";

        // ESPN odds API endpoint template
        private const string OddsUrlTemplate = "{0}/sports/football/leagues/nfl/events/{1}/competitions/{2}/odds";

        private readonly JsonSerializerOptions _jsonOptions;

        public EspnCoreApiService(
            IEspnHttpService httpService,
            ILogger<EspnCoreApiService> logger)
        {
            _httpService = httpService;
            _logger = logger;

            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        }

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

        private void ValidateParameters(int year, int week, int seasonType)
        {
            if (year < 2000 || year > DateTime.Now.Year + 1)
                throw new ArgumentException($"Invalid year: {year}");

            if (week < 1 || week > 22)
                throw new ArgumentException($"Invalid week: {week}");

            if (seasonType < 1 || seasonType > 4)
                throw new ArgumentException($"Invalid season type: {seasonType}");
        }
    }
}