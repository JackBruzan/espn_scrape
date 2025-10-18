using ESPNScrape.Models.Espn;
using ESPNScrape.Models.DataSync;
using ESPNScrape.Services.Interfaces;


namespace ESPNScrape.Services
{
    /// <summary>
    /// Service for transforming ESPN player statistics into database format
    /// </summary>
    public class EspnStatsTransformationService : IEspnStatsTransformationService
    {
        private readonly ILogger<EspnStatsTransformationService> _logger;

        // Stat category mappings for classification
        private static readonly Dictionary<string, string> StatCategoryMappings = new()
        {
            // Passing stats
            { "passingCompletions", "passing" },
            { "passingAttempts", "passing" },
            { "passingYards", "passing" },
            { "passingTouchdowns", "passing" },
            { "passingInterceptions", "passing" },
            { "passingRating", "passing" },
            { "passingQBR", "passing" },
            { "passingSacks", "passing" },
            { "passingLong", "passing" },
            { "completions", "passing" },
            { "attempts", "passing" },
            { "comp-att", "passing" },
            { "C/ATT", "passing" },
            { "interceptions", "passing" }, // ESPN QB interceptions from standard stats array

            // Rushing stats
            { "rushingAttempts", "rushing" },
            { "rushingCarries", "rushing" },
            { "rushingYards", "rushing" },
            { "rushingTouchdowns", "rushing" },
            { "rushingAverage", "rushing" },
            { "rushingLong", "rushing" },
            { "carries", "rushing" },

            // Receiving stats
            { "receivingReceptions", "receiving" },
            { "receivingTargets", "receiving" },
            { "receivingYards", "receiving" },
            { "receivingTouchdowns", "receiving" },
            { "receivingAverage", "receiving" },
            { "receivingLong", "receiving" },
            { "receptions", "receiving" },
            { "targets", "receiving" },

            // Defensive stats
            { "totalTackles", "defensive" },
            { "soloTackles", "defensive" },
            { "assistTackles", "defensive" },
            { "sacks", "defensive" },
            { "defensiveInterceptions", "defensive" },
            { "passesDefended", "defensive" },
            { "forcedFumbles", "defensive" },
            { "fumbleRecoveries", "defensive" },
            { "defensiveTouchdowns", "defensive" },

            // Kicking stats
            { "fieldGoalsMade", "kicking" },
            { "fieldGoalsAttempted", "kicking" },
            { "extraPointsMade", "kicking" },
            { "extraPointsAttempted", "kicking" },
            { "fieldGoals", "kicking" },
            { "extraPoints", "kicking" },
            { "fieldgoalsmade_fieldgoalattempts", "kicking" },
            { "extrapointsmade_extrapointattempts", "kicking" },

            // Punting stats
            { "punts", "punting" },
            { "puntingYards", "punting" },
            { "puntingAverage", "punting" },
            { "puntingLong", "punting" },
            { "puntingInside20", "punting" }
        };

        // Validation ranges for realistic stat values
        private static readonly Dictionary<string, (decimal Min, decimal Max)> StatValidationRanges = new()
        {
            // Passing stats validation ranges
            { "passingCompletions", (0, 80) },
            { "passingAttempts", (0, 100) },
            { "passingYards", (-50, 800) },
            { "passingTouchdowns", (0, 12) },
            { "passingInterceptions", (0, 10) },
            { "passingRating", (0, 158.3m) },
            { "passingQBR", (0, 100) },
            { "passingSacks", (0, 15) },
            { "passingLong", (0, 99) },

            // Rushing stats validation ranges
            { "rushingAttempts", (0, 50) },
            { "rushingCarries", (0, 50) },
            { "rushingYards", (-30, 400) },
            { "rushingTouchdowns", (0, 8) },
            { "rushingAverage", (-5, 50) },
            { "rushingLong", (0, 99) },

            // Receiving stats validation ranges
            { "receivingReceptions", (0, 25) },
            { "receivingTargets", (0, 30) },
            { "receivingYards", (-20, 400) },
            { "receivingTouchdowns", (0, 6) },
            { "receivingAverage", (-10, 80) },
            { "receivingLong", (0, 99) },

            // Defensive stats validation ranges
            { "totalTackles", (0, 30) },
            { "soloTackles", (0, 25) },
            { "assistTackles", (0, 15) },
            { "sacks", (0, 8) },
            { "interceptions", (0, 5) },
            { "passesDefended", (0, 10) },
            { "forcedFumbles", (0, 5) },
            { "fumbleRecoveries", (0, 5) },
            { "defensiveTouchdowns", (0, 3) },

            // Kicking stats validation ranges
            { "fieldGoalsMade", (0, 8) },
            { "fieldGoalsAttempted", (0, 10) },
            { "extraPointsMade", (0, 10) },
            { "extraPointsAttempted", (0, 12) },

            // Punting stats validation ranges
            { "punts", (0, 15) },
            { "puntingYards", (0, 1000) },
            { "puntingAverage", (20, 65) },
            { "puntingLong", (20, 90) },
            { "puntingInside20", (0, 10) },

            // Fumbles stats validation ranges
            { "fumbles", (0, 10) },
            { "fumblesLost", (0, 8) }
        };

        public EspnStatsTransformationService(ILogger<EspnStatsTransformationService> logger)
        {
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
                "WSH" => "Washington Commanders", // ESPN sometimes uses WSH
                _ => espnAbbreviation ?? string.Empty
            };
        }

        /// <summary>
        /// Transform a single ESPN player stats object to database format
        /// </summary>
        public async Task<DatabasePlayerStats> TransformPlayerStatsAsync(PlayerStats espnStats, CancellationToken cancellationToken = default)
        {
            _logger.LogDebug("Transforming player stats for Player ID: {PlayerId}, Game ID: {GameId}",
                espnStats.PlayerId, espnStats.GameId);

            // Organize stats by category
            var organizedStats = OrganizeStatsByCategory(espnStats.Statistics);

            // Extract fumble statistics
            var (fumbles, fumblesLost) = ExtractFumbleStats(espnStats.Statistics);

            // Create database player stats object
            var dbStats = new DatabasePlayerStats
            {
                EspnPlayerId = espnStats.PlayerId,
                EspnGameId = espnStats.GameId,
                Name = espnStats.DisplayName,
                PlayerCode = GeneratePlayerCode(espnStats),
                Team = MapEspnTeamToFullName(espnStats.Team?.Abbreviation ?? string.Empty),
                Season = espnStats.Season,
                Week = espnStats.Week,
                SeasonType = espnStats.SeasonType,
                Position = espnStats.Position?.Abbreviation ?? string.Empty,
                Jersey = espnStats.Jersey,
                GameDate = DateTime.UtcNow, // This should be set from game context in actual implementation
                GameLocation = "TBD", // This should be determined from game context

                // Transform organized stats to JSONB format
                Passing = organizedStats.Passing.Any() ? organizedStats.Passing : null,
                Rushing = organizedStats.Rushing.Any() ? organizedStats.Rushing : null,
                Receiving = organizedStats.Receiving.Any() ? organizedStats.Receiving : null,
                Defensive = organizedStats.Defensive.Any() ? organizedStats.Defensive : null,
                Kicking = organizedStats.Kicking.Any() ? organizedStats.Kicking : null,
                Punting = organizedStats.Punting.Any() ? organizedStats.Punting : null,
                General = organizedStats.General.Any() ? organizedStats.General : null,

                // Add fumble statistics
                Fumbles = fumbles > 0 ? fumbles : null,
                FumblesLost = fumblesLost > 0 ? fumblesLost : null
            };

            _logger.LogDebug("Successfully transformed player stats for Player: {PlayerName}", espnStats.DisplayName);
            return await Task.FromResult(dbStats);
        }

        /// <summary>
        /// Transform multiple ESPN player stats objects to database format in batch
        /// </summary>
        public async Task<List<DatabasePlayerStats>> TransformPlayerStatsBatchAsync(List<PlayerStats> espnStatsList, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting batch transformation of {Count} player stats", espnStatsList.Count);

            var transformedStats = new List<DatabasePlayerStats>();
            var errors = new List<string>();

            foreach (var espnStats in espnStatsList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var transformedStat = await TransformPlayerStatsAsync(espnStats, cancellationToken);
                    transformedStats.Add(transformedStat);
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Failed to transform stats for player {espnStats.PlayerId}: {ex.Message}";
                    errors.Add(errorMsg);
                    _logger.LogWarning(ex, "Error transforming player stats for Player ID: {PlayerId}", espnStats.PlayerId);
                }
            }

            if (errors.Any())
            {
                _logger.LogWarning("Completed batch transformation with {ErrorCount} errors out of {TotalCount} records",
                    errors.Count, espnStatsList.Count);
            }
            else
            {
                _logger.LogInformation("Successfully completed batch transformation of {Count} player stats", espnStatsList.Count);
            }

            return transformedStats;
        }

        /// <summary>
        /// Validate ESPN player stats for data integrity and realistic ranges
        /// </summary>
        public Models.DataSync.ValidationResult ValidatePlayerStats(PlayerStats espnStats)
        {
            var result = new Models.DataSync.ValidationResult();

            // Validate required fields
            if (string.IsNullOrEmpty(espnStats.PlayerId))
                result.AddError("Player ID is required");

            if (string.IsNullOrEmpty(espnStats.DisplayName))
                result.AddError("Player display name is required");

            if (string.IsNullOrEmpty(espnStats.GameId))
                result.AddError("Game ID is required");

            if (espnStats.Position == null || string.IsNullOrEmpty(espnStats.Position.Abbreviation))
                result.AddWarning("Player position is missing or incomplete");

            if (espnStats.Team == null || string.IsNullOrEmpty(espnStats.Team.Abbreviation))
                result.AddWarning("Team information is missing or incomplete");

            // Validate season and week
            if (espnStats.Season < 1920 || espnStats.Season > DateTime.Now.Year + 1)
                result.AddError($"Season {espnStats.Season} is not in valid range (1920-{DateTime.Now.Year + 1})");

            if (espnStats.Week < 1 || espnStats.Week > 22)
                result.AddWarning($"Week {espnStats.Week} may be outside normal range (1-22)");

            // Validate individual statistics
            foreach (var stat in espnStats.Statistics)
            {
                ValidateIndividualStat(stat, result);
            }

            _logger.LogDebug("Validation completed for Player {PlayerId}: {ValidationStatus}",
                espnStats.PlayerId, result.IsValid ? "Valid" : "Invalid");

            return result;
        }

        /// <summary>
        /// Transform ESPN player statistics list into organized stat categories
        /// </summary>
        public StatCategories OrganizeStatsByCategory(List<PlayerStatistic> statistics)
        {
            var categories = new StatCategories();

            foreach (var stat in statistics)
            {
                var category = DetermineStatCategory(stat.Name);
                var statKey = NormalizeStatName(stat.Name);

                switch (category.ToLower())
                {
                    case "passing":
                        categories.Passing[statKey] = stat.Value;
                        break;
                    case "rushing":
                        categories.Rushing[statKey] = stat.Value;
                        break;
                    case "receiving":
                        categories.Receiving[statKey] = stat.Value;
                        break;
                    case "defensive":
                        categories.Defensive[statKey] = stat.Value;
                        break;
                    case "kicking":
                        categories.Kicking[statKey] = stat.Value;
                        break;
                    case "punting":
                        categories.Punting[statKey] = stat.Value;
                        break;
                    default:
                        categories.General[statKey] = stat.Value;
                        break;
                }
            }

            return categories;
        }

        #region Private Helper Methods

        /// <summary>
        /// Determine the category of a statistic based on its name
        /// </summary>
        private string DetermineStatCategory(string statName)
        {
            var normalizedName = statName.ToLower().Trim();

            // Check direct mappings first
            if (StatCategoryMappings.TryGetValue(statName, out var directCategory))
                return directCategory;

            if (StatCategoryMappings.TryGetValue(normalizedName, out var normalizedCategory))
                return normalizedCategory;

            // Fallback to keyword matching
            if (normalizedName.Contains("pass") || normalizedName.Contains("completion") ||
                normalizedName.Contains("attempt") || normalizedName.Contains("qbr") ||
                normalizedName.Contains("rating"))
                return "passing";

            if (normalizedName.Contains("rush") || normalizedName.Contains("carr"))
                return "rushing";

            if (normalizedName.Contains("rec") || normalizedName.Contains("target") ||
                normalizedName.Contains("catch"))
                return "receiving";

            // Special handling for "interceptions" - context matters
            // If it's exactly "interceptions" from QB stats, treat as passing
            // This handles ESPN's standard QB stats array where they use "interceptions"
            if (normalizedName == "interceptions")
                return "passing";

            if (normalizedName.Contains("sack") || normalizedName.Contains("tackle") ||
                normalizedName.Contains("int") || normalizedName.Contains("fumble") ||
                normalizedName.Contains("def"))
                return "defensive";

            if (normalizedName.Contains("kick") || normalizedName.Contains("fg") ||
                normalizedName.Contains("xp") || normalizedName.Contains("extra") ||
                normalizedName.Contains("fieldgoal") || normalizedName.Contains("extrapoint"))
                return "kicking";

            if (normalizedName.Contains("punt"))
                return "punting";

            return "general";
        }

        /// <summary>
        /// Normalize stat name for consistent storage
        /// </summary>
        private string NormalizeStatName(string statName)
        {
            return statName.ToLower()
                .Replace(" ", "_")
                .Replace("-", "_")
                .Replace("/", "_")
                .Trim();
        }

        /// <summary>
        /// Generate a player code for identification
        /// </summary>
        private string GeneratePlayerCode(PlayerStats espnStats)
        {
            var nameParts = espnStats.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var lastName = nameParts.LastOrDefault() ?? "Unknown";
            var firstInitial = nameParts.FirstOrDefault()?.Substring(0, 1) ?? "X";

            return $"{lastName.ToUpper()}{firstInitial.ToUpper()}{espnStats.PlayerId}";
        }

        /// <summary>
        /// Validate an individual statistic
        /// </summary>
        private void ValidateIndividualStat(PlayerStatistic stat, Models.DataSync.ValidationResult result)
        {
            // Check for null or empty values
            if (string.IsNullOrEmpty(stat.Name))
            {
                result.AddError("Statistic name cannot be null or empty");
                return;
            }

            // Check for negative values where they shouldn't exist
            if (stat.Value < 0 && !IsNegativeStatAllowed(stat.Name))
            {
                result.AddWarning($"Negative value for {stat.Name}: {stat.Value} may be incorrect");
            }

            // Check against validation ranges
            var normalizedName = NormalizeStatName(stat.Name);
            if (StatValidationRanges.TryGetValue(normalizedName, out var range))
            {
                if (stat.Value < range.Min || stat.Value > range.Max)
                {
                    result.AddWarning($"Stat {stat.Name} value {stat.Value} is outside expected range ({range.Min}-{range.Max})");
                }
            }

            // Check for extreme outliers
            if (stat.Value > 1000 && !IsHighValueStatAllowed(stat.Name))
            {
                result.AddWarning($"Unusually high value for {stat.Name}: {stat.Value}");
            }
        }

        /// <summary>
        /// Check if negative values are allowed for a statistic
        /// </summary>
        private bool IsNegativeStatAllowed(string statName)
        {
            var lowerName = statName.ToLower();
            return lowerName.Contains("yard") || lowerName.Contains("average") ||
                   lowerName.Contains("net") || lowerName.Contains("lost");
        }

        /// <summary>
        /// Check if high values are allowed for a statistic
        /// </summary>
        private bool IsHighValueStatAllowed(string statName)
        {
            var lowerName = statName.ToLower();
            return lowerName.Contains("yard") || lowerName.Contains("total") ||
                   lowerName.Contains("season") || lowerName.Contains("career");
        }

        /// <summary>
        /// Extract fumbles and fumbles lost from ESPN player statistics
        /// </summary>
        private (int fumbles, int fumblesLost) ExtractFumbleStats(List<PlayerStatistic> statistics)
        {
            var fumbles = 0;
            var fumblesLost = 0;

            foreach (var stat in statistics)
            {
                switch (stat.Name.ToLower())
                {
                    case "fumbles":
                        fumbles = (int)Math.Round(stat.Value);
                        break;
                    case "fumbleslost":
                        fumblesLost = (int)Math.Round(stat.Value);
                        break;
                }
            }

            return (fumbles, fumblesLost);
        }

        #endregion
    }
}