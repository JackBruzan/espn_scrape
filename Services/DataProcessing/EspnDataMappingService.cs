using ESPNScrape.Models;
using ESPNScrape.Models.Espn;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text.Json;

namespace ESPNScrape.Services.DataProcessing
{
    /// <summary>
    /// Comprehensive service for ESPN data mapping, transformations, and team mappings
    /// </summary>
    public class EspnDataMappingService
    {
        private readonly ILogger<EspnDataMappingService> _logger;

        // ESPN Team ID to team info mappings
        private static readonly Dictionary<string, (string Abbreviation, string DisplayName)> TeamMappings = new()
        {
            // AFC East
            { "2", ("BUF", "Buffalo Bills") },
            { "15", ("MIA", "Miami Dolphins") },
            { "17", ("NE", "New England Patriots") },
            { "20", ("NYJ", "New York Jets") },
            
            // AFC North
            { "33", ("BAL", "Baltimore Ravens") },
            { "4", ("CIN", "Cincinnati Bengals") },
            { "5", ("CLE", "Cleveland Browns") },
            { "23", ("PIT", "Pittsburgh Steelers") },
            
            // AFC South
            { "34", ("HOU", "Houston Texans") },
            { "11", ("IND", "Indianapolis Colts") },
            { "30", ("JAX", "Jacksonville Jaguars") },
            { "10", ("TEN", "Tennessee Titans") },
            
            // AFC West
            { "7", ("DEN", "Denver Broncos") },
            { "12", ("KC", "Kansas City Chiefs") },
            { "13", ("LV", "Las Vegas Raiders") },
            { "24", ("LAC", "Los Angeles Chargers") },
            
            // NFC East
            { "6", ("DAL", "Dallas Cowboys") },
            { "19", ("NYG", "New York Giants") },
            { "21", ("PHI", "Philadelphia Eagles") },
            { "28", ("WAS", "Washington Commanders") },
            
            // NFC North
            { "3", ("CHI", "Chicago Bears") },
            { "8", ("DET", "Detroit Lions") },
            { "9", ("GB", "Green Bay Packers") },
            { "16", ("MIN", "Minnesota Vikings") },
            
            // NFC South
            { "1", ("ATL", "Atlanta Falcons") },
            { "29", ("CAR", "Carolina Panthers") },
            { "18", ("NO", "New Orleans Saints") },
            { "27", ("TB", "Tampa Bay Buccaneers") },
            
            // NFC West
            { "22", ("ARI", "Arizona Cardinals") },
            { "14", ("LAR", "Los Angeles Rams") },
            { "25", ("SF", "San Francisco 49ers") },
            { "26", ("SEA", "Seattle Seahawks") }
        };

        public EspnDataMappingService(ILogger<EspnDataMappingService> logger)
        {
            _logger = logger;
        }

        #region Team Mapping Methods

        /// <summary>
        /// Get team information by ESPN team ID
        /// </summary>
        public (string Abbreviation, string DisplayName)? GetTeamInfo(string teamId)
        {
            if (string.IsNullOrEmpty(teamId))
                return null;

            return TeamMappings.TryGetValue(teamId, out var teamInfo) ? teamInfo : null;
        }

        /// <summary>
        /// Get team abbreviation by ESPN team ID
        /// </summary>
        public string? GetTeamAbbreviation(string teamId)
        {
            var teamInfo = GetTeamInfo(teamId);
            return teamInfo?.Abbreviation;
        }

        /// <summary>
        /// Get team display name by ESPN team ID
        /// </summary>
        public string? GetTeamDisplayName(string teamId)
        {
            var teamInfo = GetTeamInfo(teamId);
            return teamInfo?.DisplayName;
        }

        /// <summary>
        /// Converts ESPN team abbreviations to database team abbreviations
        /// </summary>
        public string ConvertEspnTeamAbbreviation(string espnAbbreviation)
        {
            return espnAbbreviation?.ToUpperInvariant().Trim() switch
            {
                "ARI" => "ARI",
                "ATL" => "ATL",
                "BAL" => "BAL",
                "BUF" => "BUF",
                "CAR" => "CAR",
                "CHI" => "CHI",
                "CIN" => "CIN",
                "CLE" => "CLE",
                "DAL" => "DAL",
                "DEN" => "DEN",
                "DET" => "DET",
                "GB" => "GNB",   // ESPN uses GB, database uses GNB
                "HOU" => "HOU",
                "IND" => "IND",
                "JAX" => "JAX",
                "KC" => "KAN",   // ESPN uses KC, database uses KAN
                "LV" => "LVR",   // ESPN uses LV, database uses LVR
                "LAC" => "LAC",
                "LAR" => "LAR",
                "MIA" => "MIA",
                "MIN" => "MIN",
                "NE" => "NWE",   // ESPN uses NE, database uses NWE
                "NO" => "NOR",   // ESPN uses NO, database uses NOR
                "NYG" => "NYG",
                "NYJ" => "NYJ",
                "PHI" => "PHI",
                "PIT" => "PIT",
                "SF" => "SFO",   // ESPN uses SF, database uses SFO
                "SEA" => "SEA",
                "TB" => "TAM",   // ESPN uses TB, database uses TAM
                "TEN" => "TEN",
                "WSH" => "WAS",  // ESPN uses WSH, database uses WAS
                _ => espnAbbreviation?.ToUpperInvariant() ?? "UNK"
            };
        }

        #endregion

        #region Schedule and Event Conversion Methods

        /// <summary>
        /// Convert ESPN API response to Schedule objects
        /// </summary>
        public IEnumerable<Schedule> ConvertEspnEventsToSchedules(EspnScheduleResponse espnResponse, int year, int week, int seasonType)
        {
            var schedules = new List<Schedule>();

            if (espnResponse?.Items == null)
            {
                _logger.LogWarning("No event references found in ESPN response");
                return schedules;
            }

            _logger.LogInformation("ESPN API returned {Count} event references. Full event data retrieval not yet implemented.", espnResponse.Items.Count);

            // FUTURE FEATURE: Implement reference resolution to fetch full event details
            // Currently only supports direct event data from ESPN API responses.
            // To support event references (eventRef.Ref URLs):
            // 1. Make additional API calls to resolve each eventRef.Ref URL
            // 2. Convert the resolved full event data to Schedule objects
            // 3. Update the service architecture to handle multiple sequential API calls efficiently

            return schedules;
        }

        /// <summary>
        /// Legacy method for converting ESPN events to schedules (when API returned full events)
        /// </summary>
        public IEnumerable<Schedule> ConvertEspnEventsToSchedules_Legacy(EspnScheduleResponse espnResponse, int year, int week, int seasonType)
        {
            var schedules = new List<Schedule>();

            // This is the legacy method for when ESPN API returned full events directly
            // Currently not used since ESPN API returns references
            _logger.LogWarning("Legacy method called - ESPN API now returns references, not full events");
            return schedules;

            /* Original implementation for reference:
            if (espnResponse?.Events == null)
            {
                _logger.LogWarning("No events found in ESPN response");
                return schedules;
            }

            foreach (var espnEvent in espnResponse.Events)
            {
                try
                {
                    var schedule = ConvertEspnEventToSchedule(espnEvent, year, week, seasonType);
                    if (schedule != null)
                    {
                        schedules.Add(schedule);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error converting ESPN event {EventId} to Schedule", espnEvent.Id);
                }
            }

            _logger.LogInformation("Converted {Count} ESPN events to Schedule objects", schedules.Count);
            return schedules;
            */
        }

        /// <summary>
        /// Convert ESPN event JSON string to Schedule object
        /// </summary>
        public Schedule? ConvertEspnEventToSchedule(string eventJson, int year, int week, int seasonType)
        {
            try
            {
                var jsonOptions = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var espnEvent = JsonSerializer.Deserialize<EspnEvent>(eventJson, jsonOptions);
                if (espnEvent == null)
                {
                    _logger.LogWarning("Failed to deserialize ESPN event JSON");
                    return null;
                }

                return ConvertEspnEventToSchedule(espnEvent, year, week, seasonType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ESPN event JSON");
                return null;
            }
        }

        /// <summary>
        /// Convert ESPN event object to Schedule object
        /// </summary>
        public Schedule? ConvertEspnEventToSchedule(EspnEvent espnEvent, int year, int week, int seasonType)
        {
            if (espnEvent?.Competitions == null || !espnEvent.Competitions.Any())
            {
                _logger.LogWarning("ESPN event {EventId} does not have competitions", espnEvent?.Id);
                return null;
            }

            var competition = espnEvent.Competitions.First();

            // Get competitors from the competition
            var competitors = competition.Competitors;

            if (competitors == null || competitors.Count != 2)
            {
                _logger.LogWarning("ESPN event {EventId} does not have exactly 2 competitors in competition", espnEvent?.Id);
                return null;
            }

            var homeTeam = competitors.FirstOrDefault(c => c.HomeAway.Equals("home", StringComparison.OrdinalIgnoreCase));
            var awayTeam = competitors.FirstOrDefault(c => c.HomeAway.Equals("away", StringComparison.OrdinalIgnoreCase));

            if (homeTeam == null || awayTeam == null)
            {
                _logger.LogWarning("ESPN event {EventId} missing home or away team data", espnEvent.Id);
                return null;
            }

            // Use team ID to get team names from mapping service
            var homeTeamInfo = GetTeamInfo(homeTeam.Id);
            var awayTeamInfo = GetTeamInfo(awayTeam.Id);

            if (homeTeamInfo == null || awayTeamInfo == null)
            {
                _logger.LogWarning("ESPN event {EventId} has unknown team IDs: Home={HomeTeamId}, Away={AwayTeamId}",
                    espnEvent.Id, homeTeam.Id, awayTeam.Id);
            }

            var competitionId = competition?.Id ?? string.Empty;

            var schedule = new Schedule
            {
                EspnGameId = espnEvent.Id,
                EspnCompetitionId = competitionId,
                HomeTeamName = homeTeamInfo?.Abbreviation ?? $"Team-{homeTeam.Id}",
                AwayTeamName = awayTeamInfo?.Abbreviation ?? $"Team-{awayTeam.Id}",
                GameTime = espnEvent.Date,
                Week = week,
                Year = year,
                SeasonType = seasonType
            };

            _logger.LogDebug("Converted ESPN event {EventId}: {AwayTeam} @ {HomeTeam} on {GameTime}",
                espnEvent.Id, schedule.AwayTeamName, schedule.HomeTeamName, schedule.GameTime);

            return schedule;
        }

        #endregion

        #region Odds Mapping Methods

        /// <summary>
        /// Apply ESPN odds data to a Schedule object
        /// </summary>
        public void ApplyOddsToSchedule(Schedule schedule, EspnOdds odds)
        {
            if (schedule == null || odds == null)
            {
                return;
            }

            try
            {
                _logger.LogDebug("Applying odds data to schedule for game {GameId}", schedule.EspnGameId);

                // Set betting provider
                schedule.BettingProvider = odds.Provider?.DisplayName ?? "ESPN BET";
                schedule.OddsLastUpdated = DateTime.UtcNow;

                // Apply point spread data using direct fields
                ApplyPointSpreadDataFromOdds(schedule, odds);

                // Apply total (over/under) data
                ApplyTotalData(schedule, odds);

                // Apply moneyline data using direct fields
                ApplyMoneylineDataFromOdds(schedule, odds);

                // Calculate implied points
                schedule.CalculateImpliedPoints();

                _logger.LogDebug("Successfully applied odds data to schedule for game {GameId}", schedule.EspnGameId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying odds data to schedule for game {GameId}", schedule.EspnGameId);
            }
        }

        /// <summary>
        /// Apply point spread data from ESPN odds to schedule
        /// </summary>
        private void ApplyPointSpreadDataFromOdds(Schedule schedule, EspnOdds oddsData)
        {
            // Use direct spread field from ESPN API
            if (oddsData.Spread.HasValue)
            {
                schedule.BettingLine = oddsData.Spread.Value;

                // Set spread odds - prefer home team odds if available
                if (oddsData.HomeTeamOdds.SpreadOdds.HasValue)
                {
                    schedule.SpreadOdds = oddsData.HomeTeamOdds.SpreadOdds.Value.ToString();
                }
                else if (oddsData.AwayTeamOdds.SpreadOdds.HasValue)
                {
                    schedule.SpreadOdds = oddsData.AwayTeamOdds.SpreadOdds.Value.ToString();
                }

                _logger.LogDebug("Successfully set spread for game {GameId}: {Spread} (Odds: {SpreadOdds})",
                    schedule.EspnGameId, schedule.BettingLine, schedule.SpreadOdds);
            }
            else
            {
                _logger.LogDebug("No spread data available for game {GameId}", schedule.EspnGameId);
            }
        }

        /// <summary>
        /// Apply total (over/under) data from ESPN odds to schedule
        /// </summary>
        private void ApplyTotalData(Schedule schedule, EspnOdds oddsData)
        {
            // Use the direct OverUnder field from the ESPN API response
            if (oddsData.OverUnder.HasValue)
            {
                schedule.OverUnder = oddsData.OverUnder.Value;

                // Also set the over/under odds if available
                if (oddsData.OverOdds.HasValue)
                {
                    schedule.OverOdds = oddsData.OverOdds.Value.ToString();
                }
                if (oddsData.UnderOdds.HasValue)
                {
                    schedule.UnderOdds = oddsData.UnderOdds.Value.ToString();
                }

                _logger.LogDebug("Successfully set over/under for game {GameId}: {Total} (Over: {OverOdds}, Under: {UnderOdds})",
                    schedule.EspnGameId, schedule.OverUnder, schedule.OverOdds, schedule.UnderOdds);
            }
            else
            {
                _logger.LogDebug("No direct over/under data available for game {GameId}", schedule.EspnGameId);
            }
        }

        /// <summary>
        /// Apply moneyline data from ESPN odds to schedule
        /// </summary>
        private void ApplyMoneylineDataFromOdds(Schedule schedule, EspnOdds oddsData)
        {
            // Use direct moneyline fields from ESPN API team odds
            if (oddsData.HomeTeamOdds.MoneyLine.HasValue)
            {
                schedule.HomeMoneyline = oddsData.HomeTeamOdds.MoneyLine.Value;
                _logger.LogDebug("Set home moneyline for game {GameId}: {HomeML}", schedule.EspnGameId, schedule.HomeMoneyline);
            }

            if (oddsData.AwayTeamOdds.MoneyLine.HasValue)
            {
                schedule.AwayMoneyline = oddsData.AwayTeamOdds.MoneyLine.Value;
                _logger.LogDebug("Set away moneyline for game {GameId}: {AwayML}", schedule.EspnGameId, schedule.AwayMoneyline);
            }

            if (schedule.HomeMoneyline.HasValue && schedule.AwayMoneyline.HasValue)
            {
                _logger.LogDebug("Successfully set moneylines for game {GameId}: Home {HomeML}, Away {AwayML}",
                    schedule.EspnGameId, schedule.HomeMoneyline, schedule.AwayMoneyline);
            }
        }

        #endregion

        #region Deprecated Methods (Legacy Compatibility)

        /// <summary>
        /// Deprecated method for applying point spread data
        /// </summary>
        [Obsolete("Use ApplyPointSpreadDataFromOdds instead")]
        private void ApplyPointSpreadData(Schedule schedule, EspnPointSpread pointSpread)
        {
            // This method is deprecated - we now use the direct spread field from EspnOdds
            // Left for compatibility but should be removed once we verify the new approach works
            _logger.LogDebug("ApplyPointSpreadData called but using direct odds fields instead for game {GameId}", schedule.EspnGameId);
        }

        /// <summary>
        /// Deprecated method for applying moneyline data
        /// </summary>
        [Obsolete("Use ApplyMoneylineDataFromOdds instead")]
        private void ApplyMoneylineData(Schedule schedule, EspnMoneyline moneyline)
        {
            // This method is deprecated - we now use the direct moneyline fields from EspnOdds
            // Left for compatibility but should be removed once we verify the new approach works
            _logger.LogDebug("ApplyMoneylineData called but using direct odds fields instead for game {GameId}", schedule.EspnGameId);
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// Extract numeric values from ESPN betting strings
        /// </summary>
        private string ExtractNumericValue(string input)
        {
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Remove common prefixes like 'o', 'u', '+', and extract numeric part
            var match = Regex.Match(input, @"[-+]?\d+\.?\d*");
            return match.Success ? match.Value : string.Empty;
        }

        #endregion
    }
}