# ESPN API Integration Best Practices

This guide provides comprehensive best practices for integrating with the ESPN API, optimizing performance, and ensuring reliable data retrieval.

## Table of Contents
- [API Rate Limits and Throttling](#api-rate-limits-and-throttling)
- [Caching Strategies](#caching-strategies)
- [Error Handling Patterns](#error-handling-patterns)
- [Authentication and Security](#authentication-and-security)
- [Performance Optimization](#performance-optimization)
- [Data Consistency](#data-consistency)
- [Monitoring and Observability](#monitoring-and-observability)
- [ESPN API Endpoints Reference](#espn-api-endpoints-reference)

---

## API Rate Limits and Throttling

### 🚦 Understanding ESPN API Limits

ESPN imposes rate limits to ensure fair usage and service stability. Our service implements sophisticated rate limiting to stay within these constraints.

#### Rate Limit Guidelines
- **Requests per minute**: Recommended maximum of 100 requests/minute
- **Burst capacity**: Short bursts up to 200 requests/minute acceptable
- **Daily limits**: Monitor total daily requests to avoid hitting quotas
- **Concurrent connections**: Limit to 5 simultaneous connections

#### Rate Limit Implementation
```csharp
// Our rate limiting service configuration
public class EspnRateLimitService : IEspnRateLimitService
{
    private readonly SemaphoreSlim _requestSemaphore;
    private readonly Timer _resetTimer;
    private int _requestsThisMinute;
    private readonly int _maxRequestsPerMinute = 90; // Conservative limit

    public async Task<bool> CanMakeRequestAsync()
    {
        if (_requestsThisMinute >= _maxRequestsPerMinute)
        {
            // Wait until next minute or implement exponential backoff
            await Task.Delay(TimeSpan.FromSeconds(60));
            return false;
        }
        
        Interlocked.Increment(ref _requestsThisMinute);
        return true;
    }
}
```

#### Rate Limit Best Practices
1. **Implement Exponential Backoff**
   ```csharp
   public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation)
   {
       int attempts = 0;
       TimeSpan delay = TimeSpan.FromSeconds(1);
       
       while (attempts < 5)
       {
           try
           {
               return await operation();
           }
           catch (HttpRequestException ex) when (ex.Message.Contains("429"))
           {
               attempts++;
               await Task.Delay(delay);
               delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
           }
       }
       throw new Exception("Max retry attempts exceeded");
   }
   ```

2. **Monitor Rate Limit Headers**
   ```csharp
   private void ProcessRateLimitHeaders(HttpResponseMessage response)
   {
       if (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining))
       {
           var remainingCount = int.Parse(remaining.First());
           if (remainingCount < 10)
           {
               // Slow down requests
               _logger.LogWarning("Rate limit approaching: {Remaining} requests remaining", remainingCount);
           }
       }
   }
   ```

3. **Distribute Load Across Time**
   ```csharp
   // Spread requests evenly throughout the minute
   private async Task WaitForNextSlot()
   {
       var millisecondsPerRequest = 60000 / _maxRequestsPerMinute;
       await Task.Delay(millisecondsPerRequest);
   }
   ```

---

## Caching Strategies

### 💾 Intelligent Caching Implementation

Effective caching is crucial for ESPN API integration due to rate limits and performance requirements.

#### Cache TTL Guidelines

| Data Type | TTL | Reasoning |
|-----------|-----|-----------|
| Team Information | 24 hours | Rarely changes |
| Season Schedule | 12 hours | Updates infrequently |
| Player Stats (Historical) | 6 hours | Static once game complete |
| Live Game Data | 30 seconds | Rapidly changing |
| Scoreboard Data | 1 minute | Updates during games |
| Current Week Games | 1 hour | Schedule changes rare |

#### Multi-Layer Caching Strategy
```csharp
public class EspnCacheService : IEspnCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<EspnCacheService> _logger;

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, TimeSpan? ttl = null)
    {
        // Layer 1: Memory Cache (fastest)
        if (_memoryCache.TryGetValue(key, out T cached))
        {
            _logger.LogDebug("Cache hit (memory): {Key}", key);
            return cached;
        }

        // Layer 2: Distributed Cache (Redis/SQL)
        var distributedValue = await _distributedCache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(distributedValue))
        {
            var deserializedValue = JsonSerializer.Deserialize<T>(distributedValue);
            
            // Populate memory cache for next request
            _memoryCache.Set(key, deserializedValue, TimeSpan.FromMinutes(5));
            
            _logger.LogDebug("Cache hit (distributed): {Key}", key);
            return deserializedValue;
        }

        // Layer 3: Fetch from API
        _logger.LogDebug("Cache miss, fetching from API: {Key}", key);
        var result = await factory();
        
        // Store in both caches
        await SetAsync(key, result, ttl);
        
        return result;
    }
}
```

#### Cache Key Strategies
```csharp
public static class CacheKeys
{
    public static string TeamInfo(int teamId) => $"team:{teamId}";
    public static string PlayerStats(int playerId, int season, int week) => 
        $"player:{playerId}:stats:{season}:{week}";
    public static string Scoreboard(int season, int seasonType, int week) => 
        $"scoreboard:{season}:{seasonType}:{week}";
    public static string GameBoxScore(int gameId) => $"boxscore:{gameId}";
    public static string SeasonSchedule(int teamId, int season) => 
        $"schedule:{teamId}:{season}";
}
```

#### Cache Invalidation Patterns
```csharp
public async Task InvalidateGameDataAsync(int gameId)
{
    var keysToInvalidate = new[]
    {
        CacheKeys.GameBoxScore(gameId),
        CacheKeys.Scoreboard(GetSeasonFromGameId(gameId), GetSeasonTypeFromGameId(gameId), GetWeekFromGameId(gameId))
    };

    foreach (var key in keysToInvalidate)
    {
        await _distributedCache.RemoveAsync(key);
        _memoryCache.Remove(key);
    }
}
```

#### Cache Warming Strategy
```csharp
public class CacheWarmupService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await WarmCriticalCaches();
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task WarmCriticalCaches()
    {
        // Pre-load current week scoreboard
        var currentWeek = GetCurrentWeek();
        await _espnService.GetScoreboardAsync(2024, 2, currentWeek);

        // Pre-load popular team data
        var popularTeams = new[] { 1, 2, 3, 4, 5 }; // Cowboys, Patriots, etc.
        foreach (var teamId in popularTeams)
        {
            await _espnService.GetTeamInfoAsync(teamId);
        }
    }
}
```

---

## Error Handling Patterns

### 🛡️ Robust Error Management

ESPN API integration requires comprehensive error handling for reliable service operation.

#### Error Categories and Responses

| Error Type | HTTP Code | Retry Strategy | Action |
|------------|-----------|----------------|---------|
| Rate Limited | 429 | Exponential backoff | Wait and retry |
| Temporary Unavailable | 502, 503, 504 | Linear backoff | Retry with delay |
| Client Error | 400, 401, 403 | No retry | Log and alert |
| Not Found | 404 | Limited retry | Return empty result |
| Server Error | 500 | Exponential backoff | Retry with circuit breaker |

#### Comprehensive Error Handling
```csharp
public class EspnHttpService : IEspnHttpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<EspnHttpService> _logger;
    private readonly CircuitBreakerPolicy _circuitBreaker;

    public async Task<T> GetAsync<T>(string endpoint, CancellationToken cancellationToken = default)
    {
        var requestId = Guid.NewGuid().ToString("N")[..8];
        
        try
        {
            _logger.LogInformation("ESPN API Request [{RequestId}]: {Endpoint}", requestId, endpoint);
            
            var response = await _circuitBreaker.ExecuteAsync(async () =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
                request.Headers.Add("User-Agent", "ESPN-Scraper/1.0");
                request.Headers.Add("X-Request-ID", requestId);
                
                return await _httpClient.SendAsync(request, cancellationToken);
            });

            return await ProcessResponseAsync<T>(response, requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ESPN API Error [{RequestId}]: {Endpoint}", requestId, endpoint);
            throw new EspnApiException($"Failed to fetch data from {endpoint}", ex);
        }
    }

    private async Task<T> ProcessResponseAsync<T>(HttpResponseMessage response, string requestId)
    {
        switch (response.StatusCode)
        {
            case HttpStatusCode.OK:
                var content = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(content, _jsonOptions);

            case HttpStatusCode.NotFound:
                _logger.LogWarning("ESPN API Not Found [{RequestId}]: {StatusCode}", requestId, response.StatusCode);
                return default(T);

            case HttpStatusCode.TooManyRequests:
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromMinutes(1);
                _logger.LogWarning("ESPN API Rate Limited [{RequestId}]: Retry after {RetryAfter}", requestId, retryAfter);
                throw new EspnRateLimitException($"Rate limited, retry after {retryAfter}");

            case HttpStatusCode.InternalServerError:
            case HttpStatusCode.BadGateway:
            case HttpStatusCode.ServiceUnavailable:
            case HttpStatusCode.GatewayTimeout:
                _logger.LogError("ESPN API Server Error [{RequestId}]: {StatusCode}", requestId, response.StatusCode);
                throw new EspnServerException($"Server error: {response.StatusCode}");

            default:
                _logger.LogError("ESPN API Unexpected Status [{RequestId}]: {StatusCode}", requestId, response.StatusCode);
                throw new EspnApiException($"Unexpected status code: {response.StatusCode}");
        }
    }
}
```

#### Circuit Breaker Implementation
```csharp
public class EspnCircuitBreakerService
{
    private readonly CircuitBreakerPolicy _circuitBreaker;

    public EspnCircuitBreakerService()
    {
        _circuitBreaker = Policy
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<EspnServerException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromMinutes(2),
                onBreak: (exception, duration) =>
                {
                    // Log circuit breaker opened
                    Console.WriteLine($"Circuit breaker opened for {duration}");
                },
                onReset: () =>
                {
                    // Log circuit breaker closed
                    Console.WriteLine("Circuit breaker reset");
                });
    }
}
```

#### Custom Exception Classes
```csharp
public class EspnApiException : Exception
{
    public string RequestId { get; }
    public string Endpoint { get; }

    public EspnApiException(string message, Exception innerException = null) 
        : base(message, innerException) { }
}

public class EspnRateLimitException : EspnApiException
{
    public TimeSpan RetryAfter { get; }

    public EspnRateLimitException(string message, TimeSpan retryAfter = default) 
        : base(message)
    {
        RetryAfter = retryAfter;
    }
}

public class EspnServerException : EspnApiException
{
    public HttpStatusCode StatusCode { get; }

    public EspnServerException(string message, HttpStatusCode statusCode = HttpStatusCode.InternalServerError) 
        : base(message)
    {
        StatusCode = statusCode;
    }
}
```

---

## Authentication and Security

### 🔐 Security Best Practices

While ESPN's public API doesn't require authentication for basic data, following security best practices is essential.

#### HTTP Client Configuration
```csharp
public static class EspnHttpClientConfiguration
{
    public static void ConfigureHttpClient(HttpClient client)
    {
        // Set appropriate timeout
        client.Timeout = TimeSpan.FromSeconds(30);

        // Add security headers
        client.DefaultRequestHeaders.Add("User-Agent", "ESPN-Scraper/1.0 (+https://your-domain.com/contact)");
        client.DefaultRequestHeaders.Add("Accept", "application/json");
        client.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate");

        // Enable compression
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
    }
}
```

#### Request Security
```csharp
private HttpRequestMessage CreateSecureRequest(string endpoint)
{
    var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
    
    // Add correlation ID for tracing
    request.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());
    
    // Add request timestamp
    request.Headers.Add("X-Request-Timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
    
    // Validate endpoint URL
    if (!IsValidEspnEndpoint(endpoint))
    {
        throw new ArgumentException("Invalid ESPN endpoint", nameof(endpoint));
    }
    
    return request;
}

private bool IsValidEspnEndpoint(string endpoint)
{
    var allowedHosts = new[] 
    {
        "sports.core.api.espn.com",
        "site.api.espn.com"
    };
    
    if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
    {
        return allowedHosts.Contains(uri.Host);
    }
    
    return false;
}
```

#### Data Sanitization
```csharp
public class EspnDataSanitizer
{
    public static string SanitizeTeamName(string teamName)
    {
        if (string.IsNullOrWhiteSpace(teamName))
            return "Unknown Team";
            
        // Remove potentially dangerous characters
        var sanitized = Regex.Replace(teamName, @"[<>""'&]", "");
        
        // Limit length
        return sanitized.Length > 100 ? sanitized[..100] : sanitized;
    }

    public static string SanitizePlayerName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
            return "Unknown Player";
            
        // Allow only letters, spaces, periods, hyphens, and apostrophes
        var sanitized = Regex.Replace(playerName, @"[^a-zA-Z\s\.\-']", "");
        
        return sanitized.Length > 50 ? sanitized[..50] : sanitized;
    }
}
```

---

## Performance Optimization

### ⚡ Optimization Strategies

#### Parallel Request Processing
```csharp
public async Task<Dictionary<int, Team>> GetMultipleTeamsAsync(IEnumerable<int> teamIds)
{
    var semaphore = new SemaphoreSlim(5); // Limit concurrent requests
    var teams = new ConcurrentDictionary<int, Team>();

    var tasks = teamIds.Select(async teamId =>
    {
        await semaphore.WaitAsync();
        try
        {
            var team = await GetTeamAsync(teamId);
            if (team != null)
            {
                teams.TryAdd(teamId, team);
            }
        }
        finally
        {
            semaphore.Release();
        }
    });

    await Task.WhenAll(tasks);
    return teams.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
}
```

#### Efficient Data Processing
```csharp
public class EspnDataProcessor
{
    public async IAsyncEnumerable<Player> ProcessPlayersStreamAsync(
        IAsyncEnumerable<RawPlayerData> rawData,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var raw in rawData.WithCancellation(cancellationToken))
        {
            // Process data incrementally to avoid memory pressure
            var player = await TransformPlayerDataAsync(raw);
            
            if (player != null)
            {
                yield return player;
            }
            
            // Yield control periodically
            if (cancellationToken.IsCancellationRequested)
                yield break;
        }
    }
}
```

#### Memory-Efficient JSON Processing
```csharp
public class EspnJsonProcessor
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<T> DeserializeStreamAsync<T>(Stream jsonStream)
    {
        return await JsonSerializer.DeserializeAsync<T>(jsonStream, _options);
    }

    public async IAsyncEnumerable<T> DeserializeArrayStreamAsync<T>(Stream jsonStream)
    {
        using var document = await JsonDocument.ParseAsync(jsonStream);
        
        if (document.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in document.RootElement.EnumerateArray())
            {
                var item = JsonSerializer.Deserialize<T>(element.GetRawText(), _options);
                if (item != null)
                {
                    yield return item;
                }
            }
        }
    }
}
```

---

## Data Consistency

### 🎯 Ensuring Data Quality

#### Data Validation
```csharp
public class EspnDataValidator
{
    public bool ValidateGameData(Game game)
    {
        var errors = new List<string>();

        // Validate required fields
        if (game.Id <= 0)
            errors.Add("Invalid game ID");

        if (string.IsNullOrWhiteSpace(game.Date))
            errors.Add("Missing game date");

        if (game.HomeTeam?.Id <= 0 || game.AwayTeam?.Id <= 0)
            errors.Add("Invalid team data");

        // Validate data consistency
        if (game.Status?.Completed == true && (game.HomeScore == null || game.AwayScore == null))
            errors.Add("Completed game missing scores");

        if (errors.Any())
        {
            _logger.LogWarning("Game data validation failed: {Errors}", string.Join(", ", errors));
            return false;
        }

        return true;
    }
}
```

#### Data Reconciliation
```csharp
public class EspnDataReconciliationService
{
    public async Task<ReconciliationResult> ReconcileGameDataAsync(int gameId)
    {
        // Fetch from multiple sources
        var scoreboardData = await _scoreboardService.GetGameAsync(gameId);
        var boxScoreData = await _boxScoreService.GetGameAsync(gameId);

        var discrepancies = new List<string>();

        // Compare scores
        if (scoreboardData.HomeScore != boxScoreData.HomeScore)
        {
            discrepancies.Add($"Home score mismatch: Scoreboard={scoreboardData.HomeScore}, BoxScore={boxScoreData.HomeScore}");
        }

        // Compare status
        if (scoreboardData.Status?.Type != boxScoreData.Status?.Type)
        {
            discrepancies.Add($"Status mismatch: Scoreboard={scoreboardData.Status?.Type}, BoxScore={boxScoreData.Status?.Type}");
        }

        return new ReconciliationResult
        {
            GameId = gameId,
            IsConsistent = !discrepancies.Any(),
            Discrepancies = discrepancies,
            RecommendedSource = DetermineRecommendedSource(scoreboardData, boxScoreData)
        };
    }
}
```

---

## Monitoring and Observability

### 📊 Comprehensive Monitoring

#### Custom Metrics Collection
```csharp
public class EspnMetricsCollector
{
    private readonly Counter _apiCallsCounter;
    private readonly Histogram _responseTimeHistogram;
    private readonly Gauge _cacheHitRateGauge;

    public EspnMetricsCollector()
    {
        _apiCallsCounter = Metrics.CreateCounter(
            "espn_api_calls_total",
            "Total number of ESPN API calls",
            new[] { "endpoint", "status" });

        _responseTimeHistogram = Metrics.CreateHistogram(
            "espn_api_response_time_seconds",
            "ESPN API response time in seconds",
            new[] { "endpoint" });

        _cacheHitRateGauge = Metrics.CreateGauge(
            "espn_cache_hit_rate",
            "Cache hit rate percentage");
    }

    public void RecordApiCall(string endpoint, string status, double responseTimeSeconds)
    {
        _apiCallsCounter.WithLabels(endpoint, status).Inc();
        _responseTimeHistogram.WithLabels(endpoint).Observe(responseTimeSeconds);
    }

    public void UpdateCacheHitRate(double hitRate)
    {
        _cacheHitRateGauge.Set(hitRate);
    }
}
```

#### Health Check Implementation
```csharp
public class EspnApiHealthCheck : IHealthCheck
{
    private readonly IEspnApiService _espnService;
    private readonly ILogger<EspnApiHealthCheck> _logger;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Perform lightweight health check
            var scoreboard = await _espnService.GetCurrentWeekScoreboardAsync();
            
            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                ["responseTime"] = stopwatch.ElapsedMilliseconds,
                ["gameCount"] = scoreboard?.Events?.Count ?? 0,
                ["timestamp"] = DateTimeOffset.UtcNow
            };

            if (stopwatch.ElapsedMilliseconds > 5000) // 5 second threshold
            {
                return HealthCheckResult.Degraded("ESPN API responding slowly", data: data);
            }

            return HealthCheckResult.Healthy("ESPN API is responsive", data: data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ESPN API health check failed");
            return HealthCheckResult.Unhealthy("ESPN API is not accessible", ex);
        }
    }
}
```

---

## ESPN API Endpoints Reference

### 🌐 Key ESPN API Endpoints

#### NFL Scoreboard
```
GET https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard
Parameters:
- dates: YYYYMMDD (optional, defaults to current date)
- seasontype: 1 (preseason), 2 (regular), 3 (postseason)
- week: 1-18 for regular season
- limit: number of games to return

Example:
https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard?seasontype=2&week=1&dates=20240908
```

#### Game Box Score
```
GET https://sports.core.api.espn.com/v3/sports/football/nfl/events/{gameId}/competitions/{gameId}/boxscore
Parameters:
- gameId: ESPN game identifier

Example:
https://sports.core.api.espn.com/v3/sports/football/nfl/events/401671426/competitions/401671426/boxscore
```

#### Team Information
```
GET https://sports.core.api.espn.com/v3/sports/football/nfl/teams/{teamId}
Parameters:
- teamId: ESPN team identifier (1-32 for NFL)

Example:
https://sports.core.api.espn.com/v3/sports/football/nfl/teams/1
```

#### Team Roster
```
GET https://sports.core.api.espn.com/v3/sports/football/nfl/teams/{teamId}/roster
Parameters:
- teamId: ESPN team identifier

Example:
https://sports.core.api.espn.com/v3/sports/football/nfl/teams/1/roster
```

#### Season Schedule
```
GET https://sports.core.api.espn.com/v3/sports/football/nfl/teams/{teamId}/schedule
Parameters:
- teamId: ESPN team identifier
- season: year (e.g., 2024)

Example:
https://sports.core.api.espn.com/v3/sports/football/nfl/teams/1/schedule?season=2024
```

#### Usage Examples
```csharp
// Get current week scoreboard
var scoreboardUrl = "https://sports.core.api.espn.com/v3/sports/football/nfl/scoreboard";
var scoreboard = await _httpService.GetAsync<ScoreboardData>(scoreboardUrl);

// Get specific game box score
var gameId = 401671426;
var boxScoreUrl = $"https://sports.core.api.espn.com/v3/sports/football/nfl/events/{gameId}/competitions/{gameId}/boxscore";
var boxScore = await _httpService.GetAsync<BoxScore>(boxScoreUrl);

// Get team information
var teamId = 1; // Cowboys
var teamUrl = $"https://sports.core.api.espn.com/v3/sports/football/nfl/teams/{teamId}";
var team = await _httpService.GetAsync<Team>(teamUrl);
```

---

## Implementation Checklist

### ✅ ESPN API Integration Checklist

- [ ] **Rate Limiting**
  - [ ] Implement request throttling
  - [ ] Add exponential backoff
  - [ ] Monitor rate limit headers
  - [ ] Set up rate limit alerts

- [ ] **Caching**
  - [ ] Implement multi-layer caching
  - [ ] Configure appropriate TTLs
  - [ ] Set up cache warming
  - [ ] Monitor cache hit rates

- [ ] **Error Handling**
  - [ ] Handle all HTTP status codes
  - [ ] Implement circuit breaker
  - [ ] Add retry logic
  - [ ] Log errors comprehensively

- [ ] **Security**
  - [ ] Validate all endpoints
  - [ ] Sanitize response data
  - [ ] Implement request signing
  - [ ] Add correlation IDs

- [ ] **Performance**
  - [ ] Optimize concurrent requests
  - [ ] Stream large datasets
  - [ ] Monitor memory usage
  - [ ] Profile critical paths

- [ ] **Monitoring**
  - [ ] Set up health checks
  - [ ] Collect custom metrics
  - [ ] Configure alerting
  - [ ] Implement distributed tracing

---

For additional information, see:
- [API Documentation](API_DOCUMENTATION.md)
- [Operational Runbooks](OPERATIONAL_RUNBOOKS.md)
- [Troubleshooting Guide](TROUBLESHOOTING.md)