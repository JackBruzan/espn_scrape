# ESPN NFL Player Image Scraper

A C# hosted service application that uses Quartz.NET to schedule periodic downloading of NFL player headshot images from ESPN. Designed to run in Docker containers.

## Features

- **Scheduled Player Image Downloads**: Uses Quartz.NET to run download jobs on a schedule
- **ESPN API Integration**: Fetches active NFL player data from ESPN's official API
- **Player Headshots**: Downloads high-quality player headshot images from ESPN CDN
- **Team Organization**: Organizes downloaded images by team folders
- **Team Filtering**: Only downloads images for players currently assigned to NFL teams
- **Duplicate Prevention**: Skips downloading images that already exist
- **Comprehensive Logging**: Uses Serilog for detailed logging to console and files
- **Docker Ready**: Containerized application with volume mounting for data persistence

## How It Works

1. **Fetch Player Data**: Retrieves all active NFL players from ESPN API (`https://sports.core.api.espn.com/v3/sports/football/nfl/athletes?limit=20000&active=true`)
2. **Filter Players**: Only processes players who are currently assigned to an NFL team (excludes free agents, retired players, etc.)
3. **Generate Image URLs**: Uses player IDs to construct headshot URLs (`https://a.espncdn.com/combiner/i?img=/i/headshots/nfl/players/full/{playerId}.png`)
4. **Download Images**: Downloads each team-assigned player's headshot and organizes by team
5. **Smart Organization**: Creates folder structure by team abbreviation (e.g., `downloads/nfl_players/DAL/`, `downloads/nfl_players/NE/`)

## Prerequisites

- .NET 8.0 or later (for local development)
- Docker (for containerized deployment)

## Configuration

The application can be configured through `appsettings.json`:

```json
{
  "ESPNScrape": {
    "DownloadDirectory": "downloads/nfl_players",
    "DelayBetweenDownloads": 100,
    "UserAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36",
    "PlayersApiUrl": "https://sports.core.api.espn.com/v3/sports/football/nfl/athletes?limit=20000&active=true",
    "PlayerImageBaseUrl": "https://a.espncdn.com/combiner/i?img=/i/headshots/nfl/players/full/{0}.png"
  }
}
```

## Building and Running

### Local Development

```bash
# Run directly with .NET
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
