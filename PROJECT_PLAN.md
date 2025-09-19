# ESPN NFL Box Score Scraping Project Plan

## Project Overview

This project extends the existing ESPN scraping service to collect NFL player statistics from box scores. The goal is to scrape player performance data from ESPN's NFL scoreboard pages and box scores for each game.

## Current Project Analysis

### Existing Infrastructure
- ✅ **Quartz.NET Job Scheduler**: Already configured with background job execution
- ✅ **Serilog Logging**: Comprehensive logging framework in place
- ✅ **HTTP Client**: Basic HTTP client setup for web scraping
- ✅ **Dependency Injection**: Service container configured
- ✅ **Player Models**: Basic player data models exist
- ✅ **Image Download Service**: File I/O operations established

### Current Limitations
- 🔄 **Single Purpose**: Currently only scrapes player images
- 🔄 **Static Data**: No real-time game data collection
- 🔄 **Limited Models**: No game or statistics data models

## Target Functionality

### URL Structure Analysis
**Target URL Pattern**: `https://www.espn.com/nfl/scoreboard/_/week/{week}/year/{year}/seasontype/2`

**Parameters**:
- `week`: NFL week number (1-18 for regular season)
- `year`: Season year (e.g., 2025)
- `seasontype`: Always `2` (regular season)

### Data Collection Flow

#### Phase 1: Scoreboard Page Navigation
![Main scoreboard page with box score link circled](attachment_1)

1. **Navigate to scoreboard page** using the URL pattern
2. **Identify active games** on the specified week/year
3. **Locate "Box Score" links** for completed games
4. **Extract game identifiers** for subsequent box score requests

#### Phase 2: Box Score Data Extraction
![Box score page showing player statistics tables](attachment_2)

1. **Navigate to each game's box score page**
2. **Parse player statistics tables** for:
   - **Passing stats**: Completions/Attempts, Yards, Average, TDs, Interceptions, Sacks, QBR, Rating
   - **Rushing stats**: Carries, Yards, Average, TDs, Long
   - **Receiving stats**: Receptions, Yards, Average, TDs, Long, Targets
3. **Extract ESPN Player IDs** from player name links
4. **Associate stats with player and game context**

## Implementation Plan

### 1. Data Models Enhancement

#### New Models Required:
```csharp
// Game and scoreboard models
public class Game { }
public class Scoreboard { }

// Player statistics models  
public class PlayerStats { }
public class PassingStats { }
public class RushingStats { }
public class ReceivingStats { }

// Combined result model
public class PlayerGameStats { }
```

### 2. Service Layer Updates

#### New Service Interface:
```csharp
public interface IBoxScoreScrapingService
{
    Task<List<PlayerGameStats>> ScrapeWeekStatsAsync(int week, int year);
    Task<List<Game>> GetGamesFromScoreboardAsync(int week, int year);
    Task<List<PlayerGameStats>> ScrapeGameBoxScoreAsync(string gameId);
}
```

### 3. New Scraping Job

#### ESPNBoxScoreJob:
- Replace or supplement existing `ESPNImageScrapingJob`
- Configure for weekly execution during NFL season
- Handle multiple weeks and error recovery
- Implement data persistence (CSV, JSON, or database)

### 4. Data Parsing Strategy

#### Validated Approach:
- **✅ Primary**: JSON parsing using Newtonsoft.Json for ESPN's structured responses
- **✅ Secondary**: HtmlAgilityPack for any HTML content within JSON responses
- **❌ Not Required**: Selenium WebDriver - ESPN serves data without JavaScript rendering

**Implementation**: Direct HTTP requests + JSON parsing provides optimal performance and reliability.

### 5. Data Output Format

#### Target Output Structure:
```json
{
  "playerId": "12345",
  "playerName": "Patrick Mahomes",
  "team": "KC",
  "gameId": "401547417",
  "gameDate": "2025-09-05",
  "week": 1,
  "year": 2025,
  "opponent": "LAC",
  "passingStats": {
    "completions": 24,
    "attempts": 39,
    "yards": 258,
    "average": 6.6,
    "touchdowns": 1,
    "interceptions": 0,
    "sacks": 2,
    "qbr": 82.9,
    "rating": 89.5
  },
  "rushingStats": {
    "carries": 6,
    "yards": 57,
    "average": 9.5,
    "touchdowns": 1,
    "long": 15
  },
  "receivingStats": null
}
```

## Technical Validation Status

**✅ VALIDATED** - Data access approach confirmed

### Key Findings

#### ESPN Data Accessibility
- **Direct HTTP Access**: ESPN serves rich structured JSON data without requiring browser automation
- **No JavaScript Rendering Required**: All data is available via direct HTTP requests to scoreboard URLs
- **Structured API References**: ESPN uses $ref patterns pointing to internal API endpoints for detailed data

#### Data Structure Analysis
1. **Scoreboard Endpoint**: `https://www.espn.com/nfl/scoreboard/_/week/{week}/year/{year}/seasontype/2`
   - Returns massive JSON response with complete season navigation
   - Contains embedded API references for detailed game data
   - Includes calendar data, weeks, and event references

2. **Events API Pattern**: `http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/seasons/{year}/types/2/weeks/{week}/events`
   - Direct API endpoints for game/event data
   - JSON structure contains game details, teams, scores, statistics

3. **Box Score Access**: Games will contain references to detailed box score data with player statistics

#### Recommended Technical Approach
- **✅ HtmlAgilityPack + JSON.NET**: Perfect for parsing ESPN's JSON responses
- **❌ Browser Automation**: Not required - adds unnecessary complexity and overhead
- **✅ Direct HTTP Client**: Fastest and most reliable approach for ESPN data access

## Technical Implementation Tasks

### Phase 1: Foundation (Week 1)
- [ ] **Create new data models** for games and player statistics
- [ ] **Add Newtonsoft.Json NuGet package** for JSON parsing (if not already included)
- [ ] **Implement IBoxScoreScrapingService interface**
- [ ] **Create unit tests** for URL generation and JSON parsing

### Phase 2: Scoreboard Scraping (Week 2)
- [ ] **Implement scoreboard JSON parser**
- [ ] **Extract game events from ESPN API references**
- [ ] **Parse game metadata and box score links**
- [ ] **Add error handling** for API response variations

### Phase 3: Box Score Data Extraction (Week 3)
- [ ] **Access box score JSON endpoints**
- [ ] **Parse player statistics from structured data**
- [ ] **Map JSON objects to data models**
- [ ] **Extract ESPN player IDs from response data**

### Phase 4: Integration & Testing (Week 4)
- [ ] **Create new Quartz job** for box score scraping
- [ ] **Update Program.cs** to register new services
- [ ] **Implement data persistence** (file-based initially)
- [ ] **Add comprehensive logging** and error handling
- [ ] **Test with multiple weeks/games**

### Phase 5: Production & Monitoring (Week 5)
- [ ] **Configure production schedule** (weekly during season)
- [ ] **Add monitoring and alerting**
- [ ] **Implement data validation** and quality checks
- [ ] **Create documentation** and deployment scripts

## Risk Assessment & Mitigation

### Technical Risks:
1. **HTML Structure Changes**: ESPN may modify their HTML structure
   - *Mitigation*: Implement flexible selectors and regular testing
   
2. **Rate Limiting**: ESPN may implement anti-scraping measures
   - *Mitigation*: Add delays between requests, rotate user agents
   
3. **JavaScript Dependencies**: Content may be loaded dynamically
   - *Mitigation*: Have Selenium fallback ready

### Data Risks:
1. **Incomplete Game Data**: Games may not have box scores immediately
   - *Mitigation*: Implement retry logic with exponential backoff
   
2. **Player ID Changes**: ESPN may change player ID format
   - *Mitigation*: Implement ID validation and mapping

## Success Metrics

### Functional Requirements:
- ✅ Successfully parse 95%+ of available box scores
- ✅ Accurately extract all three stat categories (passing, rushing, receiving)
- ✅ Correctly map ESPN player IDs
- ✅ Handle all 18 weeks of regular season

### Performance Requirements:
- ✅ Process one week's games in < 10 minutes
- ✅ Memory usage < 500MB during execution
- ✅ Graceful handling of network timeouts

### Data Quality Requirements:
- ✅ Zero data corruption or mixing between players
- ✅ Complete stat coverage for all available categories
- ✅ Accurate game context (week, year, teams)

## Deployment Strategy

### Development Environment:
1. **Local testing** with sample URLs
2. **Unit tests** for all parsing logic
3. **Integration tests** with real ESPN pages

### Production Environment:
1. **Scheduled execution** during NFL season
2. **File-based output** initially (CSV/JSON)
3. **Log monitoring** for errors and performance
4. **Manual verification** of first few executions

## Future Enhancements

### Potential Extensions:
- **Historical data backfill** for previous seasons
- **Database storage** instead of file-based output
- **Real-time notifications** for exceptional performances
- **Additional stat categories** (defensive stats, special teams)
- **Fantasy football integration** with scoring calculations
- **API endpoint** to serve collected data

---

*This project plan provides a comprehensive roadmap for implementing ESPN NFL box score scraping functionality while leveraging the existing infrastructure and maintaining code quality standards.*
