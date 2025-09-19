using ESPNScrape.Models.PlayerMatching;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ESPNScrape.Services
{
    /// <summary>
    /// Service for matching ESPN players against existing database players
    /// </summary>
    public class EspnPlayerMatchingService : IEspnPlayerMatchingService
    {
        private readonly ILogger<EspnPlayerMatchingService> _logger;
        private readonly PlayerMatchingOptions _options;
        private readonly IEspnApiService _espnApiService; // For getting ESPN player data
        private readonly IServiceScopeFactory _scopeFactory; // For database operations

        public EspnPlayerMatchingService(
            ILogger<EspnPlayerMatchingService> logger,
            IOptions<PlayerMatchingOptions> options,
            IEspnApiService espnApiService,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _options = options.Value;
            _espnApiService = espnApiService;
            _scopeFactory = scopeFactory;
        }

        /// <summary>
        /// Find the best matching player in the database for an ESPN player
        /// </summary>
        /// <param name="espnPlayer">ESPN player to match</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Player match result</returns>
        public async Task<PlayerMatchResult> FindMatchingPlayerAsync(
            Models.Player espnPlayer,
            CancellationToken cancellationToken = default)
        {
            return await FindMatchingPlayerAsync(
                espnPlayer.Id,
                espnPlayer.DisplayName,
                espnPlayer.Team?.Abbreviation,
                espnPlayer.Position?.DisplayName,
                cancellationToken);
        }

        /// <summary>
        /// Find matches for multiple ESPN players in batch
        /// </summary>
        /// <param name="espnPlayers">List of ESPN players to match</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of match results</returns>
        public async Task<List<PlayerMatchResult>> FindMatchingPlayersAsync(
            List<Models.Player> espnPlayers,
            CancellationToken cancellationToken = default)
        {
            var playerData = espnPlayers.Select(p => (
                EspnPlayerId: p.Id,
                EspnPlayerName: p.DisplayName,
                Team: p.Team?.Abbreviation,
                Position: p.Position?.DisplayName
            )).ToList();

            return await BulkMatchPlayersAsync(playerData, cancellationToken);
        }

        /// <summary>
        /// Manually link an ESPN player to a database player
        /// </summary>
        /// <param name="databasePlayerId">Database player ID</param>
        /// <param name="espnPlayerId">ESPN player ID</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if link was successful</returns>
        public async Task<bool> LinkPlayerAsync(
            long databasePlayerId,
            string espnPlayerId,
            CancellationToken cancellationToken = default)
        {
            return await LinkPlayerAsync(
                espnPlayerId,
                (int)databasePlayerId,
                1.0, // Full confidence for manual links
                MatchMethod.ManualLink,
                true,
                cancellationToken);
        }

        /// <summary>
        /// Find the best matching player in the database for an ESPN player (internal method)
        /// </summary>
        /// <param name="espnPlayerId">ESPN player ID</param>
        /// <param name="espnPlayerName">ESPN player name</param>
        /// <param name="teamAbbreviation">Team abbreviation (optional)</param>
        /// <param name="position">Position (optional)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Player match result</returns>
        private async Task<PlayerMatchResult> FindMatchingPlayerAsync(
            string espnPlayerId,
            string espnPlayerName,
            string? teamAbbreviation = null,
            string? position = null,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Finding match for ESPN player: {EspnPlayerId} - {EspnPlayerName}",
                espnPlayerId, espnPlayerName);

            try
            {
                // Get all database players as candidates
                var dbPlayers = await GetDatabasePlayersAsync(cancellationToken);

                var candidates = new List<MatchCandidate>();

                foreach (var dbPlayer in dbPlayers)
                {
                    var confidence = CalculateMatchConfidence(
                        espnPlayerName,
                        dbPlayer.Name,
                        teamAbbreviation,
                        dbPlayer.Team,
                        position,
                        dbPlayer.Position);

                    if (confidence >= _options.MinimumConfidenceThreshold)
                    {
                        candidates.Add(new MatchCandidate
                        {
                            DatabasePlayerId = dbPlayer.Id,
                            DatabasePlayerName = dbPlayer.Name,
                            DatabasePlayerTeam = dbPlayer.Team ?? "",
                            DatabasePlayerPosition = dbPlayer.Position ?? "",
                            ConfidenceScore = confidence,
                            MatchReasons = new List<string> { GetPrimaryMatchMethod(espnPlayerName, dbPlayer.Name).ToString() }
                        });
                    }
                }

                // Sort by confidence (highest first)
                candidates = candidates.OrderByDescending(c => c.ConfidenceScore).ToList();

                // Determine match result
                if (!candidates.Any())
                {
                    _logger.LogWarning("No matches found for ESPN player: {EspnPlayerId} - {EspnPlayerName}",
                        espnPlayerId, espnPlayerName);

                    return new PlayerMatchResult
                    {
                        EspnPlayerId = espnPlayerId,
                        EspnPlayerName = espnPlayerName,
                        DatabasePlayerId = null,
                        ConfidenceScore = 0.0,
                        MatchMethod = MatchMethod.None,
                        RequiresManualReview = true,
                        AlternateCandidates = new List<MatchCandidate>()
                    };
                }

                var bestMatch = candidates.First();
                bool requiresManualReview = bestMatch.ConfidenceScore < _options.AutoLinkConfidenceThreshold ||
                                          (candidates.Count > 1 &&
                                           candidates[1].ConfidenceScore > bestMatch.ConfidenceScore - _options.ManualReviewThreshold);

                _logger.LogInformation(
                    "Found match for ESPN player {EspnPlayerId}: {MatchedPlayerName} (Confidence: {Confidence:F2}, Manual Review: {RequiresManualReview})",
                    espnPlayerId, bestMatch.DatabasePlayerName, bestMatch.ConfidenceScore, requiresManualReview);

                return new PlayerMatchResult
                {
                    EspnPlayerId = espnPlayerId,
                    EspnPlayerName = espnPlayerName,
                    DatabasePlayerId = bestMatch.DatabasePlayerId,
                    ConfidenceScore = bestMatch.ConfidenceScore,
                    AlternateCandidates = candidates.Skip(1).Take(5).ToList(), // Top 5 alternates
                    MatchMethod = GetPrimaryMatchMethod(espnPlayerName, bestMatch.DatabasePlayerName),
                    RequiresManualReview = requiresManualReview
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding match for ESPN player: {EspnPlayerId}", espnPlayerId);
                throw;
            }
        }

        /// <summary>
        /// Manually link an ESPN player to a database player (internal method)
        /// </summary>
        /// <param name="espnPlayerId">ESPN player ID</param>
        /// <param name="databasePlayerId">Database player ID</param>
        /// <param name="confidence">Match confidence score</param>
        /// <param name="matchMethod">How the match was determined</param>
        /// <param name="isManualLink">Whether this was manually confirmed</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if link was successful</returns>
        private async Task<bool> LinkPlayerAsync(
            string espnPlayerId,
            int databasePlayerId,
            double confidence,
            MatchMethod matchMethod,
            bool isManualLink = false,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Linking ESPN player {EspnPlayerId} to database player {DatabasePlayerId} " +
                                 "(Confidence: {Confidence:F2}, Method: {MatchMethod}, Manual: {IsManual})",
                espnPlayerId, databasePlayerId, confidence, matchMethod, isManualLink);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                // TODO: Implement database update logic when we have Supabase client
                // This would update the Players table to set espn_player_id

                // For now, log the operation
                _logger.LogInformation("Would update database: SET espn_player_id = '{EspnPlayerId}' WHERE id = {DatabasePlayerId}",
                    espnPlayerId, databasePlayerId);

                return await Task.FromResult(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error linking ESPN player {EspnPlayerId} to database player {DatabasePlayerId}",
                    espnPlayerId, databasePlayerId);
                return false;
            }
        }

        /// <summary>
        /// Get all unmatched ESPN players that need manual review
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of unmatched players</returns>
        public async Task<List<UnmatchedPlayer>> GetUnmatchedPlayersAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting unmatched ESPN players");

            try
            {
                // TODO: Implement when we have ESPN API integration
                // This would get ESPN players that don't have matches in our database

                // For now, return empty list
                return await Task.FromResult(new List<UnmatchedPlayer>());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unmatched players");
                throw;
            }
        }

        /// <summary>
        /// Get matching statistics and performance metrics
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Matching statistics</returns>
        public async Task<MatchingStatistics> GetMatchingStatisticsAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Getting matching statistics");

            try
            {
                // TODO: Implement when we have database queries
                // This would analyze the current state of matches

                return await Task.FromResult(new MatchingStatistics
                {
                    TotalEspnPlayers = 0,
                    SuccessfulMatches = 0,
                    RequiringManualReview = 0,
                    NoMatches = 0,
                    AverageConfidenceScore = 0.0,
                    MethodBreakdown = new Dictionary<MatchMethod, int>()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting matching statistics");
                throw;
            }
        }

        /// <summary>
        /// Bulk match multiple ESPN players
        /// </summary>
        /// <param name="espnPlayerData">List of ESPN player data</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>List of match results</returns>
        public async Task<List<PlayerMatchResult>> BulkMatchPlayersAsync(
            List<(string EspnPlayerId, string EspnPlayerName, string? Team, string? Position)> espnPlayerData,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Bulk matching {Count} ESPN players", espnPlayerData.Count);

            var results = new List<PlayerMatchResult>();

            foreach (var playerData in espnPlayerData)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await FindMatchingPlayerAsync(
                    playerData.EspnPlayerId,
                    playerData.EspnPlayerName,
                    playerData.Team,
                    playerData.Position,
                    cancellationToken);

                results.Add(result);

                // Add small delay to avoid overwhelming the system
                await Task.Delay(10, cancellationToken);
            }

            _logger.LogInformation("Bulk matching completed. Found {MatchCount} matches out of {TotalCount} players",
                results.Count(r => r.DatabasePlayerId.HasValue), espnPlayerData.Count);

            return results;
        }

        /// <summary>
        /// Calculate match confidence between ESPN and database player
        /// </summary>
        private double CalculateMatchConfidence(
            string espnPlayerName,
            string dbPlayerName,
            string? espnTeam = null,
            string? dbTeam = null,
            string? espnPosition = null,
            string? dbPosition = null)
        {
            double confidence = 0.0;

            // Name matching (70% weight)
            double nameScore = CalculateNameMatchScore(espnPlayerName, dbPlayerName);
            confidence += nameScore * 0.7;

            // Team matching (20% weight)
            if (!string.IsNullOrEmpty(espnTeam) && !string.IsNullOrEmpty(dbTeam))
            {
                double teamScore = string.Equals(espnTeam, dbTeam, StringComparison.OrdinalIgnoreCase) ? 1.0 : 0.0;
                confidence += teamScore * 0.2;
            }

            // Position matching (10% weight)
            if (!string.IsNullOrEmpty(espnPosition) && !string.IsNullOrEmpty(dbPosition))
            {
                double positionScore = CalculatePositionMatchScore(espnPosition, dbPosition);
                confidence += positionScore * 0.1;
            }

            return Math.Min(1.0, confidence);
        }

        /// <summary>
        /// Calculate name match score using multiple algorithms
        /// </summary>
        private double CalculateNameMatchScore(string espnName, string dbName)
        {
            if (string.IsNullOrEmpty(espnName) || string.IsNullOrEmpty(dbName))
                return 0.0;

            // Exact match
            if (string.Equals(StringMatchingAlgorithms.NormalizeName(espnName),
                             StringMatchingAlgorithms.NormalizeName(dbName),
                             StringComparison.OrdinalIgnoreCase))
                return 1.0;

            double maxScore = 0.0;

            // Levenshtein similarity
            double levenshteinScore = StringMatchingAlgorithms.CalculateSimilarity(espnName, dbName);
            maxScore = Math.Max(maxScore, levenshteinScore);

            // Jaro-Winkler similarity
            double jaroWinklerScore = StringMatchingAlgorithms.JaroWinklerSimilarity(espnName, dbName);
            maxScore = Math.Max(maxScore, jaroWinklerScore);

            // Phonetic matching bonus
            if (StringMatchingAlgorithms.ArePhoneticallySimilar(espnName, dbName))
            {
                maxScore = Math.Max(maxScore, 0.8);
            }

            // Name variation bonus
            if (StringMatchingAlgorithms.AreNameVariations(espnName, dbName))
            {
                maxScore = Math.Max(maxScore, 0.9);
            }

            // Initials matching (fallback for abbreviated names)
            string espnInitials = StringMatchingAlgorithms.GetInitials(espnName);
            string dbInitials = StringMatchingAlgorithms.GetInitials(dbName);
            if (espnInitials == dbInitials && espnInitials.Length >= 2)
            {
                maxScore = Math.Max(maxScore, 0.6);
            }

            return maxScore;
        }

        /// <summary>
        /// Calculate position match score
        /// </summary>
        private double CalculatePositionMatchScore(string espnPosition, string dbPosition)
        {
            if (string.IsNullOrEmpty(espnPosition) || string.IsNullOrEmpty(dbPosition))
                return 0.0;

            // Exact match
            if (string.Equals(espnPosition, dbPosition, StringComparison.OrdinalIgnoreCase))
                return 1.0;

            // Position group matching (e.g., RB and HB, WR and FL)
            var positionGroups = new Dictionary<string, string[]>
            {
                { "RB", new[] { "HB", "FB" } },
                { "WR", new[] { "FL", "SE" } },
                { "TE", new[] { "TE" } },
                { "QB", new[] { "QB" } },
                { "K", new[] { "PK" } },
                { "DEF", new[] { "DST", "D/ST" } }
            };

            foreach (var group in positionGroups)
            {
                if ((group.Key.Equals(espnPosition, StringComparison.OrdinalIgnoreCase) &&
                     group.Value.Contains(dbPosition, StringComparer.OrdinalIgnoreCase)) ||
                    (group.Key.Equals(dbPosition, StringComparison.OrdinalIgnoreCase) &&
                     group.Value.Contains(espnPosition, StringComparer.OrdinalIgnoreCase)))
                {
                    return 0.8;
                }
            }

            return 0.0;
        }

        /// <summary>
        /// Determine primary match method used
        /// </summary>
        private MatchMethod GetPrimaryMatchMethod(string espnName, string dbName)
        {
            if (string.Equals(StringMatchingAlgorithms.NormalizeName(espnName),
                             StringMatchingAlgorithms.NormalizeName(dbName),
                             StringComparison.OrdinalIgnoreCase))
                return MatchMethod.ExactNameMatch;

            if (StringMatchingAlgorithms.AreNameVariations(espnName, dbName))
                return MatchMethod.NameVariation;

            if (StringMatchingAlgorithms.ArePhoneticallySimilar(espnName, dbName))
                return MatchMethod.PhoneticMatch;

            double similarity = StringMatchingAlgorithms.CalculateSimilarity(espnName, dbName);
            if (similarity > 0.8)
                return MatchMethod.FuzzyNameMatch;

            return MatchMethod.MultipleFactors;
        }

        /// <summary>
        /// Get all database players for matching
        /// </summary>
        private async Task<List<(int Id, string Name, string? Team, string? Position)>> GetDatabasePlayersAsync(
            CancellationToken cancellationToken)
        {
            // TODO: Implement database query when we have Supabase client
            // This would query the Players table to get all players for matching

            // For now, return empty list
            _logger.LogWarning("GetDatabasePlayersAsync not yet implemented - need Supabase client integration");
            return await Task.FromResult(new List<(int Id, string Name, string? Team, string? Position)>());
        }
    }
}