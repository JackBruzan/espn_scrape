#!/usr/bin/env pwsh
# Test ESPN API integration for Week 4 of 2025 NFL season

param(
    [string]$BaseUrl = "http://localhost:5001",
    [int]$Year = 2025,
    [int]$Week = 4,
    [int]$SeasonType = 2
)

Write-Host "=== ESPN API Integration Test ===" -ForegroundColor Cyan
Write-Host "Testing Week $Week of $Year NFL Season (SeasonType: $SeasonType)" -ForegroundColor Yellow
Write-Host "Base URL: $BaseUrl" -ForegroundColor Gray
Write-Host ""

try {
    # Test 1: Direct API test endpoint
    Write-Host "🔍 Test 1: Testing ESPN Core API endpoint directly..." -ForegroundColor Green
    $testUrl = "$BaseUrl/api/EspnIntegration/test/schedule-api?year=$Year&week=$Week&seasonType=$SeasonType"
    Write-Host "URL: $testUrl" -ForegroundColor Gray
    
    $response1 = Invoke-RestMethod -Uri $testUrl -Method GET -ContentType "application/json"
    
    Write-Host "✅ API Test Results:" -ForegroundColor Green
    Write-Host "   Success: $($response1.Success)" -ForegroundColor $(if($response1.Success) { "Green" } else { "Red" })
    Write-Host "   Year: $($response1.Year)" -ForegroundColor White
    Write-Host "   Week: $($response1.Week)" -ForegroundColor White
    Write-Host "   Season Type: $($response1.SeasonType)" -ForegroundColor White
    Write-Host "   Schedule Count: $($response1.ScheduleCount)" -ForegroundColor $(if($response1.ScheduleCount -gt 0) { "Green" } else { "Yellow" })
    
    if ($response1.ScheduleCount -gt 0) {
        Write-Host "   📋 Sample Games:" -ForegroundColor Cyan
        foreach ($game in $response1.Schedules) {
            Write-Host "      🏈 $($game.AwayTeamName) @ $($game.HomeTeamName)" -ForegroundColor White
            Write-Host "         Game Time: $($game.GameTime)" -ForegroundColor Gray
            if ($game.HomeMoneyline -or $game.AwayMoneyline) {
                Write-Host "         Moneyline: Away $($game.AwayMoneyline) | Home $($game.HomeMoneyline)" -ForegroundColor Yellow
            }
            Write-Host ""
        }
    } else {
        Write-Host "   ⚠️  No games found for this week" -ForegroundColor Yellow
    }
    
    Write-Host ""
    
    # Test 2: Manual job trigger
    Write-Host "🚀 Test 2: Triggering manual schedule sync job..." -ForegroundColor Green
    $syncUrl = "$BaseUrl/api/EspnIntegration/sync/schedule"
    $syncBody = @{
        Year = $Year
        Week = $Week
        SeasonType = $SeasonType
    } | ConvertTo-Json
    
    Write-Host "URL: $syncUrl" -ForegroundColor Gray
    Write-Host "Body: $syncBody" -ForegroundColor Gray
    
    $response2 = Invoke-RestMethod -Uri $syncUrl -Method POST -ContentType "application/json" -Body $syncBody
    
    Write-Host "✅ Job Trigger Results:" -ForegroundColor Green
    Write-Host "   Job Key: $($response2.JobKey)" -ForegroundColor White
    Write-Host "   Triggered At: $($response2.TriggeredAt)" -ForegroundColor White
    Write-Host "   Message: $($response2.Message)" -ForegroundColor White
    
    Write-Host ""
    Write-Host "🎉 All tests completed successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "💡 Notes:" -ForegroundColor Cyan
    Write-Host "   - If ScheduleCount is 0, the 2025 season data may not be available yet in ESPN's API" -ForegroundColor Yellow
    Write-Host "   - Try testing with 2024 data: .\test_week4_2025.ps1 -Year 2024 -Week 4" -ForegroundColor Yellow
    Write-Host "   - Check the application logs in the terminal for detailed API call information" -ForegroundColor Yellow
    
} catch {
    Write-Host "❌ Error occurred during testing:" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "💡 Troubleshooting:" -ForegroundColor Cyan
    Write-Host "   - Make sure the application is running on $BaseUrl" -ForegroundColor Yellow
    Write-Host "   - Check if the endpoints are accessible" -ForegroundColor Yellow
    Write-Host "   - Verify the application logs for any errors" -ForegroundColor Yellow
    exit 1
}

Write-Host "=== Test Complete ===" -ForegroundColor Cyan