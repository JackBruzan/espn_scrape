using System.Text.Json.Serialization;

namespace ESPNScrape.Models.PlayerMatching
{
    /// <summary>
    /// Result of matching an ESPN player to a database player
    /// </summary>
    public class PlayerMatchResult
    {
        /// <summary>
        /// Database player ID if match was found
        /// </summary>
        public long? DatabasePlayerId { get; set; }

        /// <summary>
        /// ESPN player ID being matched
        /// </summary>
        public string EspnPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// ESPN player display name for reference
        /// </summary>
        public string EspnPlayerName { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score of the match (0.0 to 1.0)
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Method used to find the match
        /// </summary>
        public MatchMethod MatchMethod { get; set; }

        /// <summary>
        /// List of reasons contributing to the match
        /// </summary>
        public List<string> MatchReasons { get; set; } = new();

        /// <summary>
        /// Whether this match requires manual review due to low confidence
        /// </summary>
        public bool RequiresManualReview { get; set; }

        /// <summary>
        /// Timestamp when the match was performed
        /// </summary>
        public DateTime MatchedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional candidate matches found during the search
        /// </summary>
        public List<MatchCandidate> AlternateCandidates { get; set; } = new();
    }

    /// <summary>
    /// Method used to match players
    /// </summary>
    public enum MatchMethod
    {
        /// <summary>
        /// No match found
        /// </summary>
        None,

        /// <summary>
        /// Exact match on first name, last name, and team
        /// </summary>
        ExactNameMatch,

        /// <summary>
        /// Exact match on first name, last name, and team
        /// </summary>
        ExactNameAndTeam,

        /// <summary>
        /// Fuzzy match on names with exact team match
        /// </summary>
        FuzzyNameMatch,

        /// <summary>
        /// Fuzzy match on names with exact team match
        /// </summary>
        FuzzyNameAndTeam,

        /// <summary>
        /// Exact match on names with position validation
        /// </summary>
        ExactNameAndPosition,

        /// <summary>
        /// Fuzzy match on names only (lowest confidence)
        /// </summary>
        FuzzyNameOnly,

        /// <summary>
        /// Phonetic matching using Soundex
        /// </summary>
        PhoneticMatch,

        /// <summary>
        /// Name variation matching (nicknames)
        /// </summary>
        NameVariation,

        /// <summary>
        /// Multiple factors combined
        /// </summary>
        MultipleFactors,

        /// <summary>
        /// Manual link established by user
        /// </summary>
        ManualLink,

        /// <summary>
        /// No match found
        /// </summary>
        NoMatch
    }

    /// <summary>
    /// A potential match candidate during player matching
    /// </summary>
    public class MatchCandidate
    {
        /// <summary>
        /// Database player ID of the candidate
        /// </summary>
        public long DatabasePlayerId { get; set; }

        /// <summary>
        /// Database player name
        /// </summary>
        public string DatabasePlayerName { get; set; } = string.Empty;

        /// <summary>
        /// Team abbreviation of the database player
        /// </summary>
        public string DatabasePlayerTeam { get; set; } = string.Empty;

        /// <summary>
        /// Position of the database player
        /// </summary>
        public string DatabasePlayerPosition { get; set; } = string.Empty;

        /// <summary>
        /// Confidence score for this candidate
        /// </summary>
        public double ConfidenceScore { get; set; }

        /// <summary>
        /// Reasons why this candidate was considered
        /// </summary>
        public List<string> MatchReasons { get; set; } = new();
    }

    /// <summary>
    /// ESPN player that couldn't be matched automatically
    /// </summary>
    public class UnmatchedPlayer
    {
        /// <summary>
        /// ESPN player ID
        /// </summary>
        public string EspnPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// ESPN player display name
        /// </summary>
        public string EspnPlayerName { get; set; } = string.Empty;

        /// <summary>
        /// ESPN player first name
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// ESPN player last name
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// ESPN player team abbreviation
        /// </summary>
        public string TeamAbbreviation { get; set; } = string.Empty;

        /// <summary>
        /// ESPN player position
        /// </summary>
        public string Position { get; set; } = string.Empty;

        /// <summary>
        /// Whether the player is active
        /// </summary>
        public bool IsActive { get; set; }

        /// <summary>
        /// Best match candidates found
        /// </summary>
        public List<MatchCandidate> BestCandidates { get; set; } = new();

        /// <summary>
        /// Reason why automatic matching failed
        /// </summary>
        public string FailureReason { get; set; } = string.Empty;

        /// <summary>
        /// When the match attempt was made
        /// </summary>
        public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Statistics about the player matching process
    /// </summary>
    public class MatchingStatistics
    {
        /// <summary>
        /// Total number of ESPN players processed
        /// </summary>
        public int TotalEspnPlayers { get; set; }

        /// <summary>
        /// Number of players successfully matched
        /// </summary>
        public int SuccessfulMatches { get; set; }

        /// <summary>
        /// Number of players requiring manual review
        /// </summary>
        public int RequiringManualReview { get; set; }

        /// <summary>
        /// Number of players that couldn't be matched
        /// </summary>
        public int NoMatches { get; set; }

        /// <summary>
        /// Overall matching success rate
        /// </summary>
        public double SuccessRate => TotalEspnPlayers > 0 ? (double)SuccessfulMatches / TotalEspnPlayers : 0.0;

        /// <summary>
        /// Breakdown by match method
        /// </summary>
        public Dictionary<MatchMethod, int> MethodBreakdown { get; set; } = new();

        /// <summary>
        /// Average confidence score for successful matches
        /// </summary>
        public double AverageConfidenceScore { get; set; }

        /// <summary>
        /// When these statistics were calculated
        /// </summary>
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }
}