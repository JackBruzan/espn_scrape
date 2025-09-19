# ESPN API Service Development Tickets

## Sprint Overview
This epic contains 15 tickets across 5 phases to build a comprehensive ESPN API service library for NFL data collection. Each ticket includes acceptance criteria, unit testing requirements, and dependency mapping.

---

## 🎯 Phase 1: Core Infrastructure (Week 1)

### TICKET-001: Create ESPN API Core Models
**Priority**: High | **Story Points**: 5 | **Assignee**: Backend Developer/Coding Agent

#### Description
Create the foundational data models for ESPN API responses in the `Models/Espn/` directory. These models will serve as the foundation for all ESPN API data handling and must match ESPN's JSON response structure exactly.

#### Files to Create
- `Models/Espn/Season.cs`
- `Models/Espn/Week.cs` 
- `Models/Espn/GameEvent.cs`
- `Models/Espn/Team.cs`
- `Models/Espn/GameStatus.cs`
- `Models/Espn/EventsReference.cs`
- `Models/Espn/Venue.cs`

#### Exact Implementation Requirements

**File: `Models/Espn/Season.cs`**
```csharp
using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class Season : IEquatable<Season>
    {
        [JsonPropertyName("year")]
        public int Year { get; set; }

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("weeks")]
        public List<Week> Weeks { get; set; } = new();

        [JsonPropertyName("seasonType")]
        public int SeasonType { get; set; }

        public bool Equals(Season? other) => other != null && Year == other.Year && SeasonType == other.SeasonType;
        public override bool Equals(object? obj) => Equals(obj as Season);
        public override int GetHashCode() => HashCode.Combine(Year, SeasonType);
    }
}
```

**File: `Models/Espn/Week.cs`**
```csharp
using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class Week : IEquatable<Week>
    {
        [JsonPropertyName("weekNumber")]
        public int WeekNumber { get; set; }

        [JsonPropertyName("seasonType")]
        public int SeasonType { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("startDate")]
        public DateTime StartDate { get; set; }

        [JsonPropertyName("endDate")]
        public DateTime EndDate { get; set; }

        [JsonPropertyName("url")]
        public string Url { get; set; } = string.Empty;

        [JsonPropertyName("isActive")]
        public bool IsActive { get; set; }

        [JsonPropertyName("events")]
        public EventsReference? Events { get; set; }

        [JsonPropertyName("year")]
        public int Year { get; set; }

        public bool Equals(Week? other) => other != null && WeekNumber == other.WeekNumber && SeasonType == other.SeasonType && Year == other.Year;
        public override bool Equals(object? obj) => Equals(obj as Week);
        public override int GetHashCode() => HashCode.Combine(WeekNumber, SeasonType, Year);
    }
}
```

**File: `Models/Espn/EventsReference.cs`**
```csharp
using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class EventsReference
    {
        [JsonPropertyName("$ref")]
        public string ApiReference { get; set; } = string.Empty;
    }
}
```

**Complete remaining models following the same pattern**: GameEvent, Team, GameStatus, Venue with exact JSON property mappings.

#### Unit Testing Requirements

**File: `Tests/Models/Espn/SeasonTests.cs`**
```csharp
using ESPNScrape.Models.Espn;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Models.Espn
{
    public class SeasonTests
    {
        [Fact]
        public void Season_Serialization_RoundTrip_Success()
        {
            // Arrange
            var season = new Season
            {
                Year = 2025,
                DisplayName = "2025",
                StartDate = new DateTime(2025, 8, 1),
                EndDate = new DateTime(2026, 2, 15),
                SeasonType = 2,
                Weeks = new List<Week>
                {
                    new Week { WeekNumber = 1, SeasonType = 2, Year = 2025, Text = "Week 1" }
                }
            };

            // Act
            var json = JsonSerializer.Serialize(season);
            var deserialized = JsonSerializer.Deserialize<Season>(json);

            // Assert
            Assert.NotNull(deserialized);
            Assert.Equal(season.Year, deserialized.Year);
            Assert.Equal(season.DisplayName, deserialized.DisplayName);
            Assert.Equal(season.Weeks.Count, deserialized.Weeks.Count);
        }

        [Fact]
        public void Season_Equality_WorksCorrectly()
        {
            // Arrange
            var season1 = new Season { Year = 2025, SeasonType = 2 };
            var season2 = new Season { Year = 2025, SeasonType = 2 };
            var season3 = new Season { Year = 2024, SeasonType = 2 };

            // Act & Assert
            Assert.Equal(season1, season2);
            Assert.NotEqual(season1, season3);
        }
    }
}
```

#### Dependencies
- **Blocks**: TICKET-002, TICKET-003, TICKET-004
- **Blocked By**: None

---

### TICKET-002: Implement ESPN HTTP Service
**Priority**: High | **Story Points**: 8 | **Assignee**: Backend Developer/Coding Agent

#### Description
Build the core HTTP service for communicating with ESPN APIs. This service handles HTTP requests, response parsing, error handling, and provides a clean abstraction for ESPN API access.

#### Files to Create
- `Services/Interfaces/IEspnHttpService.cs`
- `Services/EspnHttpService.cs`
- `Tests/Services/EspnHttpServiceTests.cs`

#### Exact Implementation Requirements

**File: `Services/Interfaces/IEspnHttpService.cs`**
```csharp
namespace ESPNScrape.Services.Interfaces
{
    public interface IEspnHttpService
    {
        Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default);
        Task<string> GetRawJsonAsync(string endpoint, CancellationToken cancellationToken = default);
        Task<T> GetFromReferenceAsync<T>(string referenceUrl, CancellationToken cancellationToken = default);
    }
}
```

**File: `Services/EspnHttpService.cs`**
```csharp
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;

namespace ESPNScrape.Services
{
    public class EspnHttpService : IEspnHttpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EspnHttpService> _logger;
        private readonly IAsyncPolicy _retryPolicy;
        
        private const string BaseUrl = "https://www.espn.com";
        private const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36";

        public EspnHttpService(HttpClient httpClient, ILogger<EspnHttpService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
            
            // Configure headers
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // Configure retry policy
            _retryPolicy = Policy
                .Handle<HttpRequestException>()
                .Or<TaskCanceledException>()
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning("ESPN HTTP retry attempt {RetryCount} after {Delay}ms for {Endpoint}", 
                            retryCount, timespan.TotalMilliseconds, context.GetValueOrDefault("endpoint", "unknown"));
                    });
        }

        public async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
        {
            var json = await GetRawJsonAsync(endpoint, cancellationToken);
            return JsonSerializer.Deserialize<T>(json) ?? throw new InvalidOperationException($"Failed to deserialize response from {endpoint}");
        }

        public async Task<string> GetRawJsonAsync(string endpoint, CancellationToken cancellationToken = default)
        {
            var fullUrl = endpoint.StartsWith("http") ? endpoint : $"{BaseUrl}{endpoint}";
            
            return await _retryPolicy.ExecuteAsync(async (context) =>
            {
                try
                {
                    _logger.LogDebug("Making HTTP request to {Url}", fullUrl);
                    
                    var response = await _httpClient.GetAsync(fullUrl, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    
                    _logger.LogDebug("ESPN HTTP request successful. Response length: {Length} characters", content.Length);
                    
                    return content;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "ESPN HTTP request failed for {Url}", fullUrl);
                    throw;
                }
            }, new Context { ["endpoint"] = fullUrl });
        }

        public async Task<T> GetFromReferenceAsync<T>(string referenceUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(referenceUrl))
                throw new ArgumentException("Reference URL cannot be null or empty", nameof(referenceUrl));
                
            // ESPN $ref URLs are direct API endpoints
            return await GetAsync<T>(referenceUrl, cancellationToken);
        }
    }
}
```

#### Integration with Program.cs
**Add to Program.cs in ConfigureServices section:**
```csharp
// Add HTTP client for ESPN service
services.AddHttpClient<IEspnHttpService, EspnHttpService>();

// Add Polly for resilience
services.AddHttpClient<EspnHttpService>()
    .AddPolicyHandler(GetRetryPolicy());

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
}
```

#### Unit Testing Requirements

**File: `Tests/Services/EspnHttpServiceTests.cs`**
```csharp
using ESPNScrape.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class EspnHttpServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHandler;
        private readonly HttpClient _httpClient;
        private readonly Mock<ILogger<EspnHttpService>> _mockLogger;
        private readonly EspnHttpService _service;

        public EspnHttpServiceTests()
        {
            _mockHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHandler.Object);
            _mockLogger = new Mock<ILogger<EspnHttpService>>();
            _service = new EspnHttpService(_httpClient, _mockLogger.Object);
        }

        [Fact]
        public async Task GetAsync_ValidUrl_ReturnsDeserializedObject()
        {
            // Arrange
            var testData = new { name = "test", value = 42 };
            var json = JsonSerializer.Serialize(testData);
            
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.IsAny<HttpRequestMessage>(), 
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _service.GetAsync<dynamic>("/test/endpoint");

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetAsync_HttpError_ThrowsWithLogging()
        {
            // Arrange
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.InternalServerError
                });

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(() => _service.GetAsync<object>("/test/error"));
            
            // Verify logging occurred
            _mockLogger.Verify(
                x => x.Log(
                    LogLevel.Error,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ESPN HTTP request failed")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        [Fact]
        public async Task GetFromReferenceAsync_ValidRef_ParsesAndReturns()
        {
            // Arrange
            var referenceUrl = "http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/seasons/2025/types/2/weeks/3/events";
            var testData = new { items = new[] { new { id = "12345", name = "Test Game" } } };
            var json = JsonSerializer.Serialize(testData);
            
            _mockHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                });

            // Act
            var result = await _service.GetFromReferenceAsync<dynamic>(referenceUrl);

            // Assert
            Assert.NotNull(result);
        }
    }
}
```

#### Dependencies
- **Blocks**: TICKET-005, TICKET-006, TICKET-007
- **Blocked By**: TICKET-001

---

### TICKET-003: Add NuGet Dependencies and Project Configuration
**Priority**: High | **Story Points**: 2 | **Assignee**: DevOps/Backend Developer

#### Description
Update the project file with required NuGet packages and configure dependency injection for the ESPN API services.

#### Acceptance Criteria
- [ ] Add `Microsoft.Extensions.Http` package for HttpClient factory
- [ ] Add `Microsoft.Extensions.Caching.Memory` for response caching
- [ ] Add `Polly` and `Polly.Extensions.Http` for resilience patterns
- [ ] Update `Program.cs` to register HTTP client with proper configuration
- [ ] Configure dependency injection for all ESPN service interfaces
- [ ] Add health check endpoint for ESPN API connectivity
- [ ] Configure logging levels for ESPN-specific operations

#### Unit Testing Requirements
```csharp
[Test]
public void ServiceRegistration_AllServicesRegistered_Successfully()
{
    // Test dependency injection container configuration
}

[Test]
public void HttpClient_Configuration_IsCorrect()
{
    // Verify HttpClient timeout, headers, and base address
}
```

#### Dependencies
- **Blocks**: TICKET-002, TICKET-004
- **Blocked By**: TICKET-001

---

### TICKET-004: Create Base ESPN API Service Interface
**Priority**: High | **Story Points**: 3 | **Assignee**: Backend Developer/Coding Agent

#### Description
Design and implement the main ESPN API service interface that will serve as the public contract for all ESPN data operations. This interface defines the comprehensive ESPN API operations needed for NFL data access.

#### Files to Create
- `Services/Interfaces/IEspnApiService.cs`
- `Tests/Services/IEspnApiServiceTests.cs`

#### Exact Implementation Requirements

**File: `Services/Interfaces/IEspnApiService.cs`**
```csharp
using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.Interfaces
{
    /// <summary>
    /// Primary interface for ESPN NFL data access operations
    /// </summary>
    public interface IEspnApiService
    {
        /// <summary>
        /// Retrieves season information for the specified year
        /// </summary>
        /// <param name="year">The season year (e.g., 2024)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Season data including weeks and configuration</returns>
        Task<Season> GetSeasonAsync(int year, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all weeks for a specific season
        /// </summary>
        /// <param name="year">The season year</param>
        /// <param name="seasonType">Season type: 1=Preseason, 2=Regular, 3=Postseason</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of week data</returns>
        Task<IEnumerable<Week>> GetWeeksAsync(int year, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current active week for the current season
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Current week information</returns>
        Task<Week> GetCurrentWeekAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves specific week data
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="weekNumber">Week number within the season</param>
        /// <param name="seasonType">Season type: 1=Preseason, 2=Regular, 3=Postseason</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Week data with events</returns>
        Task<Week> GetWeekAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all games/events for a specific week
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="weekNumber">Week number</param>
        /// <param name="seasonType">Season type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of events for the week</returns>
        Task<IEnumerable<Event>> GetGamesAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves a specific game by event ID
        /// </summary>
        /// <param name="eventId">ESPN event identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete event data</returns>
        Task<Event> GetGameAsync(string eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets all games for a specific date
        /// </summary>
        /// <param name="date">Target date for games</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of events on the specified date</returns>
        Task<IEnumerable<Event>> GetGamesForDateAsync(DateTime date, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves box score statistics for a specific game
        /// </summary>
        /// <param name="eventId">ESPN event identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete box score with team and player statistics</returns>
        Task<BoxScore> GetBoxScoreAsync(string eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets player statistics for a specific game
        /// </summary>
        /// <param name="eventId">ESPN event identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of player statistics for the game</returns>
        Task<IEnumerable<PlayerStats>> GetGamePlayerStatsAsync(string eventId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all player statistics for a specific week
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="weekNumber">Week number</param>
        /// <param name="seasonType">Season type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of all player statistics for the week</returns>
        Task<IEnumerable<PlayerStats>> GetWeekPlayerStatsAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets comprehensive player statistics for an entire season
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="seasonType">Season type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of aggregated season player statistics</returns>
        Task<IEnumerable<PlayerStats>> GetSeasonPlayerStatsAsync(int year, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Bulk operation to get all players' statistics for a specific week across all games
        /// </summary>
        /// <param name="year">Season year</param>
        /// <param name="weekNumber">Week number</param>
        /// <param name="seasonType">Season type</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete collection of player statistics for all games in the week</returns>
        Task<IEnumerable<PlayerStats>> GetAllPlayersWeekStatsAsync(int year, int weekNumber, int seasonType = 2, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves all NFL teams with current information
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Collection of NFL teams</returns>
        Task<IEnumerable<Team>> GetTeamsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets detailed information for a specific team
        /// </summary>
        /// <param name="teamId">ESPN team identifier</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Complete team information</returns>
        Task<Team> GetTeamAsync(string teamId, CancellationToken cancellationToken = default);
    }
}
```

#### Unit Testing Requirements

**File: `Tests/Services/IEspnApiServiceTests.cs`**
```csharp
using ESPNScrape.Services.Interfaces;
using System.Reflection;
using Xunit;

namespace ESPNScrape.Tests.Services
{
    public class IEspnApiServiceTests
    {
        [Fact]
        public void Interface_AllMethods_HaveProperSignatures()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);
            var methods = interfaceType.GetMethods();

            // Act & Assert
            Assert.True(methods.Length >= 14, "Interface should have at least 14 methods");

            // Verify key methods exist
            var seasonMethod = methods.FirstOrDefault(m => m.Name == "GetSeasonAsync");
            Assert.NotNull(seasonMethod);
            Assert.True(seasonMethod.ReturnType.IsGenericType);
            Assert.Equal(typeof(Task<>), seasonMethod.ReturnType.GetGenericTypeDefinition());

            var gamesMethod = methods.FirstOrDefault(m => m.Name == "GetGamesAsync");
            Assert.NotNull(gamesMethod);
            Assert.True(gamesMethod.GetParameters().Any(p => p.Name == "year"));
            Assert.True(gamesMethod.GetParameters().Any(p => p.Name == "weekNumber"));
        }

        [Fact]
        public void Interface_AllAsyncMethods_HaveCancellationTokenParameter()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);
            var asyncMethods = interfaceType.GetMethods()
                .Where(m => m.Name.EndsWith("Async"));

            // Act & Assert
            foreach (var method in asyncMethods)
            {
                var hasToken = method.GetParameters()
                    .Any(p => p.ParameterType == typeof(CancellationToken));
                
                Assert.True(hasToken, $"Method {method.Name} should have CancellationToken parameter");
            }
        }

        [Fact]
        public void Interface_BulkMethods_ExistAndReturnCollections()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);

            // Act
            var seasonStatsMethod = interfaceType.GetMethod("GetSeasonPlayerStatsAsync");
            var weekStatsMethod = interfaceType.GetMethod("GetAllPlayersWeekStatsAsync");

            // Assert
            Assert.NotNull(seasonStatsMethod);
            Assert.NotNull(weekStatsMethod);
            
            // Verify return types are collections
            Assert.True(seasonStatsMethod.ReturnType.IsGenericType);
            Assert.True(weekStatsMethod.ReturnType.IsGenericType);
        }

        [Fact]
        public void Interface_OverloadMethods_HaveDefaultParameters()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);
            
            // Act
            var weekMethod = interfaceType.GetMethod("GetWeekAsync");
            var gamesMethod = interfaceType.GetMethod("GetGamesAsync");

            // Assert
            Assert.NotNull(weekMethod);
            Assert.NotNull(gamesMethod);

            // Verify seasonType has default value
            var weekSeasonTypeParam = weekMethod.GetParameters()
                .FirstOrDefault(p => p.Name == "seasonType");
            Assert.NotNull(weekSeasonTypeParam);
            Assert.True(weekSeasonTypeParam.HasDefaultValue);
            Assert.Equal(2, weekSeasonTypeParam.DefaultValue);
        }

        [Fact]
        public void Interface_Documentation_RequiredMethodsExist()
        {
            // Arrange
            var interfaceType = typeof(IEspnApiService);
            var requiredMethods = new[]
            {
                "GetSeasonAsync",
                "GetWeeksAsync", 
                "GetCurrentWeekAsync",
                "GetWeekAsync",
                "GetGamesAsync",
                "GetGameAsync",
                "GetGamesForDateAsync",
                "GetBoxScoreAsync",
                "GetGamePlayerStatsAsync",
                "GetWeekPlayerStatsAsync",
                "GetSeasonPlayerStatsAsync",
                "GetAllPlayersWeekStatsAsync",
                "GetTeamsAsync",
                "GetTeamAsync"
            };

            // Act & Assert
            foreach (var methodName in requiredMethods)
            {
                var method = interfaceType.GetMethod(methodName);
                Assert.NotNull(method);
                Assert.True(method.Name.EndsWith("Async"), $"Method {methodName} should be async");
            }
        }
    }
}
```

#### Dependencies
- **Blocks**: TICKET-005, TICKET-006, TICKET-007
- **Blocked By**: TICKET-001

---

## 🏗️ Phase 2: API Service Implementation (Week 2)

### TICKET-005: Implement ESPN Scoreboard Service
**Priority**: High | **Story Points**: 8 | **Assignee**: Backend Developer/Coding Agent

#### Description
Build the scoreboard service responsible for fetching and parsing ESPN scoreboard data, extracting game information, and handling week/season navigation. This service implements the core ESPN API operations defined in the interface.

#### Files to Create
- `Services/EspnScoreboardService.cs`
- `Services/Interfaces/IEspnScoreboardService.cs`
- `Tests/Services/EspnScoreboardServiceTests.cs`

#### Exact Implementation Requirements

**File: `Services/Interfaces/IEspnScoreboardService.cs`**
```csharp
using ESPNScrape.Models.Espn;

namespace ESPNScrape.Services.Interfaces
{
    public interface IEspnScoreboardService
    {
        Task<ScoreboardData> GetScoreboardAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default);
        Task<IEnumerable<Event>> ExtractEventsAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
        Task<Season> ExtractSeasonInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
        Task<Week> ExtractWeekInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
        Task<IEnumerable<string>> GetEventReferencesAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default);
    }
}
```

**File: `Services/EspnScoreboardService.cs`**
```csharp
using ESPNScrape.Models.Espn;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ESPNScrape.Services
{
    public class EspnScoreboardService : IEspnScoreboardService
    {
        private readonly IEspnHttpService _httpService;
        private readonly ILogger<EspnScoreboardService> _logger;
        
        // ESPN URL templates
        private const string ScoreboardUrlTemplate = "https://www.espn.com/nfl/scoreboard/_/week/{week}/year/{year}/seasontype/{seasonType}";
        
        // Regex for extracting embedded JSON from ESPN HTML
        private static readonly Regex JsonExtractionRegex = new(@"window\['__espnfitt__'\]\s*=\s*({.*?});", 
            RegexOptions.Compiled | RegexOptions.Singleline);

        public EspnScoreboardService(IEspnHttpService httpService, ILogger<EspnScoreboardService> logger)
        {
            _httpService = httpService;
            _logger = logger;
        }

        public async Task<ScoreboardData> GetScoreboardAsync(int year, int week, int seasonType = 2, CancellationToken cancellationToken = default)
        {
            try
            {
                _logger.LogDebug("Fetching scoreboard for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}", 
                    year, week, seasonType);

                // Validate input parameters
                ValidateParameters(year, week, seasonType);

                var url = ScoreboardUrlTemplate
                    .Replace("{year}", year.ToString())
                    .Replace("{week}", week.ToString())
                    .Replace("{seasonType}", seasonType.ToString());

                var htmlResponse = await _httpService.GetRawJsonAsync(url, cancellationToken);
                
                // Extract embedded JSON from HTML
                var jsonData = ExtractEmbeddedJson(htmlResponse);
                
                // Parse scoreboard data
                var scoreboardData = ParseScoreboardData(jsonData);
                
                _logger.LogDebug("Successfully retrieved scoreboard data. Events found: {EventCount}", 
                    scoreboardData.Events?.Count() ?? 0);

                return scoreboardData;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to retrieve scoreboard for Year: {Year}, Week: {Week}, SeasonType: {SeasonType}", 
                    year, week, seasonType);
                throw;
            }
        }

        public async Task<IEnumerable<Event>> ExtractEventsAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default)
        {
            try
            {
                if (scoreboard?.Events == null)
                {
                    _logger.LogWarning("No events found in scoreboard data");
                    return Enumerable.Empty<Event>();
                }

                var events = new List<Event>();
                
                foreach (var eventData in scoreboard.Events)
                {
                    if (eventData != null)
                    {
                        events.Add(eventData);
                    }
                }

                _logger.LogDebug("Extracted {EventCount} events from scoreboard", events.Count);
                return events;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract events from scoreboard data");
                throw;
            }
        }

        public async Task<Season> ExtractSeasonInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default)
        {
            try
            {
                if (scoreboard?.Season == null)
                {
                    throw new InvalidOperationException("Season information not found in scoreboard data");
                }

                _logger.LogDebug("Extracted season info: Year {Year}, Type {Type}", 
                    scoreboard.Season.Year, scoreboard.Season.Type);

                return scoreboard.Season;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract season information from scoreboard");
                throw;
            }
        }

        public async Task<Week> ExtractWeekInfoAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default)
        {
            try
            {
                if (scoreboard?.Week == null)
                {
                    throw new InvalidOperationException("Week information not found in scoreboard data");
                }

                _logger.LogDebug("Extracted week info: Number {Number}, Text {Text}", 
                    scoreboard.Week.Number, scoreboard.Week.Text);

                return scoreboard.Week;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to extract week information from scoreboard");
                throw;
            }
        }

        public async Task<IEnumerable<string>> GetEventReferencesAsync(ScoreboardData scoreboard, CancellationToken cancellationToken = default)
        {
            try
            {
                var references = new List<string>();

                if (scoreboard?.Events != null)
                {
                    foreach (var eventData in scoreboard.Events)
                    {
                        if (!string.IsNullOrEmpty(eventData.Id))
                        {
                            // ESPN event reference URL pattern
                            var refUrl = $"http://sports.core.api.espn.pvt/v2/sports/football/leagues/nfl/events/{eventData.Id}";
                            references.Add(refUrl);
                        }
                    }
                }

                _logger.LogDebug("Generated {ReferenceCount} event reference URLs", references.Count);
                return references;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to generate event references from scoreboard");
                throw;
            }
        }

        private void ValidateParameters(int year, int week, int seasonType)
        {
            if (year < 2000 || year > DateTime.Now.Year + 1)
            {
                throw new ArgumentException($"Invalid year: {year}. Must be between 2000 and {DateTime.Now.Year + 1}", nameof(year));
            }

            if (seasonType < 1 || seasonType > 3)
            {
                throw new ArgumentException($"Invalid season type: {seasonType}. Must be 1 (Preseason), 2 (Regular), or 3 (Postseason)", nameof(seasonType));
            }

            // Validate week ranges by season type
            switch (seasonType)
            {
                case 1: // Preseason
                    if (week < 1 || week > 4)
                        throw new ArgumentException($"Invalid preseason week: {week}. Must be between 1 and 4", nameof(week));
                    break;
                case 2: // Regular season
                    if (week < 1 || week > 18)
                        throw new ArgumentException($"Invalid regular season week: {week}. Must be between 1 and 18", nameof(week));
                    break;
                case 3: // Postseason
                    if (week < 1 || week > 5)
                        throw new ArgumentException($"Invalid postseason week: {week}. Must be between 1 and 5", nameof(week));
                    break;
            }
        }

        private string ExtractEmbeddedJson(string htmlContent)
        {
            var match = JsonExtractionRegex.Match(htmlContent);
            if (!match.Success)
            {
                throw new InvalidOperationException("Could not extract embedded JSON from ESPN response");
            }

            return match.Groups[1].Value;
        }

        private ScoreboardData ParseScoreboardData(string jsonContent)
        {
            try
            {
                var jsonDoc = JsonDocument.Parse(jsonContent);
                var root = jsonDoc.RootElement;

                // Navigate to scoreboard data structure
                if (!root.TryGetProperty("page", out var pageElement) ||
                    !pageElement.TryGetProperty("content", out var contentElement) ||
                    !contentElement.TryGetProperty("sbData", out var sbDataElement))
                {
                    throw new InvalidOperationException("Invalid ESPN JSON structure");
                }

                var scoreboardData = new ScoreboardData
                {
                    Events = ParseEvents(sbDataElement),
                    Season = ParseSeason(sbDataElement),
                    Week = ParseWeek(sbDataElement)
                };

                return scoreboardData;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to parse ESPN JSON response");
                throw new InvalidOperationException("Invalid JSON format in ESPN response", ex);
            }
        }

        private IEnumerable<Event> ParseEvents(JsonElement sbDataElement)
        {
            var events = new List<Event>();

            if (sbDataElement.TryGetProperty("events", out var eventsElement) && eventsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var eventElement in eventsElement.EnumerateArray())
                {
                    try
                    {
                        var eventJson = JsonSerializer.Serialize(eventElement);
                        var eventObj = JsonSerializer.Deserialize<Event>(eventJson);
                        if (eventObj != null)
                        {
                            events.Add(eventObj);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse individual event from ESPN response");
                        // Continue processing other events
                    }
                }
            }

            return events;
        }

        private Season ParseSeason(JsonElement sbDataElement)
        {
            if (sbDataElement.TryGetProperty("leagues", out var leaguesElement) && 
                leaguesElement.ValueKind == JsonValueKind.Array)
            {
                var firstLeague = leaguesElement.EnumerateArray().FirstOrDefault();
                if (firstLeague.TryGetProperty("season", out var seasonElement))
                {
                    var seasonJson = JsonSerializer.Serialize(seasonElement);
                    return JsonSerializer.Deserialize<Season>(seasonJson) ?? new Season();
                }
            }

            return new Season();
        }

        private Week ParseWeek(JsonElement sbDataElement)
        {
            if (sbDataElement.TryGetProperty("week", out var weekElement))
            {
                var weekJson = JsonSerializer.Serialize(weekElement);
                return JsonSerializer.Deserialize<Week>(weekJson) ?? new Week();
            }

            return new Week();
        }
    }
}
```

#### Additional Model Required

**File: `Models/Espn/ScoreboardData.cs`**
```csharp
using System.Text.Json.Serialization;

namespace ESPNScrape.Models.Espn
{
    public class ScoreboardData
    {
        [JsonPropertyName("events")]
        public IEnumerable<Event> Events { get; set; } = new List<Event>();

        [JsonPropertyName("season")]
        public Season Season { get; set; } = new Season();

        [JsonPropertyName("week")]
        public Week Week { get; set; } = new Week();

        [JsonPropertyName("leagues")]
        public IEnumerable<League> Leagues { get; set; } = new List<League>();
    }

    public class League
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("abbreviation")]
        public string Abbreviation { get; set; } = string.Empty;

        [JsonPropertyName("season")]
        public Season Season { get; set; } = new Season();
    }
}
```

#### Integration with Program.cs
**Add to Program.cs in ConfigureServices section:**
```csharp
// Register ESPN Scoreboard Service
services.AddScoped<IEspnScoreboardService, EspnScoreboardService>();
```

#### Description
Build the scoreboard service responsible for fetching and parsing ESPN scoreboard data, extracting game information, and handling week/season navigation.

#### Acceptance Criteria
- [ ] Create `EspnScoreboardService` implementing scoreboard-specific functionality
- [ ] Parse ESPN's embedded JSON from scoreboard HTML responses
- [ ] Extract season and week navigation data
- [ ] Identify and parse `$ref` URLs for events data
- [ ] Handle different season types (preseason=1, regular=2, postseason=3)
- [ ] Support week range validation (1-18 regular season, 1-5 postseason)
- [ ] Implement efficient JSON parsing using System.Text.Json
- [ ] Add proper error handling for malformed ESPN responses

#### Unit Testing Requirements
```csharp
[Test]
public async Task GetGamesAsync_ValidWeek_ReturnsGameList()
{
    // Mock ESPN response, verify game parsing
}

[Test]
public async Task GetGamesAsync_InvalidWeek_ThrowsArgumentException()
{
    // Test parameter validation
}

[Test]
public async Task ParseEspnJson_MalformedResponse_HandlesGracefully()
{
    // Test error handling for bad ESPN data
}

[Test]
public async Task ExtractEventsReference_ValidResponse_ReturnsCorrectUrl()
{
    // Test $ref URL extraction from ESPN JSON
}
```

#### Dependencies
- **Blocks**: TICKET-008, TICKET-009
- **Blocked By**: TICKET-002, TICKET-004

---

### TICKET-006: Implement Response Caching Service
**Priority**: Medium | **Story Points**: 5 | **Assignee**: Backend Developer/Coding Agent

#### Description
Create a caching layer to improve performance and reduce ESPN API calls. Implement intelligent cache strategies based on data type and freshness requirements with configurable TTL policies.

#### Files to Create
- `Services/Interfaces/IEspnCacheService.cs`
- `Services/EspnCacheService.cs`
- `Configuration/CacheConfiguration.cs`
- `Tests/Services/EspnCacheServiceTests.cs`

#### Exact Implementation Requirements

**File: `Services/Interfaces/IEspnCacheService.cs`**
```csharp
namespace ESPNScrape.Services.Interfaces
{
    public interface IEspnCacheService
    {
        Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class;
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class;
        Task RemoveAsync(string key, CancellationToken cancellationToken = default);
        Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
        Task WarmCacheAsync(int currentYear, int currentWeek, CancellationToken cancellationToken = default);
        string GenerateKey(string operation, params object[] parameters);
        TimeSpan GetTtlForOperation(string operation);
    }
}
```

**File: `Configuration/CacheConfiguration.cs`**
```csharp
namespace ESPNScrape.Configuration
{
    public class CacheConfiguration
    {
        public int DefaultTtlMinutes { get; set; } = 30;
        public int SeasonDataTtlHours { get; set; } = 24;
        public int CompletedGameTtlMinutes { get; set; } = 60;
        public int LiveGameTtlSeconds { get; set; } = 30;
        public int PlayerStatsTtlMinutes { get; set; } = 15;
        public int TeamDataTtlHours { get; set; } = 12;
        public bool EnableCacheWarming { get; set; } = true;
        public int MaxCacheSize { get; set; } = 1000;
    }
}
```

**File: `Services/EspnCacheService.cs`**
```csharp
using ESPNScrape.Configuration;
using ESPNScrape.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ESPNScrape.Services
{
    public class EspnCacheService : IEspnCacheService
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ILogger<EspnCacheService> _logger;
        private readonly CacheConfiguration _config;
        private readonly ConcurrentDictionary<string, object> _keyTracking;
        
        public EspnCacheService(
            IMemoryCache memoryCache, 
            ILogger<EspnCacheService> logger, 
            IOptions<CacheConfiguration> config)
        {
            _memoryCache = memoryCache;
            _logger = logger;
            _config = config.Value;
            _keyTracking = new ConcurrentDictionary<string, object>();
        }

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                if (_memoryCache.TryGetValue(key, out var cachedValue))
                {
                    _logger.LogDebug("Cache hit for key: {Key}", key);
                    
                    if (cachedValue is string json)
                    {
                        return JsonSerializer.Deserialize<T>(json);
                    }
                    
                    return cachedValue as T;
                }

                _logger.LogDebug("Cache miss for key: {Key}", key);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving from cache for key: {Key}", key);
                return null;
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                // Try to get from cache first
                var cached = await GetAsync<T>(key, cancellationToken);
                if (cached != null)
                {
                    return cached;
                }

                _logger.LogDebug("Cache miss, executing factory for key: {Key}", key);
                
                // Execute factory to get fresh data
                var result = await factory();
                
                // Cache the result
                await SetAsync(key, result, expiry, cancellationToken);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOrSetAsync for key: {Key}", key);
                throw;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null, CancellationToken cancellationToken = default) where T : class
        {
            try
            {
                var effectiveExpiry = expiry ?? TimeSpan.FromMinutes(_config.DefaultTtlMinutes);
                
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = effectiveExpiry,
                    Size = EstimateSize(value),
                    Priority = CacheItemPriority.Normal
                };

                // Serialize complex objects to JSON for consistent storage
                var cacheValue = value is string ? value : JsonSerializer.Serialize(value);
                
                _memoryCache.Set(key, cacheValue, options);
                _keyTracking.TryAdd(key, DateTime.UtcNow);
                
                _logger.LogDebug("Cached value for key: {Key}, TTL: {TTL}", key, effectiveExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache for key: {Key}", key);
                throw;
            }
        }

        public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            try
            {
                _memoryCache.Remove(key);
                _keyTracking.TryRemove(key, out _);
                
                _logger.LogDebug("Removed cache entry for key: {Key}", key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entry for key: {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern, CancellationToken cancellationToken = default)
        {
            try
            {
                var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var keysToRemove = _keyTracking.Keys.Where(k => regex.IsMatch(k)).ToList();
                
                foreach (var key in keysToRemove)
                {
                    await RemoveAsync(key, cancellationToken);
                }
                
                _logger.LogDebug("Removed {Count} cache entries matching pattern: {Pattern}", keysToRemove.Count, pattern);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache entries by pattern: {Pattern}", pattern);
            }
        }

        public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
        {
            return _memoryCache.TryGetValue(key, out _);
        }

        public async Task WarmCacheAsync(int currentYear, int currentWeek, CancellationToken cancellationToken = default)
        {
            if (!_config.EnableCacheWarming)
            {
                _logger.LogDebug("Cache warming is disabled");
                return;
            }

            try
            {
                _logger.LogInformation("Starting cache warming for Year: {Year}, Week: {Week}", currentYear, currentWeek);
                
                // Pre-generate common cache keys that will likely be requested
                var commonKeys = new[]
                {
                    GenerateKey("season", currentYear),
                    GenerateKey("week", currentYear, currentWeek),
                    GenerateKey("scoreboard", currentYear, currentWeek, 2),
                    GenerateKey("teams"),
                    GenerateKey("currentWeek")
                };

                foreach (var key in commonKeys)
                {
                    _keyTracking.TryAdd(key, DateTime.UtcNow);
                }

                _logger.LogInformation("Cache warming completed. Pre-registered {Count} keys", commonKeys.Length);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during cache warming");
            }
        }

        public string GenerateKey(string operation, params object[] parameters)
        {
            try
            {
                var keyParts = new[] { "espn", operation }.Concat(parameters.Select(p => p?.ToString() ?? "null"));
                return string.Join(":", keyParts);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating cache key for operation: {Operation}", operation);
                return $"espn:{operation}:{Guid.NewGuid()}";
            }
        }

        public TimeSpan GetTtlForOperation(string operation)
        {
            return operation.ToLowerInvariant() switch
            {
                var op when op.Contains("season") => TimeSpan.FromHours(_config.SeasonDataTtlHours),
                var op when op.Contains("completed") || op.Contains("final") => TimeSpan.FromMinutes(_config.CompletedGameTtlMinutes),
                var op when op.Contains("live") || op.Contains("inprogress") => TimeSpan.FromSeconds(_config.LiveGameTtlSeconds),
                var op when op.Contains("player") || op.Contains("stats") => TimeSpan.FromMinutes(_config.PlayerStatsTtlMinutes),
                var op when op.Contains("team") => TimeSpan.FromHours(_config.TeamDataTtlHours),
                _ => TimeSpan.FromMinutes(_config.DefaultTtlMinutes)
            };
        }

        private long EstimateSize<T>(T value)
        {
            try
            {
                if (value is string str)
                {
                    return str.Length * 2; // Approximate size in bytes
                }
                
                var json = JsonSerializer.Serialize(value);
                return json.Length * 2;
            }
            catch
            {
                return 1024; // Default size estimate
            }
        }
    }
}
```

#### Integration with Program.cs
**Add to Program.cs in ConfigureServices section:**
```csharp
// Configure cache settings
services.Configure<CacheConfiguration>(configuration.GetSection("Cache"));

// Add memory cache
services.AddMemoryCache(options =>
{
    options.SizeLimit = 1000; // Max number of entries
});

// Register cache service
services.AddSingleton<IEspnCacheService, EspnCacheService>();
```

#### Configuration in appsettings.json
```json
{
  "Cache": {
    "DefaultTtlMinutes": 30,
    "SeasonDataTtlHours": 24,
    "CompletedGameTtlMinutes": 60,
    "LiveGameTtlSeconds": 30,
    "PlayerStatsTtlMinutes": 15,
    "TeamDataTtlHours": 12,
    "EnableCacheWarming": true,
    "MaxCacheSize": 1000
  }
}
```
    // Test pattern-based cache invalidation
}

[Test]
public void CacheKey_Generation_IsConsistent()
{
    // Test cache key generation for same inputs
}
```

#### Dependencies
- **Blocks**: TICKET-007, TICKET-008
- **Blocked By**: TICKET-002, TICKET-003

---

### TICKET-007: Implement Main ESPN API Service
**Priority**: High | **Story Points**: 10 | **Assignee**: Senior Backend Developer

#### Description
Create the main ESPN API service that orchestrates all ESPN data operations, combining scoreboard, events, and caching services into a cohesive public API.

#### Acceptance Criteria
- [ ] Implement `EspnApiService` class implementing `IEspnApiService`
- [ ] Integrate with `EspnScoreboardService` and `EspnCacheService`
- [ ] Implement all interface methods with proper error handling
- [ ] Add current week detection logic based on system date
- [ ] Support date-based game lookups with proper week calculation
- [ ] Implement bulk operations with parallel processing where appropriate
- [ ] Add comprehensive logging for all operations
- [ ] Include performance metrics and timing logs

#### Unit Testing Requirements
```csharp
[Test]
public async Task GetCurrentWeekAsync_DuringRegularSeason_ReturnsCorrectWeek()
{
    // Test current week calculation logic
}

[Test]
public async Task GetGamesAsync_ValidParameters_ReturnsCachedIfAvailable()
{
    // Test integration with caching service
}

[Test]
public async Task GetWeekPlayerStatsAsync_MultipleGames_AggregatesCorrectly()
{
    // Test bulk operations and data aggregation
}

[Test]
public async Task ErrorHandling_ServiceFailure_LogsAndThrows()
{
    // Test error propagation and logging
}
```

#### Dependencies
- **Blocks**: TICKET-011, TICKET-012
- **Blocked By**: TICKET-005, TICKET-006

---

## 📊 Phase 3: Player Statistics Service (Week 3)

### TICKET-008: Create Player Statistics Models
**Priority**: High | **Story Points**: 5 | **Assignee**: Backend Developer

#### Description
Design and implement comprehensive data models for player statistics including passing, rushing, receiving, and defensive stats with proper validation and serialization.

#### Acceptance Criteria
- [ ] Create `PlayerStats` model with player info and stat categories
- [ ] Create `PassingStats` model: Completions, Attempts, Yards, Touchdowns, Interceptions, Sacks, QBR, Rating
- [ ] Create `RushingStats` model: Carries, Yards, Average, Touchdowns, Long
- [ ] Create `ReceivingStats` model: Receptions, Targets, Yards, Average, Touchdowns, Long
- [ ] Create `DefensiveStats` model: Tackles, Assists, Sacks, Interceptions, PassDeflections
- [ ] Add validation attributes for reasonable stat ranges
- [ ] Implement calculated properties (completion percentage, yards per carry, etc.)
- [ ] Support nullable stats for players who didn't play

#### Unit Testing Requirements
```csharp
[Test]
public void PassingStats_CalculatedProperties_AreCorrect()
{
    // Test completion percentage and other calculated fields
}

[Test]
public void PlayerStats_Validation_RejectsInvalidValues()
{
    // Test validation attributes work correctly
}

[Test]
public void PlayerStats_Serialization_HandlesNullValues()
{
    // Test JSON serialization with null stat categories
}
```

#### Dependencies
- **Blocks**: TICKET-009, TICKET-010
- **Blocked By**: TICKET-001

---

### TICKET-009: Implement Player Statistics Extraction Service
**Priority**: High | **Story Points**: 10 | **Assignee**: Senior Backend Developer

#### Description
Build the service responsible for extracting player statistics from ESPN game data, handling various data formats and mapping to our statistical models.

#### Acceptance Criteria
- [ ] Create `EspnPlayerStatsService` for player data extraction
- [ ] Parse ESPN box score data from game event responses
- [ ] Map ESPN player data to our PlayerStats models
- [ ] Handle different statistical categories based on player position
- [ ] Extract ESPN player IDs for data correlation
- [ ] Support both individual game and aggregated week statistics
- [ ] Handle missing or incomplete statistical data gracefully
- [ ] Implement player name normalization and matching

#### Unit Testing Requirements
```csharp
[Test]
public async Task ExtractPlayerStats_ValidGameData_ReturnsMappedStats()
{
    // Test player stats extraction from mock ESPN data
}

[Test]
public async Task ExtractPlayerStats_MissingData_HandlesGracefully()
{
    // Test handling of incomplete ESPN responses
}

[Test]
public async Task PlayerIdExtraction_ValidResponse_ReturnsCorrectIds()
{
    // Test ESPN player ID extraction
}

[Test]
public async Task StatMapping_AllPositions_MapsCorrectly()
{
    // Test position-specific stat mapping (QB, RB, WR, etc.)
}
```

#### Dependencies
- **Blocks**: TICKET-010, TICKET-013
- **Blocked By**: TICKET-008, TICKET-005

---

### TICKET-010: Implement Box Score Data Access
**Priority**: High | **Story Points**: 8 | **Assignee**: Backend Developer

#### Description
Create the service layer for accessing ESPN box score data, handling game-specific URLs, and extracting detailed game statistics for individual games.

#### Acceptance Criteria
- [ ] Create box score URL resolution from game events
- [ ] Implement box score data fetching and parsing
- [ ] Extract team-level statistics and individual player data
- [ ] Handle live games vs completed games differently
- [ ] Support multiple statistical views (team offense, defense, special teams)
- [ ] Parse play-by-play data if available
- [ ] Extract game metadata (weather, attendance, officials)
- [ ] Handle ESPN's different box score formats across seasons

#### Unit Testing Requirements
```csharp
[Test]
public async Task GetBoxScoreData_CompletedGame_ReturnsFullStats()
{
    // Test complete box score data extraction
}

[Test]
public async Task GetBoxScoreData_LiveGame_ReturnsPartialStats()
{
    // Test handling of in-progress games
}

[Test]
public async Task ParseTeamStats_ValidData_ExtractsCorrectly()
{
    // Test team-level statistics parsing
}
```

#### Dependencies
- **Blocks**: TICKET-013, TICKET-014
- **Blocked By**: TICKET-008, TICKET-009

---

## 🔧 Phase 4: Integration & Enhancement (Week 4)

### TICKET-011: Update Existing Quartz Job Integration
**Priority**: High | **Story Points**: 6 | **Assignee**: Backend Developer

#### Description
Modify the existing ESPNImageScrapingJob or create a new ESPN API job that uses the new service library for scheduled data collection.

#### Acceptance Criteria
- [ ] Create `EspnApiScrapingJob` implementing `IJob` interface
- [ ] Integrate with `IEspnApiService` for data collection
- [ ] Configure job scheduling for weekly execution during NFL season
- [ ] Implement data persistence (JSON files initially, database ready)
- [ ] Add job execution logging and error reporting
- [ ] Support manual job triggering for testing
- [ ] Handle job overlap prevention and timeout scenarios
- [ ] Add job status monitoring and health checks

#### Unit Testing Requirements
```csharp
[Test]
public async Task Execute_DuringNflSeason_CollectsWeekData()
{
    // Test job execution during NFL season
}

[Test]
public async Task Execute_OffSeason_SkipsExecution()
{
    // Test job behavior during off-season
}

[Test]
public async Task Execute_ServiceFailure_HandlesGracefully()
{
    // Test error handling in job execution
}
```

#### Dependencies
- **Blocks**: TICKET-015
- **Blocked By**: TICKET-007

---

### TICKET-012: Implement Resilience and Error Handling
**Priority**: High | **Story Points**: 7 | **Assignee**: Senior Backend Developer

#### Description
Add comprehensive resilience patterns including retry policies, circuit breakers, and timeout handling for robust ESPN API communication.

#### Acceptance Criteria
- [ ] Implement Polly retry policies for ESPN HTTP calls
- [ ] Add circuit breaker pattern for ESPN API health
- [ ] Configure different timeout values for different operations
- [ ] Implement exponential backoff with jitter
- [ ] Add health checks for ESPN API connectivity
- [ ] Create fallback mechanisms for critical data
- [ ] Implement rate limiting to respect ESPN's limits
- [ ] Add comprehensive error classification and handling

#### Unit Testing Requirements
```csharp
[Test]
public async Task RetryPolicy_TransientFailure_RetriesCorrectly()
{
    // Test retry behavior with mock failures
}

[Test]
public async Task CircuitBreaker_ConsecutiveFailures_OpensCircuit()
{
    // Test circuit breaker functionality
}

[Test]
public async Task HealthCheck_EspnDown_ReportsUnhealthy()
{
    // Test health check detection of ESPN issues
}
```

#### Dependencies
- **Blocks**: TICKET-015
- **Blocked By**: TICKET-007, TICKET-003

---

### TICKET-013: Add Bulk Operations and Performance Optimization
**Priority**: Medium | **Story Points**: 8 | **Assignee**: Senior Backend Developer

#### Description
Implement bulk data operations and performance optimizations for efficient collection of large datasets like entire weeks or seasons of player statistics.

#### Acceptance Criteria
- [ ] Implement parallel processing for multiple games
- [ ] Add batch operations for player statistics collection
- [ ] Optimize JSON parsing performance for large responses
- [ ] Implement streaming for large dataset operations
- [ ] Add progress reporting for long-running operations
- [ ] Configure appropriate semaphore limits for concurrent requests
- [ ] Implement memory-efficient data processing
- [ ] Add performance metrics and monitoring

#### Unit Testing Requirements
```csharp
[Test]
public async Task BulkPlayerStats_MultipleWeeks_ProcessesInParallel()
{
    // Test parallel processing of multiple weeks
}

[Test]
public async Task BulkOperations_LargeDataset_ManagesMemoryEfficiently()
{
    // Test memory usage during bulk operations
}

[Test]
public async Task ProgressReporting_LongOperation_ReportsCorrectly()
{
    // Test progress reporting functionality
}
```

#### Dependencies
- **Blocks**: TICKET-015
- **Blocked By**: TICKET-009, TICKET-010

---

## 🚀 Phase 5: Production Readiness (Week 5)

### TICKET-014: Add Comprehensive Logging and Monitoring
**Priority**: High | **Story Points**: 5 | **Assignee**: DevOps/Backend Developer

#### Description
Implement comprehensive logging, metrics, and monitoring for the ESPN API service to ensure production observability and debugging capabilities.

#### Acceptance Criteria
- [ ] Add structured logging using Serilog for all operations
- [ ] Implement performance metrics collection (response times, cache hit rates)
- [ ] Add custom metrics for ESPN API health and usage
- [ ] Create dashboard-ready log formats for monitoring tools
- [ ] Implement log correlation IDs for request tracing
- [ ] Add alerting triggers for service degradation
- [ ] Include business metrics (games processed, players extracted)
- [ ] Add diagnostic endpoints for service health

#### Unit Testing Requirements
```csharp
[Test]
public void Logging_AllOperations_WriteStructuredLogs()
{
    // Test that all operations write appropriate log entries
}

[Test]
public void Metrics_Collection_CapturesPerformanceData()
{
    // Test metrics collection and formatting
}

[Test]
public void CorrelationIds_RequestFlow_MaintainsTracing()
{
    // Test correlation ID propagation through service calls
}
```

#### Dependencies
- **Blocks**: TICKET-015
- **Blocked By**: TICKET-010, TICKET-011

---

### TICKET-015: Production Deployment and Documentation
**Priority**: High | **Story Points**: 4 | **Assignee**: DevOps/Technical Writer

#### Description
Create production deployment artifacts, comprehensive documentation, and operational runbooks for the ESPN API service.

#### Acceptance Criteria
- [ ] Create deployment scripts and configuration files
- [ ] Write API documentation with usage examples
- [ ] Create operational runbooks for common scenarios
- [ ] Document ESPN API rate limits and best practices
- [ ] Create troubleshooting guides for common issues
- [ ] Add service architecture diagrams
- [ ] Create performance tuning guidelines
- [ ] Document backup and recovery procedures

#### Unit Testing Requirements
```csharp
[Test]
public void Documentation_Examples_ExecuteSuccessfully()
{
    // Test that all code examples in documentation work
}

[Test]
public void DeploymentScripts_Configuration_IsValid()
{
    // Test deployment script functionality
}
```

#### Dependencies
- **Blocks**: None (Final ticket)
- **Blocked By**: TICKET-011, TICKET-012, TICKET-013, TICKET-014

---

## 📋 Sprint Summary

### Development Team Allocation
- **Senior Backend Developer**: 6 tickets (complex service implementation)
- **Backend Developer**: 7 tickets (models, services, integration)
- **DevOps/Technical Writer**: 2 tickets (infrastructure, documentation)

### Story Points Distribution
- **Phase 1**: 18 points (Foundation)
- **Phase 2**: 23 points (Core Services)
- **Phase 3**: 23 points (Statistics)
- **Phase 4**: 21 points (Integration)
- **Phase 5**: 9 points (Production)
- **Total**: 94 story points

### Critical Path Dependencies
1. TICKET-001 → TICKET-002 → TICKET-005 → TICKET-007 → TICKET-011 → TICKET-015
2. TICKET-008 → TICKET-009 → TICKET-010 → TICKET-013

### Success Metrics
- ✅ All ESPN data accessible without browser automation
- ✅ Sub-500ms response times for cached data
- ✅ 99%+ reliability for data collection jobs
- ✅ Complete player statistics for all NFL games
- ✅ Production-ready monitoring and alerting
