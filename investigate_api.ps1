#!/usr/bin/env pwsh
# Investigate ESPN Core API data availability

param(
    [string]$BaseUrl = "http://localhost:5001"
)

Write-Host "=== ESPN Core API Investigation ===" -ForegroundColor Cyan
Write-Host "Let's find out what data is actually available..." -ForegroundColor Yellow
Write-Host ""

$testCases = @(
    @{Year=2024; Week=1; Desc="2024 Week 1 (Past season start)"},
    @{Year=2024; Week=4; Desc="2024 Week 4 (Mid season)"},
    @{Year=2024; Week=17; Desc="2024 Week 17 (End of regular season)"},
    @{Year=2023; Week=4; Desc="2023 Week 4 (Previous season)"},
    @{Year=2025; Week=1; Desc="2025 Week 1 (Future season start)"},
    @{Year=2025; Week=4; Desc="2025 Week 4 (Future mid season)"}
)

foreach ($test in $testCases) {
    Write-Host "🔍 Testing: $($test.Desc)" -ForegroundColor Green
    
    try {
        $testUrl = "$BaseUrl/api/EspnIntegration/test/schedule-api?year=$($test.Year)&week=$($test.Week)"
        $response = Invoke-RestMethod -Uri $testUrl -Method GET -ContentType "application/json"
        
        if ($response.ScheduleCount -gt 0) {
            Write-Host "   ✅ Found $($response.ScheduleCount) games!" -ForegroundColor Green
            Write-Host "   📋 Sample game: $($response.Schedules[0].AwayTeamName) @ $($response.Schedules[0].HomeTeamName)" -ForegroundColor White
        } else {
            Write-Host "   ❌ No games found" -ForegroundColor Red
        }
    } catch {
        Write-Host "   💥 Error: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host "=== Raw ESPN API Test ===" -ForegroundColor Cyan
Write-Host "Let's also test the ESPN API directly..." -ForegroundColor Yellow

# Test ESPN API directly for 2024 data
$directUrls = @(
    "https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2024/types/2/weeks/1/events",
    "https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2023/types/2/weeks/1/events"
)

foreach ($url in $directUrls) {
    Write-Host "🌐 Testing direct ESPN API: $url" -ForegroundColor Green
    try {
        $response = Invoke-RestMethod -Uri $url -Method GET -Headers @{
            'User-Agent' = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36'
            'Accept' = 'application/json'
        }
        
        $eventCount = ($response.items | Measure-Object).Count
        Write-Host "   ✅ ESPN API returned $eventCount events" -ForegroundColor Green
        
        if ($eventCount -gt 0) {
            Write-Host "   📋 First event URL: $($response.items[0])" -ForegroundColor White
        }
    } catch {
        Write-Host "   💥 Error: $($_.Exception.Message)" -ForegroundColor Red
    }
    Write-Host ""
}

Write-Host "=== Investigation Complete ===" -ForegroundColor Cyan