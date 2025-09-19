# ESPN API Service - API Documentation

Complete API reference for the ESPN API Service, including all diagnostic endpoints, service interfaces, and integration examples.

## Table of Contents
- [Diagnostic API Endpoints](#diagnostic-api-endpoints)
- [Service Interfaces](#service-interfaces)
- [Configuration Reference](#configuration-reference)
- [Usage Examples](#usage-examples)
- [Error Codes](#error-codes)
- [Rate Limiting](#rate-limiting)

## Diagnostic API Endpoints

### Health Check Endpoint

**GET** `/health`

Returns the overall health status of the service and all its dependencies.

#### Response Format
```json
{
  "status": "Healthy|Degraded|Unhealthy",
  "totalDuration": 45.23,
  "entries": {
    "espn_api": {
      "status": "Healthy",
      "duration": 12.34,
      "description": "ESPN API connectivity check",
      "data": {
        "endpoint": "https://sports.core.api.espn.com",
        "responseTime": 123,
        "lastSuccessfulCall": "2025-09-19T16:30:00.000Z"
      }
    }
  }
}
```

#### Status Codes
- `200 OK` - Service is healthy
- `200 OK` - Service is degraded (some non-critical components unhealthy)
- `503 Service Unavailable` - Service is unhealthy

#### Usage Example
```bash
curl -X GET http://localhost:5000/health \
  -H "Accept: application/json"
```

```csharp
// C# Client Example
var httpClient = new HttpClient();
var response = await httpClient.GetAsync("http://localhost:5000/health");
var healthData = await response.Content.ReadFromJsonAsync<HealthCheckResponse>();

if (healthData.Status == "Healthy")
{
    Console.WriteLine("Service is running properly");
}
```

---

### Metrics Endpoint

**GET** `/metrics`

Returns performance and business metrics collected by the service.

#### Response Format
```json
{
  "timestamp": "2025-09-19T16:30:00.000Z",
  "performance": {
    "apiResponseTime": {
      "average": 245.67,
      "minimum": 89.12,
      "maximum": 1234.56,
      "count": 150,
      "percentiles": {
        "p50": 200.45,
        "p95": 500.12,
        "p99": 800.67
      }
    },
    "cacheMetrics": {
      "hitRate": 85.4,
      "totalOperations": 1000,
      "hits": 854,
      "misses": 146,
      "averageDuration": 2.3
    },
    "memoryUsage": {
      "current": 256.7,
      "peak": 312.8,
      "gcCollections": {
        "gen0": 45,
        "gen1": 12,
        "gen2": 3
      }
    }
  },
  "business": {
    "gamesProcessed": 16,
    "playersExtracted": 1894,
    "dataVolumeGB": 2.4,
    "apiCallsToday": 2847,
    "errorRate": 0.8
  },
  "summary": {
    "totalMetricTypes": 8,
    "totalDataPoints": 1205
  }
}
```

#### Usage Example
```bash
# Get all metrics
curl -X GET http://localhost:5000/metrics

# Monitor specific metrics with jq
curl -s http://localhost:5000/metrics | jq '.performance.apiResponseTime.average'
```

---

### System Information Endpoint

**GET** `/system-info`

Returns detailed system resource information and performance statistics.

#### Response Format
```json
{
  "timestamp": "2025-09-19T16:30:00.000Z",
  "application": {
    "name": "ESPNScrape",
    "version": "1.0.0",
    "environment": "Production",
    "uptime": "2.15:45:30",
    "startTime": "2025-09-17T00:45:00.000Z"
  },
  "system": {
    "operatingSystem": "Microsoft Windows 10.0.19045",
    "architecture": "X64",
    "processorCount": 8,
    "workingSet": 268435456,
    "privateMemory": 245760000,
    "peakWorkingSet": 314572800
  },
  "runtime": {
    "dotNetVersion": "8.0.0",
    "gcMode": "Server",
    "totalProcessorTime": 1234.56,
    "userProcessorTime": 987.65,
    "privilegedProcessorTime": 246.91
  }
}
```

#### Usage Example
```bash
# Get system information
curl -X GET http://localhost:5000/system-info

# Monitor memory usage
curl -s http://localhost:5000/system-info | jq '.system.workingSet'
```

---

### Alerts Endpoint

**GET** `/alerts`

Returns current alert conditions and their severity levels.

#### Response Format
```json
{
  "timestamp": "2025-09-19T16:30:00.000Z",
  "alerts": [
    {
      "id": "api-response-time-001",
      "type": "ResponseTime",
      "component": "/api/nfl/games",
      "severity": "High",
      "message": "Average response time exceeding threshold",
      "currentValue": 2500.0,
      "threshold": 2000.0,
      "triggeredAt": "2025-09-19T16:25:00.000Z",
      "status": "Active"
    },
    {
      "id": "cache-hit-rate-002", 
      "type": "CacheHitRate",
      "component": "PlayerStatsCache",
      "severity": "Medium",
      "message": "Cache hit rate below optimal threshold",
      "currentValue": 65.5,
      "threshold": 70.0,
      "triggeredAt": "2025-09-19T16:20:00.000Z",
      "status": "Active"
    }
  ],
  "summary": {
    "total": 2,
    "critical": 0,
    "high": 1,
    "medium": 1,
    "hasActiveAlerts": true
  }
}
```

#### Usage Example
```bash
# Get all alerts
curl -X GET http://localhost:5000/alerts

# Check for critical alerts
curl -s http://localhost:5000/alerts | jq '.summary.critical'

# Filter high severity alerts
curl -s http://localhost:5000/alerts | jq '.alerts[] | select(.severity == "High")'
```

---

### Reset Metrics Endpoint

**POST** `/metrics/reset`

Resets all collected metrics. Primarily used for testing and development.

#### Response Format
```json
{
  "message": "Metrics reset successfully",
  "timestamp": "2025-09-19T16:30:00.000Z",
  "resetCounters": {
    "performanceMetrics": 150,
    "businessMetrics": 45,
    "cacheMetrics": 1000,
    "alertConditions": 3
  }
}
```

#### Usage Example
```bash
# Reset all metrics
curl -X POST http://localhost:5000/metrics/reset \
  -H "Content-Type: application/json"
```

---

### Configuration Endpoint

**GET** `/config`

Returns the current service configuration (sensitive values are masked).

#### Response Format
```json
{
  "timestamp": "2025-09-19T16:30:00.000Z",
  "environment": "Production",
  "configuration": {
    "logging": {
      "enableStructuredLogging": true,
      "enableDetailedMetrics": true,
      "logLevel": "Information"
    },
    "cache": {
      "defaultTtlMinutes": 30,
      "maxCacheSize": 1000,
      "enableCacheWarming": true
    },
    "espnApi": {
      "baseUrl": "https://sports.core.api.espn.com",
      "rateLimitRequestsPerMinute": 100,
      "maxRetryAttempts": 3
    },
    "alerting": {
      "enableAlerting": true,
      "errorRateThreshold": 5.0,
      "responseTimeThresholdMs": 2000,
      "monitoringInterval": "00:01:00"
    }
  }
}
```

---

### Full Diagnostic Endpoint

**GET** `/full-diagnostic`

Returns a comprehensive diagnostic report combining health, metrics, alerts, and system information.

#### Response Format
```json
{
  "timestamp": "2025-09-19T16:30:00.000Z",
  "diagnosticDuration": 45.67,
  "overallStatus": "Healthy",
  "health": {
    "status": "Healthy",
    "totalDuration": 23.45,
    "unhealthyChecks": 0
  },
  "alerts": {
    "count": 2,
    "hasActiveAlerts": true,
    "criticalAlerts": 0
  },
  "performance": {
    "apiResponseTime": 245.67,
    "cacheHitRate": 85.4,
    "memoryUsageMB": 256.7,
    "errorRate": 0.8
  },
  "system": {
    "uptime": "2.15:45:30",
    "cpuUsage": 15.4,
    "memoryUsage": 268435456
  },
  "recommendations": [
    "Consider increasing cache TTL for static data",
    "Monitor API response times during peak hours",
    "Review alerting thresholds for cache hit rate"
  ]
}
```

#### Usage Example
```bash
# Get comprehensive diagnostic report
curl -X GET http://localhost:5000/full-diagnostic

# Save diagnostic report to file
curl -s http://localhost:5000/full-diagnostic > diagnostic-report.json
```

---

## Service Interfaces

### IEspnApiService

Main service interface for ESPN API operations.

```csharp
public interface IEspnApiService
{
    Task<ScoreboardData> GetScoreboardAsync(int? week = null, int? year = null, int? seasonType = null);
    Task<GameEvent> GetGameDetailsAsync(string gameId);
    Task<List<PlayerStats>> GetPlayerStatsAsync(string gameId);
    Task<BoxScore> GetBoxScoreAsync(string gameId);
    Task<List<Player>> GetPlayersAsync(int limit = 1000, bool activeOnly = true);
    Task<Team> GetTeamAsync(string teamId);
}
```

### IEspnLoggingService

Enhanced logging service with structured logging and correlation tracking.

```csharp
public interface IEspnLoggingService
{
    void LogApiOperation(string endpoint, string method, TimeSpan responseTime, int statusCode, bool success, string? errorMessage = null);
    void LogCacheOperation(string operation, string key, bool hit, TimeSpan? duration = null);
    void LogBusinessMetric(string metricName, object value, Dictionary<string, object>? additionalProperties = null);
    void LogBulkOperationProgress(string operationId, string operationType, int completed, int total, TimeSpan elapsed);
    IDisposable BeginTimedOperation(string operationName, Dictionary<string, object>? properties = null);
    IDisposable BeginCorrelationContext(string correlationId);
    string GetOrGenerateCorrelationId();
}
```

### IEspnMetricsService

Metrics collection and performance monitoring service.

```csharp
public interface IEspnMetricsService
{
    void RecordApiResponseTime(string endpoint, TimeSpan responseTime);
    void RecordCacheOperation(string operation, bool hit, TimeSpan duration);
    void RecordBusinessMetric(string metricName, double value);
    void RecordMemoryUsage(long bytes);
    MetricsSnapshot GetCurrentMetrics();
    List<AlertCondition> CheckAlertConditions();
    void ResetMetrics();
}
```

### IEspnAlertingService

Alert processing and notification service.

```csharp
public interface IEspnAlertingService
{
    Task ProcessAlertsAsync(List<AlertCondition> alertConditions);
    Task SendAlertAsync(AlertCondition alert, AlertSeverity severity);
    List<AlertCondition> GetActiveAlerts();
    void ClearResolvedAlerts();
    bool IsAlertActive(string alertKey);
}
```

---

## Configuration Reference

### Complete Configuration Schema

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "ESPNScrape.Services": "Information"
    },
    "StructuredLogging": {
      "EnableStructuredLogging": true,
      "UseJsonFormat": true,
      "IncludeScopes": true,
      "IncludeTimestamp": true,
      "TimestampFormat": "yyyy-MM-dd HH:mm:ss.fff zzz"
    },
    "PerformanceMetrics": {
      "TrackResponseTimes": true,
      "TrackCacheMetrics": true,
      "TrackMemoryMetrics": true,
      "EnableDetailedMetrics": true,
      "ResponseTimeThresholdMs": 1000
    },
    "BusinessMetrics": {
      "EnableBusinessMetrics": true,
      "TrackApiCallCounts": true,
      "TrackDataProcessingVolumes": true,
      "TrackErrorRates": true
    },
    "Alerting": {
      "EnableAlerting": true,
      "ErrorRateThreshold": 5.0,
      "ResponseTimeThresholdMs": 2000,
      "CacheHitRateThreshold": 80.0,
      "MemoryUsageThresholdMB": 1000,
      "MonitoringInterval": "00:01:00",
      "AlertCooldownPeriod": "00:05:00",
      "EmailEnabled": false,
      "WebhookEnabled": false
    }
  },
  "Cache": {
    "DefaultTtlMinutes": 30,
    "SeasonDataTtlHours": 24,
    "CompletedGameTtlMinutes": 60,
    "LiveGameTtlSeconds": 30,
    "PlayerStatsTtlMinutes": 15,
    "MaxCacheSize": 1000,
    "EnableCacheWarming": true
  },
  "Resilience": {
    "RetryPolicy": {
      "MaxRetryAttempts": 3,
      "BaseDelayMs": 1000,
      "MaxDelayMs": 10000,
      "BackoffMultiplier": 2.0
    },
    "CircuitBreaker": {
      "FailureThreshold": 5,
      "SamplingDurationMs": 10000,
      "MinimumThroughput": 10,
      "DurationOfBreakMs": 30000
    },
    "Timeout": {
      "DefaultTimeoutMs": 30000,
      "LongRunningTimeoutMs": 120000
    }
  },
  "BulkOperations": {
    "DefaultBatchSize": 100,
    "MaxConcurrency": 5,
    "DelayBetweenBatchesMs": 100,
    "ProgressReportingInterval": 50
  }
}
```

---

## Usage Examples

### Basic Service Integration

```csharp
// Dependency Injection Setup
services.AddScoped<IEspnApiService, EspnApiService>();
services.AddSingleton<IEspnLoggingService, EspnLoggingService>();
services.AddSingleton<IEspnMetricsService, EspnMetricsService>();

// Using the service
public class GameController : ControllerBase
{
    private readonly IEspnApiService _espnService;
    private readonly IEspnLoggingService _logger;

    public GameController(IEspnApiService espnService, IEspnLoggingService logger)
    {
        _espnService = espnService;
        _logger = logger;
    }

    [HttpGet("games")]
    public async Task<IActionResult> GetGames()
    {
        using var operation = _logger.BeginTimedOperation("GetGames");
        
        try
        {
            var scoreboard = await _espnService.GetScoreboardAsync();
            _logger.LogBusinessMetric("games_retrieved", scoreboard.Events.Count);
            return Ok(scoreboard);
        }
        catch (Exception ex)
        {
            _logger.LogApiOperation("/games", "GET", TimeSpan.Zero, 500, false, ex.Message);
            return StatusCode(500, "Error retrieving games");
        }
    }
}
```

### Monitoring Integration

```csharp
// Custom health check
public class CustomHealthCheck : IHealthCheck
{
    private readonly IEspnApiService _espnService;

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await _espnService.GetScoreboardAsync();
            stopwatch.Stop();

            var data = new Dictionary<string, object>
            {
                {"responseTime", stopwatch.ElapsedMilliseconds},
                {"endpoint", "scoreboard"}
            };

            return HealthCheckResult.Healthy("ESPN API is responding", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("ESPN API is not responding", ex);
        }
    }
}

// Register health check
services.AddHealthChecks()
    .AddCheck<CustomHealthCheck>("custom_espn_check");
```

### Alert Handling

```csharp
// Custom alert processor
public class CustomAlertHandler : BackgroundService
{
    private readonly IEspnAlertingService _alertingService;
    private readonly ILogger<CustomAlertHandler> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var alerts = _alertingService.GetActiveAlerts();
            
            foreach (var alert in alerts.Where(a => a.Severity == AlertSeverity.Critical))
            {
                _logger.LogCritical("Critical alert: {Message}", alert.Message);
                // Send to external monitoring system
                await SendToMonitoringSystem(alert);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
```

---

## Error Codes

### HTTP Status Codes

| Code | Description | Resolution |
|------|-------------|------------|
| 200 | Success | Request completed successfully |
| 500 | Internal Server Error | Check logs for detailed error information |
| 503 | Service Unavailable | Service is unhealthy, check health endpoint |
| 429 | Too Many Requests | Rate limit exceeded, reduce request frequency |

### Alert Severity Levels

| Level | Description | Action Required |
|-------|-------------|-----------------|
| Critical | Service-impacting issue | Immediate action required |
| High | Performance degradation | Action required within 1 hour |
| Medium | Warning condition | Monitor and plan resolution |
| Low | Informational | No immediate action required |

### Common Error Scenarios

```json
{
  "error": "ESPN API unavailable",
  "code": "ESPN_API_DOWN",
  "details": {
    "lastSuccessfulCall": "2025-09-19T15:30:00.000Z",
    "failureCount": 5,
    "nextRetryAt": "2025-09-19T16:35:00.000Z"
  }
}
```

---

## Rate Limiting

### ESPN API Limits
- **Default**: 100 requests per minute
- **Burst**: Up to 200 requests in 30-second window
- **Daily**: 10,000 requests per day (estimated)

### Service Rate Limiting
```json
{
  "RateLimit": {
    "RequestsPerMinute": 100,
    "BurstAllowance": 50,
    "SlidingWindowMinutes": 1
  }
}
```

### Rate Limit Headers
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 45
X-RateLimit-Reset: 1632067200
```

---

## Performance Expectations

### Response Time Baselines
- **Health Check**: < 100ms
- **Metrics**: < 200ms
- **System Info**: < 150ms
- **Alerts**: < 100ms
- **Full Diagnostic**: < 500ms

### Throughput Expectations
- **Concurrent Requests**: Up to 50
- **Peak QPS**: 100 requests/second
- **Sustained Load**: 50 requests/second

---

For additional information, see:
- [Operational Runbooks](OPERATIONAL_RUNBOOKS.md)
- [Troubleshooting Guide](TROUBLESHOOTING.md)
- [Performance Tuning](PERFORMANCE_TUNING.md)