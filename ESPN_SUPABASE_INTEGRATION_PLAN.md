# ESPN Supabase Integration Development Plan

## Overview
This plan outlines the development tickets required to integrate ESPN API data into the existing Supabase database. The integration will focus on player matching, data storage, and maintaining data consistency between ESPN and the existing fantasy football database.

## Current State Analysis

### Existing Database Structure
- **Players Table**: 4,095 records with multiple external IDs (sleeper_id, pfr_player_code, fantasy_sharks_player_id)
- **PlayerStats Table**: 41 records with JSONB columns for passing, rushing, receiving stats
- **Teams/Positions**: Reference tables for normalization

### ESPN Data Structure
- **Player Model**: Contains ESPN player ID, names, team, position, active status
- **PlayerStats Model**: Comprehensive game statistics with multiple stat categories

### Integration Challenges
1. **Player Matching**: Need to link ESPN players to existing database players
2. **Data Schema**: Current PlayerStats table may need enhancement for ESPN data structure
3. **Data Synchronization**: Ensure data consistency and avoid duplicates
4. **Performance**: Bulk operations for large datasets

---

## Development Tickets

## 🎯 PHASE 1: Database Schema & Infrastructure

### TICKET-001: Add ESPN Player ID to Players Table
**Priority**: HIGH  
**Estimated Effort**: 2-4 hours  
**Dependencies**: None

#### Objective
Add ESPN player ID column to the Players table to enable linking between ESPN data and existing players.

#### Acceptance Criteria
- [ ] Add `espn_player_id` column to Players table (TEXT, nullable, indexed)
- [ ] Create database migration for the schema change
- [ ] Add unique constraint on espn_player_id where not null
- [ ] Update any existing queries that reference the Players table

#### Implementation Details
```sql
-- Migration: add_espn_player_id_to_players.sql
ALTER TABLE "Players" 
ADD COLUMN espn_player_id TEXT NULL;

CREATE INDEX idx_players_espn_player_id 
ON "Players"(espn_player_id) 
WHERE espn_player_id IS NOT NULL;

ALTER TABLE "Players" 
ADD CONSTRAINT uk_players_espn_player_id 
UNIQUE (espn_player_id);
```

#### Testing Requirements
- [ ] Unit tests for migration rollback
- [ ] Integration tests for Players table queries with new column
- [ ] Performance tests for index effectiveness

---

### TICKET-002: Enhance PlayerStats Table for ESPN Data
**Priority**: HIGH  
**Estimated Effort**: 4-6 hours  
**Dependencies**: TICKET-001

#### Objective
Modify the PlayerStats table to accommodate ESPN-specific data structure and improve data organization.

#### Acceptance Criteria
- [ ] Add ESPN-specific columns for better data tracking
- [ ] Create optimized indexes for query performance  
- [ ] Maintain backward compatibility with existing data
- [ ] Support both existing JSON structure and new ESPN structure

#### Implementation Details
```sql
-- Migration: enhance_playerstats_for_espn.sql
ALTER TABLE "PlayerStats" ADD COLUMN espn_player_id TEXT NULL;
ALTER TABLE "PlayerStats" ADD COLUMN espn_game_id TEXT NULL;
ALTER TABLE "PlayerStats" ADD COLUMN season INTEGER NULL;
ALTER TABLE "PlayerStats" ADD COLUMN week INTEGER NULL;

-- Indexes for performance
CREATE INDEX idx_playerstats_espn_player_id ON "PlayerStats"(espn_player_id);
CREATE INDEX idx_playerstats_espn_game_id ON "PlayerStats"(espn_game_id);
CREATE INDEX idx_playerstats_season_week ON "PlayerStats"(season, week);
CREATE INDEX idx_playerstats_composite ON "PlayerStats"(espn_player_id, season, week);

-- Foreign key to Players table
ALTER TABLE "PlayerStats" 
ADD CONSTRAINT fk_playerstats_espn_player_id 
FOREIGN KEY (espn_player_id) 
REFERENCES "Players"(espn_player_id);
```

#### Testing Requirements
- [ ] Unit tests for all new columns
- [ ] Migration tests (up and down)
- [ ] Performance tests for new indexes
- [ ] Data integrity tests for foreign key constraints

---

### TICKET-003: Create ESPN Player Matching Service
**Priority**: HIGH  
**Estimated Effort**: 8-12 hours  
**Dependencies**: TICKET-001

#### Objective
Develop a service to intelligently match ESPN players with existing database players using multiple data points.

#### Acceptance Criteria
- [ ] Implement fuzzy name matching algorithm
- [ ] Use team and position for additional matching confidence
- [ ] Handle edge cases (traded players, name variations, etc.)
- [ ] Provide confidence scoring for matches
- [ ] Support manual review for low-confidence matches
- [ ] Log all matching decisions for audit

#### Implementation Details
**File: `Services/EspnPlayerMatchingService.cs`**
```csharp
public interface IEspnPlayerMatchingService
{
    Task<PlayerMatchResult> FindMatchingPlayerAsync(Player espnPlayer);
    Task<List<PlayerMatchResult>> FindMatchingPlayersAsync(List<Player> espnPlayers);
    Task<bool> LinkPlayerAsync(long databasePlayerId, string espnPlayerId);
    Task<List<UnmatchedPlayer>> GetUnmatchedPlayersAsync();
}

public class PlayerMatchResult
{
    public long? DatabasePlayerId { get; set; }
    public string EspnPlayerId { get; set; }
    public double ConfidenceScore { get; set; }
    public MatchMethod MatchMethod { get; set; }
    public List<string> MatchReasons { get; set; }
    public bool RequiresManualReview { get; set; }
}

public enum MatchMethod
{
    ExactNameAndTeam,
    FuzzyNameAndTeam,
    ExactNameAndPosition,
    FuzzyNameOnly,
    ManualLink
}
```

**Key Matching Logic:**
1. **Exact Match**: First name + Last name + Team abbreviation
2. **Fuzzy Match**: Levenshtein distance < 2 for names + Team/Position
3. **Position Validation**: Ensure positions are compatible
4. **Team History**: Check for recent trades/transfers

#### Testing Requirements
- [ ] Unit tests for all matching algorithms
- [ ] Integration tests with database
- [ ] Performance tests with large datasets
- [ ] Edge case tests (special characters, Jr/Sr, etc.)
- [ ] Mock tests for external dependencies

---

## 🔄 PHASE 2: Data Synchronization & ETL

### TICKET-004: Create ESPN Data Sync Service
**Priority**: HIGH  
**Estimated Effort**: 10-14 hours  
**Dependencies**: TICKET-002, TICKET-003

#### Objective
Develop a comprehensive service to synchronize ESPN player data with the database, including both player information and statistics.

#### Acceptance Criteria
- [ ] Sync player roster data from ESPN
- [ ] Handle player updates (team changes, status changes)
- [ ] Implement incremental sync capabilities
- [ ] Support full data refresh when needed
- [ ] Provide detailed sync reporting and logging
- [ ] Handle rate limiting and API failures gracefully

#### Implementation Details
**File: `Services/EspnDataSyncService.cs`**
```csharp
public interface IEspnDataSyncService
{
    Task<SyncResult> SyncPlayersAsync(SyncOptions options = null);
    Task<SyncResult> SyncPlayerStatsAsync(int season, int week, SyncOptions options = null);
    Task<SyncResult> SyncPlayerStatsForDateRangeAsync(DateTime startDate, DateTime endDate);
    Task<SyncReport> GetLastSyncReportAsync();
}

public class SyncResult
{
    public int PlayersProcessed { get; set; }
    public int PlayersUpdated { get; set; }
    public int NewPlayersAdded { get; set; }
    public int MatchingErrors { get; set; }
    public int DataErrors { get; set; }
    public TimeSpan Duration { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class SyncOptions
{
    public bool ForceFullSync { get; set; } = false;
    public bool SkipInactives { get; set; } = true;
    public int BatchSize { get; set; } = 100;
    public bool DryRun { get; set; } = false;
}
```

#### Key Features
1. **Incremental Sync**: Only process changed/new data
2. **Batch Processing**: Handle large datasets efficiently
3. **Error Recovery**: Continue processing despite individual failures
4. **Rollback Support**: Ability to undo problematic syncs
5. **Monitoring**: Comprehensive logging and metrics

#### Testing Requirements
- [ ] Unit tests for all sync operations
- [ ] Integration tests with database and ESPN API
- [ ] Performance tests with large datasets
- [ ] Error handling tests (API failures, network issues)
- [ ] Transaction tests (rollback scenarios)

---

### TICKET-005: Implement ESPN Stats Transformation Service
**Priority**: MEDIUM  
**Estimated Effort**: 6-8 hours  
**Dependencies**: TICKET-002

#### Objective
Create a service to transform ESPN player statistics into the database schema format, handling data validation and normalization.

#### Acceptance Criteria
- [ ] Transform ESPN PlayerStats model to database format
- [ ] Validate all statistical data before insertion
- [ ] Handle missing or invalid data gracefully
- [ ] Support core stat categories (passing, rushing, receiving)
- [ ] Maintain data lineage and audit trails

#### Implementation Details
**File: `Services/EspnStatsTransformationService.cs`**
```csharp
public interface IEspnStatsTransformationService
{
    Task<DatabasePlayerStats> TransformPlayerStatsAsync(PlayerStats espnStats);
    Task<List<DatabasePlayerStats>> TransformPlayerStatsBatchAsync(List<PlayerStats> espnStatsList);
    ValidationResult ValidatePlayerStats(PlayerStats espnStats);
}

public class DatabasePlayerStats
{
    public string? EspnPlayerId { get; set; }
    public string? EspnGameId { get; set; }
    public long? PlayerId { get; set; } // Link to Players table
    public string Name { get; set; }
    public string PlayerCode { get; set; }
    public string Team { get; set; }
    public DateTime GameDate { get; set; }
    public string GameLocation { get; set; }
    public int? Season { get; set; }
    public int? Week { get; set; }
    
    // Existing JSONB fields enhanced
    public object? Passing { get; set; }
    public object? Rushing { get; set; }
    public object? Receiving { get; set; }
}
```

#### Data Transformation Rules
1. **Stat Categories**: Map ESPN passing, rushing, and receiving stats to appropriate JSONB fields
2. **Data Types**: Convert ESPN values to appropriate database types
3. **Missing Data**: Use null for missing optional fields, defaults for required
4. **Validation**: Ensure realistic ranges for all statistics
5. **Deduplication**: Prevent duplicate stat entries for same player/game

#### Testing Requirements
- [ ] Unit tests for all transformation methods
- [ ] Validation tests for edge cases and invalid data
- [ ] Performance tests for batch operations
- [ ] Data integrity tests
- [ ] Regression tests for existing data compatibility

---

## 🚀 PHASE 3: Integration & Automation

### TICKET-006: Create ESPN Integration Scheduled Jobs
**Priority**: MEDIUM  
**Estimated Effort**: 4-6 hours  
**Dependencies**: TICKET-004, TICKET-005

#### Objective
Implement scheduled jobs using Quartz.NET to automatically sync ESPN data at regular intervals.

#### Acceptance Criteria
- [ ] Daily player roster sync job
- [ ] Weekly stats sync job for current season
- [ ] Historical data backfill job (manual trigger)
- [ ] Job monitoring and failure alerts
- [ ] Configurable sync schedules
- [ ] Job status dashboard/endpoints

#### Implementation Details
**File: `Jobs/EspnPlayerSyncJob.cs`**
```csharp
[DisallowConcurrentExecution]
public class EspnPlayerSyncJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // Implementation with comprehensive error handling
    }
}

[DisallowConcurrentExecution]
public class EspnStatsSyncJob : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        // Weekly stats sync implementation
    }
}
```

**Job Configuration:**
- **Player Sync**: Daily at 3 AM EST
- **Stats Sync**: Every Tuesday at 4 AM EST (after Monday Night Football)
- **Historical Backfill**: Manual trigger only

#### Testing Requirements
- [ ] Unit tests for job execution logic
- [ ] Integration tests with scheduler
- [ ] Job failure and recovery tests
- [ ] Configuration tests
- [ ] Monitoring endpoint tests

---

### TICKET-007: Implement ESPN Integration API Endpoints
**Priority**: LOW  
**Estimated Effort**: 6-8 hours  
**Dependencies**: TICKET-004, TICKET-005, TICKET-006

#### Objective
Create REST API endpoints for manual ESPN data operations and monitoring.

#### Acceptance Criteria
- [ ] Trigger manual sync operations
- [ ] View sync status and history
- [ ] Player matching management endpoints
- [ ] Data validation and reporting endpoints
- [ ] Administrative controls for data management

#### Implementation Details
**File: `Controllers/EspnIntegrationController.cs`**
```csharp
[ApiController]
[Route("api/[controller]")]
public class EspnIntegrationController : ControllerBase
{
    [HttpPost("sync/players")]
    public async Task<ActionResult<SyncResult>> TriggerPlayerSync([FromBody] SyncOptions options)
    
    [HttpPost("sync/stats")]
    public async Task<ActionResult<SyncResult>> TriggerStatsSync([FromBody] StatsSyncRequest request)
    
    [HttpGet("sync/status")]
    public async Task<ActionResult<SyncStatus>> GetSyncStatus()
    
    [HttpGet("players/unmatched")]
    public async Task<ActionResult<List<UnmatchedPlayer>>> GetUnmatchedPlayers()
    
    [HttpPost("players/link")]
    public async Task<ActionResult> LinkPlayer([FromBody] PlayerLinkRequest request)
    
    [HttpGet("reports/sync-history")]
    public async Task<ActionResult<List<SyncReport>>> GetSyncHistory([FromQuery] int days = 30)
}
```

#### API Features
1. **Manual Triggers**: Allow on-demand sync operations
2. **Status Monitoring**: Real-time sync status and progress
3. **Player Management**: Handle unmatched players and manual linking
4. **Reporting**: Historical sync reports and statistics
5. **Authentication**: Secure endpoints with appropriate authorization

#### Testing Requirements
- [ ] Unit tests for all controller actions
- [ ] Integration tests with services
- [ ] API contract tests
- [ ] Authentication/authorization tests
- [ ] Error handling tests

---

## 🧪 PHASE 4: Testing & Quality Assurance

### TICKET-008: Comprehensive Integration Testing Suite
**Priority**: HIGH  
**Estimated Effort**: 8-10 hours  
**Dependencies**: All previous tickets

#### Objective
Develop comprehensive integration tests to ensure the entire ESPN integration works correctly end-to-end.

#### Acceptance Criteria
- [ ] End-to-end integration tests for complete sync workflows
- [ ] Database integration tests with real Supabase instance
- [ ] ESPN API integration tests with rate limiting
- [ ] Performance tests for large datasets
- [ ] Data consistency validation tests

#### Implementation Details
**File: `Tests/Integration/EspnIntegrationTests.cs`**
```csharp
public class EspnIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task CompletePlayerSyncWorkflow_ShouldSucceed()
    
    [Fact]
    public async Task PlayerStatsSync_ShouldPreserveDataIntegrity()
    
    [Fact]
    public async Task PlayerMatching_ShouldHandleEdgeCases()
    
    [Theory]
    [InlineData(100), InlineData(1000), InlineData(5000)]
    public async Task BulkSync_ShouldHandleLargeDatasets(int playerCount)
}
```

#### Test Categories
1. **Workflow Tests**: Complete sync processes from start to finish
2. **Data Integrity**: Verify data consistency across operations
3. **Performance Tests**: Ensure acceptable performance under load
4. **Error Scenarios**: Test failure handling and recovery
5. **API Integration**: Test actual ESPN API integration

#### Testing Requirements
- [ ] All integration tests pass consistently
- [ ] Performance benchmarks meet requirements
- [ ] Error scenarios are handled gracefully
- [ ] Data consistency is maintained
- [ ] Tests can run in CI/CD pipeline

---

### TICKET-009: Monitoring and Alerting Implementation
**Priority**: MEDIUM  
**Estimated Effort**: 4-6 hours  
**Dependencies**: TICKET-006, TICKET-007

#### Objective
Implement comprehensive monitoring and alerting for the ESPN integration to ensure reliable operation.

#### Acceptance Criteria
- [ ] Health checks for ESPN integration components
- [ ] Performance metrics collection and reporting
- [ ] Automated alerts for sync failures
- [ ] Data quality monitoring and alerts
- [ ] Dashboard for operational visibility

#### Implementation Details
**File: `HealthChecks/EspnIntegrationHealthCheck.cs`**
```csharp
public class EspnIntegrationHealthCheck : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // Check ESPN API connectivity
        // Verify database connectivity
        // Check last sync status
        // Validate data freshness
    }
}
```

**Monitoring Metrics:**
- Sync success/failure rates
- Data processing times
- Player matching accuracy
- API response times
- Database performance metrics

#### Testing Requirements
- [ ] Health check tests
- [ ] Metrics collection tests
- [ ] Alert trigger tests
- [ ] Dashboard functionality tests

---

## 📋 Implementation Guidelines

### Code Quality Standards
- **Unit Test Coverage**: Minimum 80% for all new code
- **Integration Tests**: All major workflows must have integration tests
- **Error Handling**: Comprehensive error handling with appropriate logging
- **Documentation**: All public methods must have XML documentation
- **Code Review**: All code must pass peer review before merge

### Performance Requirements
- **Sync Performance**: Process 1000 players in < 2 minutes
- **API Response**: All endpoints respond in < 500ms (95th percentile)
- **Database Operations**: Bulk operations complete in acceptable timeframes
- **Memory Usage**: No memory leaks during long-running operations

### Security Considerations
- **API Keys**: Store ESPN API credentials securely
- **Database Access**: Use least privilege principles
- **Input Validation**: Validate all external data before processing
- **Audit Logging**: Log all data modification operations

### Deployment Strategy
1. **Database Migrations**: Deploy schema changes first
2. **Service Deployment**: Deploy services with feature flags
3. **Job Scheduling**: Activate scheduled jobs after validation
4. **API Endpoints**: Enable endpoints after testing
5. **Monitoring**: Activate monitoring and alerting

---

## 🎯 Success Criteria

### Functional Requirements Met
- [ ] All ESPN players successfully matched to database players
- [ ] Player statistics accurately stored and retrievable
- [ ] Automated sync processes run reliably
- [ ] Manual sync operations available when needed
- [ ] Data quality maintained throughout operations

### Technical Requirements Met
- [ ] All tests pass (unit, integration, performance)
- [ ] Code coverage exceeds 80%
- [ ] Performance benchmarks achieved
- [ ] Security standards met
- [ ] Documentation complete

### Operational Requirements Met
- [ ] Monitoring and alerting functional
- [ ] Support runbooks available
- [ ] Deployment process documented
- [ ] Rollback procedures tested
- [ ] Training materials provided

---

## 📚 Additional Resources

### ESPN API Documentation
- Player API endpoints and data structure
- Statistics API endpoints and data structure
- Rate limiting and best practices
- Error handling and status codes

### Database Documentation
- Current schema documentation
- Migration procedures
- Performance optimization guidelines
- Backup and recovery procedures

### Development Tools
- Unit testing frameworks and conventions
- Integration testing setup
- Code coverage tools
- Performance profiling tools