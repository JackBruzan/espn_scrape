using ESPNScrape.Models;
using ESPNScrape.Models.Supabase;
using ESPNScrape.Services.Interfaces;
using HtmlAgilityPack;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ESPNScrape.Services
{
    public class EspnScheduleService : IEspnScheduleService
    {
        private readonly IEspnHttpService _httpService;
        private readonly ISupabaseDatabaseService _databaseService;
        private readonly IEspnCoreApiService _coreApiService;
        private readonly EspnApiDataMappingService _mappingService;
        private readonly ILogger<EspnScheduleService> _logger;

        // ESPN URL template for schedule
        private const string ScheduleUrlTemplate = "https://www.espn.com/nfl/schedule/_/week/{0}/year/{1}/seasontype/{2}";

        // Regex patterns for parsing betting information
        private static readonly Regex BettingLineRegex = new(@"Line:\s*([A-Z]{2,3})\s*([-+]?\d+\.?\d*)", RegexOptions.Compiled);
        private static readonly Regex OverUnderRegex = new(@"O/U:\s*(\d+\.?\d*)", RegexOptions.Compiled);

        public EspnScheduleService(
            IEspnHttpService httpService,
            ISupabaseDatabaseService databaseService,
            IEspnCoreApiService coreApiService,
            EspnApiDataMappingService mappingService,
            ILogger<EspnScheduleService> logger)
        {
            _httpService = httpService;
            _databaseService = databaseService;
            _coreApiService = coreApiService;
            _mappingService = mappingService;
            _logger = logger;
        }

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
                        _logger.LogWarning(ex, "Failed to fetch/convert event details from {Url}", eventRef.Ref);
                    }
                }

                _logger.LogInformation("Successfully converted {Count} games from ESPN Core API for Week {Week}, {Year}",
                    schedules.Count, week, year);

                // Resolve team names to team IDs
                await ResolveTeamIds(schedules, cancellationToken);

                // Get odds data for each game
                await FetchAndApplyOddsData(schedules, cancellationToken);

                return schedules;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching schedule from ESPN Core API for Week {Week}, Year {Year}, SeasonType {SeasonType}",
                    week, year, seasonType);
                throw;
            }
        }

        private async Task ResolveTeamIds(IList<Schedule> schedules, CancellationToken cancellationToken)
        {
            foreach (var schedule in schedules)
            {
                // Use team abbreviation from ESPN API if available, otherwise convert team name
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
            }
        }

        private async Task FetchAndApplyOddsData(IList<Schedule> schedules, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Fetching odds data for {Count} games", schedules.Count);

            var gameCompetitionPairs = schedules
                .Where(s => !string.IsNullOrEmpty(s.EspnGameId) && !string.IsNullOrEmpty(s.EspnCompetitionId))
                .Select(s => (s.EspnGameId, s.EspnCompetitionId))
                .ToList();

            if (!gameCompetitionPairs.Any())
            {
                _logger.LogWarning("No valid game/competition ID pairs found for odds data");
                return;
            }

            // Apply odds data to each schedule individually to ensure proper matching
            foreach (var schedule in schedules)
            {
                if (!string.IsNullOrEmpty(schedule.EspnGameId) && !string.IsNullOrEmpty(schedule.EspnCompetitionId))
                {
                    var oddsData = await _coreApiService.GetEventOddsAsync(schedule.EspnGameId, schedule.EspnCompetitionId, cancellationToken);
                    if (oddsData != null)
                    {
                        _mappingService.ApplyOddsToSchedule(schedule, oddsData);
                        _logger.LogDebug("Applied odds to game {GameId}: Over/Under {OverUnder}",
                            schedule.EspnGameId, schedule.OverUnder);
                    }
                    else
                    {
                        _logger.LogWarning("No odds data found for game {GameId}", schedule.EspnGameId);
                    }
                }
            }

            _logger.LogInformation("Applied odds data to {Count} games", schedules.Count);
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

        private IEnumerable<Schedule> ParseScheduleFromHtml(string htmlContent, int year, int week, int seasonType)
        {
            var schedules = new List<Schedule>();

            try
            {
                var doc = new HtmlDocument();
                doc.LoadHtml(htmlContent);

                // Look for table rows or schedule entries in the ESPN page
                // ESPN typically shows schedule in format: "Team @ Team" or similar patterns
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

        private void ValidateParameters(int year, int week, int seasonType)
        {
            if (year < 2000 || year > DateTime.Now.Year + 1)
                throw new ArgumentException($"Invalid year: {year}");

            if (week < 1 || week > 22)
                throw new ArgumentException($"Invalid week: {week}");

            if (seasonType < 1 || seasonType > 4)
                throw new ArgumentException($"Invalid season type: {seasonType}");
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
    }
}