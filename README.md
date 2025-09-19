# ESPN API Service

A comprehensive .NET 8 service for ESPN NFL data collection with enterprise-grade monitoring, alerting, and diagnostic capabilities. Built with ASP.NET Core, featuring scheduled data collection, real-time monitoring, performance metrics, and production-ready operational tools.

## 🚀 Features

### Core ESPN API Integration
- **NFL Data Collection**: Complete access to ESPN's NFL API for games, players, teams, and statistics
- **Scheduled Jobs**: Automated data collection using Quartz.NET scheduling
- **Rate Limiting**: Built-in ESPN API rate limiting and retry policies
- **Caching Layer**: Intelligent caching with configurable TTL for different data types
- **Bulk Operations**: Efficient batch processing for large data sets

### Enterprise Monitoring & Alerting
- **Real-time Metrics**: Performance tracking for API calls, cache hit rates, and system resources
- **Advanced Alerting**: Configurable thresholds with multi-level severity (Critical, High, Medium)
- **Health Monitoring**: Comprehensive health checks for all service dependencies
- **Structured Logging**: JSON-formatted logs with correlation IDs and contextual data
- **Background Monitoring**: Continuous alert processing with notification support

### Production-Ready Operations
- **Diagnostic API**: 7 REST endpoints for system monitoring and troubleshooting
- **Performance Tracking**: Detailed metrics collection with time-series data
- **Correlation Tracking**: Request tracing across service boundaries
- **Resilience Patterns**: Circuit breakers, retries, and timeout handling
- **Docker Ready**: Containerized deployment with production configuration

## 📊 Service Architecture

```
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   ESPN API      │◄───┤  ESPN API Service │───►│   Diagnostic    │
│   (External)    │    │                  │    │   Endpoints     │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                              │
                              ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Cache Layer   │◄───┤  Core Services   │───►│   Monitoring    │
│  (Memory/Redis) │    │                  │    │   & Alerting    │
└─────────────────┘    └──────────────────┘    └─────────────────┘
                              │
                              ▼
┌─────────────────┐    ┌──────────────────┐    ┌─────────────────┐
│   Scheduled     │◄───┤   Data Storage   │───►│   Logging &     │
│   Jobs (Quartz) │    │   & Processing   │    │   Metrics       │
└─────────────────┘    └──────────────────┘    └─────────────────┘
```

## 🛠️ Quick Start

### Prerequisites
- .NET 8.0 SDK or later
- Docker (for containerized deployment)
- Visual Studio 2022 or VS Code (for development)

### Local Development

1. **Clone the repository**
```bash
git clone https://github.com/JackBruzan/espn_scrape.git
cd espn_scrape
```

2. **Restore dependencies**
```bash
dotnet restore
```

3. **Run the application**
```bash
dotnet run --project ESPNScrape.csproj
```

4. **Access diagnostic endpoints**
```bash
# Health check
curl http://localhost:5000/health

# System metrics
curl http://localhost:5000/metrics

# Active alerts
curl http://localhost:5000/alerts
```

### Docker Deployment

1. **Build the Docker image**
```bash
docker build -t espn-api-service .
```

2. **Run with Docker**
```bash
docker run -d \
  --name espn-service \
  -p 5000:80 \
  -v $(pwd)/logs:/app/logs \
  -v $(pwd)/downloads:/app/downloads \
  espn-api-service
```

3. **Using Docker Compose** (recommended for production)
```bash
docker-compose up -d
```

## 📚 API Documentation

### Diagnostic Endpoints

| Endpoint | Method | Description | Response |
|----------|---------|-------------|----------|
| `/health` | GET | Health check status with detailed metrics | JSON health report |
| `/metrics` | GET | Performance and business metrics | JSON metrics data |
| `/system-info` | GET | System resource information | JSON system stats |
| `/alerts` | GET | Current alert conditions | JSON alert list |
| `/metrics/reset` | POST | Reset all metrics (testing) | Success confirmation |
| `/config` | GET | Service configuration details | JSON config data |
| `/full-diagnostic` | GET | Comprehensive diagnostic report | JSON full report |

### Health Check Response Example
```json
{
  "status": "Healthy",
  "totalDuration": 45.23,
  "entries": {
    "espn_api": {
      "status": "Healthy",
      "duration": 12.34,
      "description": "ESPN API connectivity check",
      "data": {
        "endpoint": "https://sports.core.api.espn.com",
        "responseTime": 123
      }
    }
  }
}
```

### Metrics Response Example
```json
{
  "timestamp": "2025-09-19T16:30:00.000Z",
  "performance": {
    "apiResponseTime": {
      "average": 245.67,
      "minimum": 89.12,
      "maximum": 1234.56,
      "count": 150
    },
    "cacheMetrics": {
      "hitRate": 85.4,
      "totalOperations": 1000,
      "averageDuration": 2.3
    }
  },
  "business": {
    "gamesProcessed": 16,
    "playersExtracted": 1894,
    "dataVolumeGB": 2.4
  }
}
```

## ⚙️ Configuration

The service is configured through `appsettings.json` with the following key sections:

### ESPN API Configuration
```json
{
  "EspnApi": {
    "BaseUrl": "https://sports.core.api.espn.com",
    "RateLimitRequestsPerMinute": 100,
    "DefaultTimeout": "00:00:30",
    "MaxRetryAttempts": 3
  }
}
```

### Logging and Monitoring
```json
{
  "Logging": {
    "StructuredLogging": {
      "EnableStructuredLogging": true,
      "UseJsonFormat": true,
      "IncludeScopes": true
    },
    "PerformanceMetrics": {
      "TrackResponseTimes": true,
      "TrackCacheMetrics": true,
      "EnableDetailedMetrics": true
    },
    "Alerting": {
      "EnableAlerting": true,
      "ErrorRateThreshold": 5.0,
      "ResponseTimeThresholdMs": 2000,
      "MonitoringInterval": "00:01:00"
    }
  }
}
```

### Cache Configuration
```json
{
  "Cache": {
    "DefaultTtlMinutes": 30,
    "SeasonDataTtlHours": 24,
    "LiveGameTtlSeconds": 30,
    "MaxCacheSize": 1000
  }
}
```

### Scheduled Jobs
```json
{
  "Quartz": {
    "Scheduler": {
      "InstanceName": "ESPNScrapeScheduler"
    },
    "ThreadPool": {
      "ThreadCount": 3
    }
  }
}
```

## 🔍 Monitoring and Observability

### Structured Logging
All operations are logged with structured data including:
- **Correlation IDs**: Track requests across service boundaries
- **Performance Metrics**: Response times, cache hit rates, error rates
- **Business Metrics**: Games processed, players extracted, data volumes
- **Health Status**: Service dependency health and availability

### Alert Conditions
The service monitors and alerts on:
- **Error Rate**: > 5% API call failures
- **Response Time**: > 2000ms average response time
- **Memory Usage**: > 80% memory utilization
- **Cache Hit Rate**: < 70% cache effectiveness

### Metrics Collection
Real-time collection of:
- API response times and status codes
- Cache performance (hit/miss rates, operation times)
- System resources (memory, CPU, disk)
- Business KPIs (data processing volumes, error counts)

## 🚀 Production Deployment

### Environment Variables
```bash
# Required
ASPNETCORE_ENVIRONMENT=Production
DOTNET_RUNNING_IN_CONTAINER=true

# Optional overrides
ESPN_API_BASE_URL=https://sports.core.api.espn.com
CACHE_DEFAULT_TTL_MINUTES=30
LOGGING_LEVEL=Information
```

### Health Check Monitoring
Configure your monitoring system to check:
- `GET /health` - Primary health endpoint
- Expected response: `200 OK` with `"status": "Healthy"`
- Check interval: 30 seconds
- Timeout: 10 seconds

### Performance Baselines
Expected performance characteristics:
- **API Response Time**: < 500ms (95th percentile)
- **Cache Hit Rate**: > 80%
- **Memory Usage**: < 512MB under normal load
- **Error Rate**: < 1%

## 🔧 Development

### Project Structure
```
ESPNScrape/
├── Configuration/          # Service configuration classes
├── Controllers/           # API controllers (diagnostics)
├── HealthChecks/         # Health check implementations
├── Jobs/                 # Quartz.NET scheduled jobs
├── Models/               # Data models and DTOs
│   └── Espn/            # ESPN API response models
├── Services/             # Core service implementations
│   └── Interfaces/      # Service contracts
└── Tests/                # Unit and integration tests
```

### Building and Testing
```bash
# Build the solution
dotnet build

# Run all tests
dotnet test

# Run with hot reload (development)
dotnet watch run

# Build for production
dotnet publish -c Release -o ./publish
```

### Adding New Features
1. **Create service interface** in `Services/Interfaces/`
2. **Implement service** in `Services/`
3. **Register in DI** in `Program.cs`
4. **Add configuration** in `appsettings.json`
5. **Create tests** in `Tests/`

## 📋 Operational Procedures

### Service Startup
1. Verify ESPN API connectivity
2. Initialize cache warming
3. Start background monitoring services
4. Begin scheduled job execution

### Health Monitoring
- Monitor `/health` endpoint continuously
- Check alert conditions via `/alerts` endpoint
- Review performance metrics at `/metrics`
- Investigate issues using `/full-diagnostic`

### Maintenance Windows
1. Disable scheduled jobs temporarily
2. Allow current operations to complete
3. Perform maintenance activities
4. Verify service health before resuming

## 🆘 Troubleshooting

### Common Issues

**High Error Rate**
1. Check ESPN API status and connectivity
2. Review rate limiting configuration
3. Examine error logs for specific failures
4. Verify network connectivity and DNS resolution

**Poor Cache Performance**
1. Monitor cache hit rates via `/metrics`
2. Review cache TTL configuration
3. Check memory usage and cache size limits
4. Analyze cache key patterns for effectiveness

**Memory Issues**
1. Monitor memory usage via `/system-info`
2. Check for memory leaks in metrics data
3. Review cache size and retention policies
4. Analyze GC pressure and collection frequency

### Log Analysis
```bash
# Find correlation ID for request tracing
grep "correlation-id-here" logs/espn-scrape-*.json

# Monitor error patterns
grep '"level":"Error"' logs/espn-scrape-*.json | jq .message

# Performance analysis
grep '"duration"' logs/espn-scrape-*.json | jq .duration
```

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📞 Support

For issues and questions:
- **GitHub Issues**: [Repository Issues](https://github.com/JackBruzan/espn_scrape/issues)
- **Documentation**: See `/docs` directory for detailed guides
- **Monitoring**: Use diagnostic endpoints for real-time troubleshooting
dotnet run

# Or for testing with 5-second intervals
dotnet run --environment Development
```

### Docker Deployment

#### Using Docker Compose (Recommended)

```bash
# Build and run with docker-compose
docker-compose up -d

# View logs
docker-compose logs -f

# Stop the service
docker-compose down
```

#### Using Docker Commands

```bash
# Build the image
docker build -t espn-scrape .

# Run the container
docker run -d \
  --name espn-scrape-service \
  --restart unless-stopped \
  -v ./downloads:/app/downloads \
  -v ./logs:/app/logs \
  espn-scrape

# View logs
docker logs -f espn-scrape-service
```

#### Using Batch Scripts (Windows)

```batch
# Build and run the container
docker-run.bat

# Stop and remove the container
docker-stop.bat
```

### Production Build

```bash
dotnet build -c Release
dotnet publish -c Release -o ./publish
```

## Job Schedule

By default, the scraping job runs every hour. You can modify the schedule in `Program.cs`:

```csharp
.WithCronSchedule("0 0 * * * ?") // Every hour
```

Common Cron expressions:
- `0 0 * * * ?` - Every hour
- `0 0 */6 * * ?` - Every 6 hours
- `0 0 12 * * ?` - Daily at noon
- `0 0 12 * * MON-FRI` - Weekdays at noon

## Logging

Logs are written to:
- Console (when running in development)
- `logs/espn-scrape-{date}.txt` files (rolling daily)

Log levels can be configured in `appsettings.json`.

## Downloaded Images

Images are organized in the following structure:
```
downloads/
└── nfl_players/
    ├── DAL/          (Dallas Cowboys players)
    ├── NE/           (New England Patriots players)
    ├── GB/           (Green Bay Packers players)
    ├── SF/           (San Francisco 49ers players)
    ├── KC/           (Kansas City Chiefs players)
    └── [Other teams]/ (All 32 NFL teams)
```

Each image is named: `{PlayerId}_{PlayerName}.png`

**Note**: Only players currently assigned to NFL teams will have their images downloaded. Free agents, practice squad players, and players without team assignments are filtered out.

## Dependencies

- **Quartz.NET**: Job scheduling framework
- **System.Text.Json**: JSON parsing for ESPN API responses
- **Serilog**: Structured logging
- **Microsoft.Extensions.Hosting**: .NET hosting framework

## Data Sources

- **Player Data**: ESPN API (`https://sports.core.api.espn.com/v3/sports/football/nfl/athletes`)
- **Player Images**: ESPN CDN (`https://a.espncdn.com/combiner/i?img=/i/headshots/nfl/players/full/{playerId}.png`)

## Volume Mounts

When running in Docker, the following directories are mounted as volumes:

- `/app/downloads` - Downloaded player images (persistent)
- `/app/logs` - Application logs (persistent)

## Environment Variables

- `ASPNETCORE_ENVIRONMENT` - Set to `Development` for debug logging
- `DOTNET_RUNNING_IN_CONTAINER` - Automatically set when running in Docker

## License

This project is for educational purposes. Please respect ESPN's API terms of service when using this application.
