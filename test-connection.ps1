# Simple test script to debug the API connection
param(
    [string]$BaseUrl = "http://localhost:60772"
)

Write-Host "Testing connection to: $BaseUrl" -ForegroundColor Yellow

# Test 1: Basic connectivity
Write-Host "`n=== Test 1: Basic Connectivity ===" -ForegroundColor Cyan
try {
    $response = Invoke-WebRequest -Uri "$BaseUrl/api/Diagnostics/health" -Method GET -TimeoutSec 5 -UseBasicParsing
    Write-Host "✅ Health endpoint responded with status: $($response.StatusCode)" -ForegroundColor Green
    Write-Host "Response length: $($response.Content.Length) chars" -ForegroundColor White
} catch {
    Write-Host "❌ Health endpoint failed:" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    
    # Test 2: Try sync status endpoint
    Write-Host "`n=== Test 2: Sync Status Endpoint ===" -ForegroundColor Cyan
    try {
        $response2 = Invoke-WebRequest -Uri "$BaseUrl/api/EspnIntegration/sync/status" -Method GET -TimeoutSec 5 -UseBasicParsing
        Write-Host "✅ Sync status endpoint responded with status: $($response2.StatusCode)" -ForegroundColor Green
        Write-Host "Response length: $($response2.Content.Length) chars" -ForegroundColor White
    } catch {
        Write-Host "❌ Sync status endpoint also failed:" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        
        # Test 3: Try a simple HTTP request to root
        Write-Host "`n=== Test 3: Root Endpoint ===" -ForegroundColor Cyan
        try {
            $response3 = Invoke-WebRequest -Uri "$BaseUrl/" -Method GET -TimeoutSec 5 -UseBasicParsing
            Write-Host "✅ Root endpoint responded with status: $($response3.StatusCode)" -ForegroundColor Green
        } catch {
            Write-Host "❌ Root endpoint failed:" -ForegroundColor Red
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "`n🔍 This suggests the application might not be running or accessible" -ForegroundColor Yellow
        }
    }
}

Write-Host "`n=== Connection Test Complete ===" -ForegroundColor Cyan