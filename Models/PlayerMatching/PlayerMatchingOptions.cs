namespace ESPNScrape.Models.PlayerMatching
{
    /// <summary>
    /// Configuration options for player matching behavior
    /// </summary>
    public class PlayerMatchingOptions
    {
        /// <summary>
        /// Configuration section name in appsettings.json
        /// </summary>
        public const string SectionName = "PlayerMatching";

        /// <summary>
        /// Minimum confidence threshold for considering a match (default: 0.5)
        /// </summary>
        public double MinimumConfidenceThreshold { get; set; } = 0.5;

        /// <summary>
        /// Confidence threshold for automatic linking without manual review (default: 0.9)
        /// </summary>
        public double AutoLinkConfidenceThreshold { get; set; } = 0.9;

        /// <summary>
        /// If second-best match is within this threshold of best match, require manual review (default: 0.1)
        /// </summary>
        public double ManualReviewThreshold { get; set; } = 0.1;

        /// <summary>
        /// Maximum number of alternate candidates to return (default: 5)
        /// </summary>
        public int MaxAlternateCandidates { get; set; } = 5;

        /// <summary>
        /// Enable phonetic matching (Soundex) (default: true)
        /// </summary>
        public bool EnablePhoneticMatching { get; set; } = true;

        /// <summary>
        /// Enable name variation matching (nicknames) (default: true)
        /// </summary>
        public bool EnableNameVariationMatching { get; set; } = true;

        /// <summary>
        /// Weight for name matching in confidence calculation (default: 0.7)
        /// </summary>
        public double NameMatchWeight { get; set; } = 0.7;

        /// <summary>
        /// Weight for team matching in confidence calculation (default: 0.2)
        /// </summary>
        public double TeamMatchWeight { get; set; } = 0.2;

        /// <summary>
        /// Weight for position matching in confidence calculation (default: 0.1)
        /// </summary>
        public double PositionMatchWeight { get; set; } = 0.1;

        /// <summary>
        /// Delay between bulk match operations in milliseconds (default: 10)
        /// </summary>
        public int BulkMatchDelayMs { get; set; } = 10;

        /// <summary>
        /// Enable detailed logging for match operations (default: false)
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;
    }
}