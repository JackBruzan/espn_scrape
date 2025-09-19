# ESPN API Service & Model Library Plan

## Overview

Based on technical validation, ESPN provides rich JSON data through direct API endpoints without requiring browser automation. This plan outlines building a comprehensive service library for ESPN NFL data access.

## ESPN API Discovery

### Primary Endpoints Identified

#### 1. Scoreboard Endpoint
```
https://www.espn.com/nfl/scoreboard/_/week/{week}/year/{year}/seasontype/{seasonType}
```
- **Returns**: Complete season navigation, week calendar, game references
- **Format**: Massive JSON response with embedded API references
- **Parameters**:
  - `week`: 1-18 (regular season), 1-5 (postseason)
  - `year`: Season year (e.g., 2024)
  - `seasonType`: 1 (preseason), 2 (regular), 3 (postseason)

#### 2. Events/Games API
```
http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/seasons/{year}/types/{seasonType}/weeks/{week}/events
```
- **Returns**: Detailed game/event data for specific week
- **Format**: JSON with game details, teams, scores, timestamps
- **Access**: Referenced from scoreboard $ref patterns

#### 3. Individual Game Details
```
http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/events/{gameId}
```
- **Returns**: Complete game data including box scores, player stats
- **Format**: Nested JSON with comprehensive game information

## Service Architecture Plan

### 1. Core Models Library

#### ESPN Response Models
```csharp
// Core API response containers
public class EspnApiResponse<T>
{
    public T Data { get; set; }
    public string Status { get; set; }
    public DateTime Timestamp { get; set; }
}

// Season & Calendar Models
public class Season
{
    public int Year { get; set; }
    public string DisplayName { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public List<Week> Weeks { get; set; }
}

public class Week
{
    public int WeekNumber { get; set; }
    public int SeasonType { get; set; }
    public string Text { get; set; }
    public string Label { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string Url { get; set; }
    public bool IsActive { get; set; }
    public EventsReference Events { get; set; }
}

public class EventsReference
{
    [JsonProperty("$ref")]
    public string ApiReference { get; set; }
}
```

#### Game & Event Models
```csharp
public class GameEvent
{
    public string Id { get; set; }
    public DateTime Date { get; set; }
    public string Name { get; set; }
    public string ShortName { get; set; }
    public GameStatus Status { get; set; }
    public List<Team> Competitors { get; set; }
    public Venue Venue { get; set; }
    public string BoxScoreUrl { get; set; }
}

public class Team
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string DisplayName { get; set; }
    public string Abbreviation { get; set; }
    public string Color { get; set; }
    public string Logo { get; set; }
    public int Score { get; set; }
    public bool IsHome { get; set; }
}

public class GameStatus
{
    public int Type { get; set; }
    public string State { get; set; }
    public bool Completed { get; set; }
    public string Description { get; set; }
    public string Detail { get; set; }
}
```

#### Player Statistics Models
```csharp
public class PlayerStats
{
    public string PlayerId { get; set; }
    public string PlayerName { get; set; }
    public string Team { get; set; }
    public string Position { get; set; }
    public PassingStats Passing { get; set; }
    public RushingStats Rushing { get; set; }
    public ReceivingStats Receiving { get; set; }
    public DefensiveStats Defense { get; set; }
}

public class PassingStats
{
    public int Completions { get; set; }
    public int Attempts { get; set; }
    public int Yards { get; set; }
    public int Touchdowns { get; set; }
    public int Interceptions { get; set; }
    public int Sacks { get; set; }
    public double QBR { get; set; }
    public double Rating { get; set; }
}

public class RushingStats
{
    public int Carries { get; set; }
    public int Yards { get; set; }
    public double Average { get; set; }
    public int Touchdowns { get; set; }
    public int Long { get; set; }
}

public class ReceivingStats
{
    public int Receptions { get; set; }
    public int Targets { get; set; }
    public int Yards { get; set; }
    public double Average { get; set; }
    public int Touchdowns { get; set; }
    public int Long { get; set; }
}
```

### 2. ESPN API Service Layer

#### Core Service Interface
```csharp
public interface IEspnApiService
{
    // Season & Week Navigation
    Task<Season> GetSeasonAsync(int year, int seasonType = 2);
    Task<List<Week>> GetWeeksAsync(int year, int seasonType = 2);
    Task<Week> GetCurrentWeekAsync();
    
    // Game Data
    Task<List<GameEvent>> GetGamesAsync(int year, int week, int seasonType = 2);
    Task<GameEvent> GetGameAsync(string gameId);
    Task<List<GameEvent>> GetGamesForDateAsync(DateTime date);
    
    // Player Statistics
    Task<List<PlayerStats>> GetGamePlayerStatsAsync(string gameId);
    Task<List<PlayerStats>> GetWeekPlayerStatsAsync(int year, int week, int seasonType = 2);
    Task<PlayerStats> GetPlayerStatsAsync(string playerId, string gameId);
    
    // Bulk Operations
    Task<List<PlayerStats>> GetSeasonPlayerStatsAsync(int year, string playerId);
    Task<Dictionary<string, List<PlayerStats>>> GetAllPlayersWeekStatsAsync(int year, int week);
}
```

#### HTTP Client Service
```csharp
public interface IEspnHttpService
{
    Task<T> GetAsync<T>(string endpoint);
    Task<string> GetRawJsonAsync(string endpoint);
    Task<T> GetFromReferenceAsync<T>(string referenceUrl);
}

public class EspnHttpService : IEspnHttpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EspnHttpService> _logger;
    
    private const string BaseUrl = "https://www.espn.com";
    private const string ApiBaseUrl = "http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl";
    
    public EspnHttpService(HttpClient httpClient, ILogger<EspnHttpService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        
        // Configure headers
        _httpClient.DefaultRequestHeaders.Add("User-Agent", 
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }
    
    public async Task<T> GetAsync<T>(string endpoint)
    {
        var json = await GetRawJsonAsync(endpoint);
        return JsonConvert.DeserializeObject<T>(json);
    }
    
    public async Task<string> GetRawJsonAsync(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch data from ESPN endpoint: {Endpoint}", endpoint);
            throw;
        }
    }
}
```

### 3. Implementation Services

#### Scoreboard Service
```csharp
public class EspnScoreboardService : IEspnScoreboardService
{
    private readonly IEspnHttpService _httpService;
    
    public async Task<List<GameEvent>> GetGamesAsync(int year, int week, int seasonType = 2)
    {
        // Step 1: Get scoreboard data
        var scoreboardUrl = $"/nfl/scoreboard/_/week/{week}/year/{year}/seasontype/{seasonType}";
        var scoreboardData = await _httpService.GetAsync<ScoreboardResponse>(scoreboardUrl);
        
        // Step 2: Extract events reference
        var currentWeek = scoreboardData.Season.Weeks
            .FirstOrDefault(w => w.WeekNumber == week && w.SeasonType == seasonType);
            
        if (currentWeek?.Events?.ApiReference == null)
            return new List<GameEvent>();
            
        // Step 3: Get events from API reference
        var events = await _httpService.GetFromReferenceAsync<EventsResponse>(
            currentWeek.Events.ApiReference);
            
        return events.Items.Select(MapToGameEvent).ToList();
    }
}
```

#### Player Statistics Service
```csharp
public class EspnPlayerStatsService : IEspnPlayerStatsService
{
    public async Task<List<PlayerStats>> GetGamePlayerStatsAsync(string gameId)
    {
        var gameUrl = $"{ApiBaseUrl}/events/{gameId}";
        var gameData = await _httpService.GetAsync<GameResponse>(gameUrl);
        
        var playerStats = new List<PlayerStats>();
        
        // Extract player statistics from box score data
        foreach (var team in gameData.BoxScore.Teams)
        {
            foreach (var player in team.Statistics.Players)
            {
                playerStats.Add(MapPlayerStats(player, team.Team.Abbreviation));
            }
        }
        
        return playerStats;
    }
    
    public async Task<List<PlayerStats>> GetWeekPlayerStatsAsync(int year, int week, int seasonType = 2)
    {
        var games = await _scoreboardService.GetGamesAsync(year, week, seasonType);
        var allPlayerStats = new List<PlayerStats>();
        
        foreach (var game in games.Where(g => g.Status.Completed))
        {
            var gameStats = await GetGamePlayerStatsAsync(game.Id);
            allPlayerStats.AddRange(gameStats);
        }
        
        return allPlayerStats;
    }
}
```

### 4. Caching & Performance Strategy

#### Response Caching Service
```csharp
public interface IEspnCacheService
{
    Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiry = null);
    Task InvalidateAsync(string pattern);
}

public class EspnCacheService : IEspnCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    
    // Cache strategies:
    // - Season data: 24 hours
    // - Completed games: 1 hour  
    // - Live games: 30 seconds
    // - Player stats: 2 hours
}
```

### 5. Error Handling & Resilience

#### Retry Policies
```csharp
public class EspnApiServiceWithResilience : IEspnApiService
{
    private readonly IEspnApiService _innerService;
    private readonly IAsyncPolicy _retryPolicy;
    
    public EspnApiServiceWithResilience(IEspnApiService innerService)
    {
        _innerService = innerService;
        
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempts
                });
    }
}
```

## Integration with Existing Project

### 1. NuGet Package Dependencies
```xml
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="Microsoft.Extensions.Http" Version="8.0.0" />
<PackageReference Include="Microsoft.Extensions.Caching.Memory" Version="8.0.0" />
<PackageReference Include="Polly" Version="8.0.0" />
<PackageReference Include="Polly.Extensions.Http" Version="3.0.0" />
```

### 2. Dependency Injection Setup
```csharp
// In Program.cs or Startup.cs
services.AddHttpClient<IEspnHttpService, EspnHttpService>();
services.AddScoped<IEspnApiService, EspnApiService>();
services.AddScoped<IEspnScoreboardService, EspnScoreboardService>();
services.AddScoped<IEspnPlayerStatsService, EspnPlayerStatsService>();
services.AddMemoryCache();
services.AddScoped<IEspnCacheService, EspnCacheService>();
```

### 3. Updated Job Implementation
```csharp
public class EspnApiScrapingJob : IJob
{
    private readonly IEspnApiService _espnApiService;
    private readonly ILogger<EspnApiScrapingJob> _logger;
    
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var currentWeek = await _espnApiService.GetCurrentWeekAsync();
            var playerStats = await _espnApiService.GetWeekPlayerStatsAsync(
                currentWeek.Year, currentWeek.WeekNumber);
                
            // Process and save player statistics
            await SavePlayerStatsAsync(playerStats);
            
            _logger.LogInformation("Successfully scraped {PlayerCount} player stats for week {Week}", 
                playerStats.Count, currentWeek.WeekNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape ESPN data");
            throw;
        }
    }
}
```

## Implementation Timeline

### Phase 1: Core Infrastructure (Week 1)
- [ ] Create model classes for ESPN responses
- [ ] Implement `IEspnHttpService` with basic HTTP handling
- [ ] Add JSON parsing and error handling
- [ ] Create unit tests for HTTP service

### Phase 2: API Service Implementation (Week 2)
- [ ] Implement `IEspnApiService` with season/week navigation
- [ ] Add scoreboard and games retrieval methods
- [ ] Implement caching layer
- [ ] Add integration tests with live ESPN data

### Phase 3: Player Statistics Service (Week 3)
- [ ] Implement player statistics extraction
- [ ] Add box score parsing functionality
- [ ] Create player data mapping logic
- [ ] Add comprehensive validation and error handling

### Phase 4: Integration & Enhancement (Week 4)
- [ ] Update existing job to use new API service
- [ ] Add retry policies and resilience patterns
- [ ] Implement bulk operations for performance
- [ ] Add monitoring and logging enhancements

### Phase 5: Production Readiness (Week 5)
- [ ] Performance optimization and load testing
- [ ] Add comprehensive documentation
- [ ] Implement health checks and monitoring
- [ ] Deploy and validate in production environment

## Benefits of This Approach

### Performance Advantages
- **10x Faster**: Direct API calls vs browser automation
- **Lower Resource Usage**: No browser overhead
- **Better Reliability**: Fewer moving parts and dependencies

### Maintainability Benefits
- **Strongly Typed Models**: Full IntelliSense and compile-time checking
- **Testable Architecture**: Easy unit testing without browser dependencies
- **Cleaner Code**: Service-oriented architecture with clear separation of concerns

### Scalability Features
- **Caching Strategy**: Reduces API calls and improves response times
- **Async/Await**: Non-blocking operations for better throughput
- **Retry Policies**: Handles transient failures gracefully

This ESPN API service library will provide a robust, performant, and maintainable foundation for accessing NFL data directly from ESPN's APIs.
