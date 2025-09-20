using System.Text.Json.Serialization;

namespace ESPNScrape.Models.DataSync
{
    /// <summary>
    /// Type of synchronization operation
    /// </summary>
    public enum SyncType
    {
        /// <summary>
        /// Player roster synchronization
        /// </summary>
        Players,

        /// <summary>
        /// Player statistics synchronization
        /// </summary>
        PlayerStats,

        /// <summary>
        /// Full data synchronization
        /// </summary>
        Full,

        /// <summary>
        /// Incremental data synchronization
        /// </summary>
        Incremental
    }

    /// <summary>
    /// Status of a synchronization operation
    /// </summary>
    public enum SyncStatus
    {
        /// <summary>
        /// Sync operation is running
        /// </summary>
        Running,

        /// <summary>
        /// Sync completed successfully
        /// </summary>
        Completed,

        /// <summary>
        /// Sync completed with warnings
        /// </summary>
        CompletedWithWarnings,

        /// <summary>
        /// Sync failed
        /// </summary>
        Failed,

        /// <summary>
        /// Sync was cancelled
        /// </summary>
        Cancelled,

        /// <summary>
        /// Sync was partially completed
        /// </summary>
        PartiallyCompleted
    }

    /// <summary>
    /// Configuration options for sync operations
    /// </summary>
    public class SyncOptions
    {
        /// <summary>
        /// Force a full synchronization instead of incremental
        /// </summary>
        public bool ForceFullSync { get; set; } = false;

        /// <summary>
        /// Skip inactive players during sync
        /// </summary>
        public bool SkipInactives { get; set; } = true;

        /// <summary>
        /// Number of records to process in each batch
        /// </summary>
        public int BatchSize { get; set; } = 100;

        /// <summary>
        /// Perform a dry run without making changes
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Maximum number of retries for failed operations
        /// </summary>
        public int MaxRetries { get; set; } = 3;

        /// <summary>
        /// Delay between retries in milliseconds
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Timeout for the entire sync operation in minutes
        /// </summary>
        public int TimeoutMinutes { get; set; } = 60;

        /// <summary>
        /// Include detailed progress reporting
        /// </summary>
        public bool EnableDetailedLogging { get; set; } = false;

        /// <summary>
        /// Validate data before insertion
        /// </summary>
        public bool ValidateData { get; set; } = true;

        /// <summary>
        /// Skip records that fail validation instead of failing the entire sync
        /// </summary>
        public bool SkipInvalidRecords { get; set; } = true;

        /// <summary>
        /// Create backup before making changes
        /// </summary>
        public bool CreateBackup { get; set; } = false;

        /// <summary>
        /// Specific player IDs to sync (null for all players)
        /// </summary>
        public List<string>? PlayerIds { get; set; }

        /// <summary>
        /// Specific teams to sync (null for all teams)
        /// </summary>
        public List<string>? TeamAbbreviations { get; set; }
    }

    /// <summary>
    /// Result of a synchronization operation
    /// </summary>
    public class SyncResult
    {
        /// <summary>
        /// Unique identifier for this sync operation
        /// </summary>
        public string SyncId { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Type of synchronization performed
        /// </summary>
        public SyncType SyncType { get; set; }

        /// <summary>
        /// Status of the synchronization
        /// </summary>
        public SyncStatus Status { get; set; }

        /// <summary>
        /// When the sync operation started
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the sync operation completed
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Total duration of the sync operation
        /// </summary>
        public TimeSpan Duration => EndTime?.Subtract(StartTime) ?? TimeSpan.Zero;

        /// <summary>
        /// Total number of records processed
        /// </summary>
        public int RecordsProcessed { get; set; }

        /// <summary>
        /// Number of players processed
        /// </summary>
        public int PlayersProcessed { get; set; }

        /// <summary>
        /// Number of existing players updated
        /// </summary>
        public int PlayersUpdated { get; set; }

        /// <summary>
        /// Number of new players added
        /// </summary>
        public int NewPlayersAdded { get; set; }

        /// <summary>
        /// Number of player stats records processed
        /// </summary>
        public int StatsRecordsProcessed { get; set; }

        /// <summary>
        /// Number of new stats records added
        /// </summary>
        public int NewStatsAdded { get; set; }

        /// <summary>
        /// Number of existing stats updated
        /// </summary>
        public int StatsUpdated { get; set; }

        /// <summary>
        /// Number of player matching errors
        /// </summary>
        public int MatchingErrors { get; set; }

        /// <summary>
        /// Number of data validation errors
        /// </summary>
        public int DataErrors { get; set; }

        /// <summary>
        /// Number of API errors encountered
        /// </summary>
        public int ApiErrors { get; set; }

        /// <summary>
        /// Number of records skipped due to errors
        /// </summary>
        public int RecordsSkipped { get; set; }

        /// <summary>
        /// List of error messages encountered
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// List of warning messages
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Detailed processing information
        /// </summary>
        public List<string> ProcessingDetails { get; set; } = new();

        /// <summary>
        /// Configuration used for this sync
        /// </summary>
        public SyncOptions? Options { get; set; }

        /// <summary>
        /// Success rate as a percentage
        /// </summary>
        public double SuccessRate => RecordsProcessed > 0 ?
            ((double)(RecordsProcessed - DataErrors - MatchingErrors - ApiErrors) / RecordsProcessed) * 100 : 100;

        /// <summary>
        /// Whether the sync was successful overall
        /// </summary>
        public bool IsSuccessful => Status == SyncStatus.Completed || Status == SyncStatus.CompletedWithWarnings;

        /// <summary>
        /// Performance metrics
        /// </summary>
        public double RecordsPerSecond => Duration.TotalSeconds > 0 ? RecordsProcessed / Duration.TotalSeconds : 0;
    }

    /// <summary>
    /// Detailed report of a synchronization operation
    /// </summary>
    public class SyncReport
    {
        /// <summary>
        /// Sync result information
        /// </summary>
        public SyncResult Result { get; set; } = new();

        /// <summary>
        /// Team-specific sync details
        /// </summary>
        public Dictionary<string, TeamSyncDetails> TeamDetails { get; set; } = new();

        /// <summary>
        /// Player-specific issues that require attention
        /// </summary>
        public List<PlayerSyncIssue> PlayerIssues { get; set; } = new();

        /// <summary>
        /// Database changes made during sync
        /// </summary>
        public List<DatabaseChange> DatabaseChanges { get; set; } = new();

        /// <summary>
        /// Performance metrics
        /// </summary>
        public SyncPerformanceMetrics Performance { get; set; } = new();

        /// <summary>
        /// Raw log entries for debugging
        /// </summary>
        public List<SyncLogEntry> LogEntries { get; set; } = new();
    }

    /// <summary>
    /// Team-specific synchronization details
    /// </summary>
    public class TeamSyncDetails
    {
        /// <summary>
        /// Team abbreviation
        /// </summary>
        public string TeamAbbreviation { get; set; } = string.Empty;

        /// <summary>
        /// Number of players processed for this team
        /// </summary>
        public int PlayersProcessed { get; set; }

        /// <summary>
        /// Number of new players added
        /// </summary>
        public int NewPlayers { get; set; }

        /// <summary>
        /// Number of existing players updated
        /// </summary>
        public int UpdatedPlayers { get; set; }

        /// <summary>
        /// Number of errors for this team
        /// </summary>
        public int Errors { get; set; }

        /// <summary>
        /// Processing time for this team
        /// </summary>
        public TimeSpan ProcessingTime { get; set; }
    }

    /// <summary>
    /// Player-specific sync issue
    /// </summary>
    public class PlayerSyncIssue
    {
        /// <summary>
        /// ESPN player ID
        /// </summary>
        public string EspnPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// Player name from ESPN
        /// </summary>
        public string PlayerName { get; set; } = string.Empty;

        /// <summary>
        /// Type of issue encountered
        /// </summary>
        public PlayerIssueType IssueType { get; set; }

        /// <summary>
        /// Description of the issue
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Suggested resolution
        /// </summary>
        public string SuggestedResolution { get; set; } = string.Empty;

        /// <summary>
        /// Whether this issue requires manual intervention
        /// </summary>
        public bool RequiresManualIntervention { get; set; }

        /// <summary>
        /// When the issue was detected
        /// </summary>
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Type of player sync issue
    /// </summary>
    public enum PlayerIssueType
    {
        /// <summary>
        /// No matching player found in database
        /// </summary>
        NoMatch,

        /// <summary>
        /// Multiple potential matches found
        /// </summary>
        MultipleMatches,

        /// <summary>
        /// Data validation failed
        /// </summary>
        ValidationError,

        /// <summary>
        /// Player data inconsistency
        /// </summary>
        DataInconsistency,

        /// <summary>
        /// API data missing or incomplete
        /// </summary>
        IncompleteData,

        /// <summary>
        /// Player status change detected
        /// </summary>
        StatusChange
    }

    /// <summary>
    /// Database change made during sync
    /// </summary>
    public class DatabaseChange
    {
        /// <summary>
        /// Type of database operation
        /// </summary>
        public ChangeType ChangeType { get; set; }

        /// <summary>
        /// Table that was modified
        /// </summary>
        public string TableName { get; set; } = string.Empty;

        /// <summary>
        /// Primary key of the record
        /// </summary>
        public string RecordId { get; set; } = string.Empty;

        /// <summary>
        /// Description of the change
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Old values (for updates)
        /// </summary>
        public Dictionary<string, object?> OldValues { get; set; } = new();

        /// <summary>
        /// New values
        /// </summary>
        public Dictionary<string, object?> NewValues { get; set; } = new();

        /// <summary>
        /// When the change was made
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Type of database change
    /// </summary>
    public enum ChangeType
    {
        /// <summary>
        /// New record inserted
        /// </summary>
        Insert,

        /// <summary>
        /// Existing record updated
        /// </summary>
        Update,

        /// <summary>
        /// Record deleted
        /// </summary>
        Delete
    }

    /// <summary>
    /// Performance metrics for sync operation
    /// </summary>
    public class SyncPerformanceMetrics
    {
        /// <summary>
        /// Time spent fetching data from ESPN API
        /// </summary>
        public TimeSpan ApiCallTime { get; set; }

        /// <summary>
        /// Time spent matching players
        /// </summary>
        public TimeSpan PlayerMatchingTime { get; set; }

        /// <summary>
        /// Time spent on database operations
        /// </summary>
        public TimeSpan DatabaseTime { get; set; }

        /// <summary>
        /// Time spent on data validation
        /// </summary>
        public TimeSpan ValidationTime { get; set; }

        /// <summary>
        /// Number of API calls made
        /// </summary>
        public int ApiCallCount { get; set; }

        /// <summary>
        /// Average API response time in milliseconds
        /// </summary>
        public double AverageApiResponseTime { get; set; }

        /// <summary>
        /// Peak memory usage during sync
        /// </summary>
        public long PeakMemoryUsage { get; set; }

        /// <summary>
        /// Database connections used
        /// </summary>
        public int DatabaseConnections { get; set; }
    }

    /// <summary>
    /// Log entry for sync operation
    /// </summary>
    public class SyncLogEntry
    {
        /// <summary>
        /// Log level
        /// </summary>
        public LogLevel Level { get; set; }

        /// <summary>
        /// Log message
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Exception details if applicable
        /// </summary>
        public string? Exception { get; set; }

        /// <summary>
        /// When the log entry was created
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Additional properties
        /// </summary>
        public Dictionary<string, object?> Properties { get; set; } = new();
    }

    /// <summary>
    /// Log level enumeration
    /// </summary>
    public enum LogLevel
    {
        /// <summary>
        /// Debug level
        /// </summary>
        Debug,

        /// <summary>
        /// Information level
        /// </summary>
        Information,

        /// <summary>
        /// Warning level
        /// </summary>
        Warning,

        /// <summary>
        /// Error level
        /// </summary>
        Error,

        /// <summary>
        /// Critical level
        /// </summary>
        Critical
    }

    /// <summary>
    /// Result of ESPN connectivity validation
    /// </summary>
    public class SyncValidationResult
    {
        /// <summary>
        /// Whether ESPN API is accessible
        /// </summary>
        public bool IsEspnApiAccessible { get; set; }

        /// <summary>
        /// Whether database is accessible
        /// </summary>
        public bool IsDatabaseAccessible { get; set; }

        /// <summary>
        /// ESPN API response time in milliseconds
        /// </summary>
        public double EspnApiResponseTime { get; set; }

        /// <summary>
        /// Database response time in milliseconds
        /// </summary>
        public double DatabaseResponseTime { get; set; }

        /// <summary>
        /// Available data endpoints
        /// </summary>
        public List<string> AvailableEndpoints { get; set; } = new();

        /// <summary>
        /// Any validation errors encountered
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new();

        /// <summary>
        /// Overall validation success
        /// </summary>
        public bool IsValid => IsEspnApiAccessible && IsDatabaseAccessible && !ValidationErrors.Any();

        /// <summary>
        /// When the validation was performed
        /// </summary>
        public DateTime ValidatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Sync metrics and statistics
    /// </summary>
    public class SyncMetrics
    {
        /// <summary>
        /// Date range for these metrics
        /// </summary>
        public DateTime FromDate { get; set; }

        /// <summary>
        /// End date for these metrics
        /// </summary>
        public DateTime ToDate { get; set; }

        /// <summary>
        /// Total number of sync operations
        /// </summary>
        public int TotalSyncOperations { get; set; }

        /// <summary>
        /// Number of successful syncs
        /// </summary>
        public int SuccessfulSyncs { get; set; }

        /// <summary>
        /// Number of failed syncs
        /// </summary>
        public int FailedSyncs { get; set; }

        /// <summary>
        /// Average sync duration
        /// </summary>
        public TimeSpan AverageSyncDuration { get; set; }

        /// <summary>
        /// Total records processed
        /// </summary>
        public int TotalRecordsProcessed { get; set; }

        /// <summary>
        /// Total errors encountered
        /// </summary>
        public int TotalErrors { get; set; }

        /// <summary>
        /// Success rate as percentage
        /// </summary>
        public double SuccessRate => TotalSyncOperations > 0 ?
            ((double)SuccessfulSyncs / TotalSyncOperations) * 100 : 100;

        /// <summary>
        /// Average records per sync
        /// </summary>
        public double AverageRecordsPerSync => TotalSyncOperations > 0 ?
            (double)TotalRecordsProcessed / TotalSyncOperations : 0;

        /// <summary>
        /// Breakdown by sync type
        /// </summary>
        public Dictionary<SyncType, SyncTypeMetrics> SyncTypeBreakdown { get; set; } = new();
    }

    /// <summary>
    /// Metrics for a specific sync type
    /// </summary>
    public class SyncTypeMetrics
    {
        /// <summary>
        /// Number of operations
        /// </summary>
        public int OperationCount { get; set; }

        /// <summary>
        /// Success rate
        /// </summary>
        public double SuccessRate { get; set; }

        /// <summary>
        /// Average duration
        /// </summary>
        public TimeSpan AverageDuration { get; set; }

        /// <summary>
        /// Total records processed
        /// </summary>
        public int TotalRecords { get; set; }
    }

    /// <summary>
    /// Represents player statistics in database format for storage
    /// </summary>
    public class DatabasePlayerStats
    {
        /// <summary>
        /// ESPN Player ID for linking
        /// </summary>
        public string? EspnPlayerId { get; set; }

        /// <summary>
        /// ESPN Game ID for tracking
        /// </summary>
        public string? EspnGameId { get; set; }

        /// <summary>
        /// Link to Players table (if matched)
        /// </summary>
        public long? PlayerId { get; set; }

        /// <summary>
        /// Player display name
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Player code for identification
        /// </summary>
        public string PlayerCode { get; set; } = string.Empty;

        /// <summary>
        /// Team abbreviation
        /// </summary>
        public string Team { get; set; } = string.Empty;

        /// <summary>
        /// Game date
        /// </summary>
        public DateTime GameDate { get; set; }

        /// <summary>
        /// Game location (Home/Away)
        /// </summary>
        public string GameLocation { get; set; } = string.Empty;

        /// <summary>
        /// Season year
        /// </summary>
        public int? Season { get; set; }

        /// <summary>
        /// Week number
        /// </summary>
        public int? Week { get; set; }

        /// <summary>
        /// Season type (regular season, playoffs, etc.)
        /// </summary>
        public int? SeasonType { get; set; }

        /// <summary>
        /// Player position
        /// </summary>
        public string Position { get; set; } = string.Empty;

        /// <summary>
        /// Player jersey number
        /// </summary>
        public string Jersey { get; set; } = string.Empty;

        /// <summary>
        /// Passing statistics as JSONB
        /// </summary>
        public object? Passing { get; set; }

        /// <summary>
        /// Rushing statistics as JSONB
        /// </summary>
        public object? Rushing { get; set; }

        /// <summary>
        /// Receiving statistics as JSONB
        /// </summary>
        public object? Receiving { get; set; }

        /// <summary>
        /// Defensive statistics as JSONB
        /// </summary>
        public object? Defensive { get; set; }

        /// <summary>
        /// Kicking statistics as JSONB
        /// </summary>
        public object? Kicking { get; set; }

        /// <summary>
        /// Punting statistics as JSONB
        /// </summary>
        public object? Punting { get; set; }

        /// <summary>
        /// General/Other statistics as JSONB
        /// </summary>
        public object? General { get; set; }

        /// <summary>
        /// Timestamp when record was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Timestamp when record was last updated
        /// </summary>
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Result of data validation operations
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Whether validation passed
        /// </summary>
        public bool IsValid { get; set; } = true;

        /// <summary>
        /// List of validation errors
        /// </summary>
        public List<string> Errors { get; set; } = new();

        /// <summary>
        /// List of validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Add an error to the validation result
        /// </summary>
        public void AddError(string error)
        {
            Errors.Add(error);
            IsValid = false;
        }

        /// <summary>
        /// Add a warning to the validation result
        /// </summary>
        public void AddWarning(string warning)
        {
            Warnings.Add(warning);
        }

        /// <summary>
        /// Add multiple errors
        /// </summary>
        public void AddErrors(IEnumerable<string> errors)
        {
            foreach (var error in errors)
            {
                AddError(error);
            }
        }

        /// <summary>
        /// Add multiple warnings
        /// </summary>
        public void AddWarnings(IEnumerable<string> warnings)
        {
            foreach (var warning in warnings)
            {
                AddWarning(warning);
            }
        }
    }

    /// <summary>
    /// Organized statistics by category for easier processing
    /// </summary>
    public class StatCategories
    {
        /// <summary>
        /// Passing statistics
        /// </summary>
        public Dictionary<string, decimal> Passing { get; set; } = new();

        /// <summary>
        /// Rushing statistics
        /// </summary>
        public Dictionary<string, decimal> Rushing { get; set; } = new();

        /// <summary>
        /// Receiving statistics
        /// </summary>
        public Dictionary<string, decimal> Receiving { get; set; } = new();

        /// <summary>
        /// Defensive statistics
        /// </summary>
        public Dictionary<string, decimal> Defensive { get; set; } = new();

        /// <summary>
        /// Kicking statistics
        /// </summary>
        public Dictionary<string, decimal> Kicking { get; set; } = new();

        /// <summary>
        /// Punting statistics
        /// </summary>
        public Dictionary<string, decimal> Punting { get; set; } = new();

        /// <summary>
        /// General/Other statistics
        /// </summary>
        public Dictionary<string, decimal> General { get; set; } = new();

        /// <summary>
        /// Check if any stats exist in any category
        /// </summary>
        public bool HasAnyStats =>
            Passing.Any() || Rushing.Any() || Receiving.Any() ||
            Defensive.Any() || Kicking.Any() || Punting.Any() || General.Any();
    }
}